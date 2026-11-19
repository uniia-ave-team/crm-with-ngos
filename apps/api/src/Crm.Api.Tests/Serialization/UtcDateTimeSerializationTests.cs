using System.Text.Json;
using Crm.Api.Serialization;
using Shouldly;

namespace Crm.Api.Tests.Serialization;

/// <summary>
/// Verifies <see cref="UtcDateTimeJsonConverter"/> and <see cref="UtcDateTimeParser"/>
/// handle both <see cref="DateTime"/> and nullable <see cref="DateTime"/>? payloads in a single parse pass.
/// </summary>
public class UtcDateTimeSerializationTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    [Fact]
    public void UtcDateTimeJsonConverterShouldHandleBothNonNullableAndNullableDateTimes()
    {
        string json = """{"Required":"2026-11-20T02:00:00+02:00","Optional":"2026-11-20T00:00:00","Cleared":null}""";

        var deserialized = JsonSerializer.Deserialize<DatePayload>(json, Options);

        deserialized.ShouldNotBeNull();
        deserialized.Required.Kind.ShouldBe(DateTimeKind.Utc);
        deserialized.Required.ShouldBe(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));
        deserialized.Optional.ShouldNotBeNull();
        deserialized.Optional.Value.Kind.ShouldBe(DateTimeKind.Utc);
        deserialized.Optional.Value.ShouldBe(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));
        deserialized.Cleared.ShouldBeNull();
    }

    [Theory]
    [InlineData("2026-11-20T00:00:00", 2026, 11, 20, 0)]
    [InlineData("2026-11-20T02:00:00+02:00", 2026, 11, 20, 0)]
    [InlineData("2026-11-20T00:00:00Z", 2026, 11, 20, 0)]
    public void TryParseUtcShouldParseInUtc(string raw, int year, int month, int day, int hour)
    {
        UtcDateTimeParser.TryParseUtc(raw, out DateTime parsed).ShouldBeTrue();
        parsed.Kind.ShouldBe(DateTimeKind.Utc);
        parsed.ShouldBe(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeJsonConverter());
        return options;
    }

    private sealed record DatePayload(DateTime Required, DateTime? Optional, DateTime? Cleared);
}
