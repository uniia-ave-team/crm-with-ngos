using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Services;
using NSubstitute;
using Shouldly;

namespace Crm.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="LocalFileStorageService"/> verifying installation-wide storage quotas
/// and atomic write cleanup when an upload fails or is cancelled mid-stream.
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

        Directory.GetFiles(Path.Combine(_tempDir, "attachments")).Length.ShouldBe(2);
        secondFile.ShouldNotBeNullOrWhiteSpace();
        thirdFile.ShouldNotBeNullOrWhiteSpace();
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

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private LocalFileStorageService CreateService(long maxTotalBytes)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(new FileStorageOptions
        {
            UploadDirectory = _tempDir,
            BaseUrl = "http://localhost:5065",
            MaxTotalBytes = maxTotalBytes,
        });

        return new LocalFileStorageService(opts, _imageValidator);
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
