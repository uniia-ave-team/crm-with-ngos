using System.Text;
using Crm.Domain.Exceptions;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Services.Storage;
using Crm.Infrastructure.Services.Storage.FileValidators;
using NSubstitute;
using Shouldly;

namespace Crm.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="LocalFileStorageService"/> verifying installation-wide storage quotas,
/// atomic write cleanup on failure, stale <c>.tmp</c> cleanup, and async stream lifetime safety for large SVG images.
/// </summary>
public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IImageValidator _imageValidator = Substitute.For<IImageValidator>();

    public LocalFileStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"crm-storage-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task SaveFileAsyncShouldEnforceInstallationMaxTotalBytesQuotaAndReleaseSpaceAfterDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService(maxTotalBytes: 100);

        using var firstStream = new MemoryStream(new byte[60]);
        string firstFile = await service.SaveFileAsync(firstStream, "first.pdf", "attachments", ct);

        // Second upload exceeds 100-byte installation quota -> StorageQuotaExceededException (413)
        using var oversizedStream = new MemoryStream(new byte[41]);
        await Should.ThrowAsync<StorageQuotaExceededException>(() =>
            service.SaveFileAsync(oversizedStream, "second.pdf", "attachments", ct));

        // Exact remaining quota succeeds
        using var exactStream = new MemoryStream(new byte[40]);
        string secondFile = await service.SaveFileAsync(exactStream, "second.pdf", "attachments", ct);

        // Deleting first file frees 60 bytes so another 50-byte upload succeeds
        await service.DeleteFileAsync(firstFile, "attachments", ct);
        using var afterDeleteStream = new MemoryStream(new byte[50]);
        string thirdFile = await service.SaveFileAsync(afterDeleteStream, "third.pdf", "attachments", ct);

        // Deleting second file directly on disk (out-of-band) is recovered by fallback re-scan when quota appears full
        File.Delete(Path.Combine(_tempDir, "attachments", secondFile));
        using var outOfBandRecoveryStream = new MemoryStream(new byte[45]);
        string fourthFile = await service.SaveFileAsync(outOfBandRecoveryStream, "fourth.pdf", "attachments", ct);

        Directory.GetFiles(Path.Combine(_tempDir, "attachments")).Length.ShouldBe(2);
        thirdFile.ShouldNotBeNullOrWhiteSpace();
        fourthFile.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SaveFileAsyncWhenUploadFailsMidStreamShouldLeaveNoPartialOrTempFilesOnDisk()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService(maxTotalBytes: 1024 * 1024);

        using var failingStream = new FailingMidReadStream(totalLength: 16384, failAfterBytes: 4096);

        await Should.ThrowAsync<IOException>(() =>
            service.SaveFileAsync(failingStream, "aborted.pdf", "attachments", ct));

        string attachmentsDir = Path.Combine(_tempDir, "attachments");
        if (Directory.Exists(attachmentsDir))
        {
            Directory.GetFiles(attachmentsDir, "*", SearchOption.AllDirectories).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task SaveImageAsyncWithLargeSvgOver4KbAndAsyncYieldShouldPersistEntireFileWithoutDisposingStreamEarly()
    {
        var ct = TestContext.Current.CancellationToken;

        // Build a valid > 20 KB SVG document
        var svgBuilder = new StringBuilder();
        svgBuilder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">");
        for (int i = 0; i < 600; i++)
        {
            svgBuilder.Append($"<rect x=\"{i % 100}\" y=\"{i / 100}\" width=\"10\" height=\"10\" fill=\"#336699\" />");
        }

        svgBuilder.Append("</svg>");
        byte[] svgBytes = Encoding.UTF8.GetBytes(svgBuilder.ToString());
        svgBytes.Length.ShouldBeGreaterThan(20 * 1024);

        // 1. Test with real SvgImageValidator (> 20 KB sanitized MemoryStream)
        var realValidator = new ImageValidator([new SvgImageValidator()]);
        var realService = CreateService(maxTotalBytes: 10 * 1024 * 1024, realValidator);
        using var svgInputStream = new MemoryStream(svgBytes);

        string savedSvgName = await realService.SaveImageAsync(svgInputStream, "logo.svg", "ngo", ct);
        string savedSvgPath = Path.Combine(_tempDir, "ngo", savedSvgName);
        File.Exists(savedSvgPath).ShouldBeTrue();
        new FileInfo(savedSvgPath).Length.ShouldBeGreaterThan(20 * 1024);

        // 2. Test with an async-yielding ResultStream (> 20 KB) to deterministically reproduce Linux FileStream async suspension
        var yieldingValidator = Substitute.For<IImageValidator>();
        yieldingValidator.Validate(Arg.Any<Stream>(), Arg.Any<string>())
            .Returns(_ => new FileValidationResult(IsValid: true, ResultStream: new YieldingDisposableStream(svgBytes)));

        var yieldingService = CreateService(maxTotalBytes: 10 * 1024 * 1024, yieldingValidator);
        using var secondInput = new MemoryStream(svgBytes);

        string savedYieldedName = await yieldingService.SaveImageAsync(secondInput, "avatar.svg", "users", ct);
        string savedYieldedPath = Path.Combine(_tempDir, "users", savedYieldedName);
        File.Exists(savedYieldedPath).ShouldBeTrue();
        (await File.ReadAllBytesAsync(savedYieldedPath, ct)).ShouldBe(svgBytes);
    }

    [Fact]
    public void CleanupStaleTempFilesShouldRemoveExpiredTempFilesAndKeepFreshTempAndRealFiles()
    {
        string folder = Path.Combine(_tempDir, "attachments");
        Directory.CreateDirectory(folder);

        var now = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        string staleTemp = Path.Combine(folder, "stale.pdf.abc.tmp");
        string freshTemp = Path.Combine(folder, "active.pdf.def.tmp");
        string realFile = Path.Combine(folder, "saved.pdf");

        File.WriteAllText(staleTemp, "partial");
        File.WriteAllText(freshTemp, "in-progress");
        File.WriteAllText(realFile, "real-data");

        File.SetLastWriteTimeUtc(staleTemp, now.AddHours(-2));
        File.SetLastWriteTimeUtc(freshTemp, now.AddMinutes(-5));
        File.SetLastWriteTimeUtc(realFile, now.AddDays(-5));

        var fixedClock = new FixedTimeProvider(new DateTimeOffset(now, TimeSpan.Zero));
        var quotaTracker = new LocalStorageQuotaTracker(fixedClock);

        int deleted = quotaTracker.CleanupStaleTempFiles(_tempDir, TimeSpan.FromHours(1), TestContext.Current.CancellationToken);

        deleted.ShouldBe(1);
        File.Exists(staleTemp).ShouldBeFalse();
        File.Exists(freshTemp).ShouldBeTrue();
        File.Exists(realFile).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveImageAsyncWhenImageContentIsInvalidShouldThrowDomainValidationException()
    {
        var ct = TestContext.Current.CancellationToken;
        _imageValidator.Validate(Arg.Any<Stream>(), Arg.Any<string>())
            .Returns(new FileValidationResult(IsValid: false, ResultStream: null));

        var service = CreateService(maxTotalBytes: 1024 * 1024);
        using var invalidStream = new MemoryStream(Encoding.UTF8.GetBytes("not-an-image"));

        await Should.ThrowAsync<DomainValidationException>(() =>
            service.SaveImageAsync(invalidStream, "fake.jpg", "users", ct));
    }

    [Fact]
    public async Task GetFileAsyncWhenFileDoesNotExistShouldThrowStorageFileNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService(maxTotalBytes: 1024 * 1024);

        await Should.ThrowAsync<StorageFileNotFoundException>(() =>
            service.GetFileAsync("non-existent.png", "users", ct));
    }

    [Fact]
    public async Task GetFileAsyncWhenIdentifierIsExternalUrlOrEmptyShouldThrowDomainValidationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService(maxTotalBytes: 1024 * 1024);

        await Should.ThrowAsync<DomainValidationException>(() =>
            service.GetFileAsync("https://cdn.example.org/avatar.png", "users", ct));
        await Should.ThrowAsync<DomainValidationException>(() =>
            service.GetFileAsync("   ", "users", ct));
    }

    [Fact]
    public async Task SaveFileAsyncWhenStreamIsEmptyOrFileNameMissingShouldThrowDomainValidationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService(maxTotalBytes: 1024 * 1024);

        using var emptyStream = new MemoryStream();
        await Should.ThrowAsync<DomainValidationException>(() =>
            service.SaveFileAsync(emptyStream, "empty.txt", "attachments", ct));

        using var validStream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        await Should.ThrowAsync<DomainValidationException>(() =>
            service.SaveFileAsync(validStream, "   ", "attachments", ct));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private LocalFileStorageService CreateService(long maxTotalBytes, IImageValidator? validator = null)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(new FileStorageOptions
        {
            UploadDirectory = _tempDir,
            BaseUrl = "http://localhost:5065",
            MaxTotalBytes = maxTotalBytes,
        });

        return new LocalFileStorageService(opts, validator ?? _imageValidator, new LocalStorageQuotaTracker(TimeProvider.System));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class YieldingDisposableStream(byte[] payload) : Stream
    {
        private int _position;
        private bool _disposed;

        public override bool CanRead => !_disposed;

        public override bool CanSeek => !_disposed;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return payload.Length;
            }
        }

        public override long Position
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _position;
            }

            set
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _position = (int)value;
            }
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            ObjectDisposedException.ThrowIf(_disposed, this);

            int remaining = payload.Length - _position;
            if (remaining <= 0)
            {
                return 0;
            }

            int toCopy = Math.Min(buffer.Length, remaining);
            payload.AsMemory(_position, toCopy).CopyTo(buffer);
            _position += toCopy;
            return toCopy;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            int remaining = payload.Length - _position;
            if (remaining <= 0)
            {
                return 0;
            }

            int toCopy = Math.Min(count, remaining);
            Array.Copy(payload, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _position = origin switch
            {
                SeekOrigin.Begin => (int)offset,
                SeekOrigin.Current => _position + (int)offset,
                SeekOrigin.End => payload.Length + (int)offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            return _position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FailingMidReadStream(long totalLength, int failAfterBytes) : Stream
    {
        private long _position;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => totalLength;

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= failAfterBytes)
            {
                throw new IOException("Simulated client disconnect mid-upload.");
            }

            int toCopy = (int)Math.Min(count, failAfterBytes - _position);
            Array.Fill(buffer, (byte)0x41, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position >= failAfterBytes)
            {
                throw new IOException("Simulated client disconnect mid-upload.");
            }

            int toCopy = (int)Math.Min(buffer.Length, failAfterBytes - _position);
            buffer[..toCopy].Span.Fill(0x41);
            _position += toCopy;
            return ValueTask.FromResult(toCopy);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => totalLength + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            return _position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
