using System.Reflection;
using Crm.Api.Controllers.V1;
using Crm.Application.Common.Consts;
using Crm.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Crm.Architecture.Tests;

/// <summary>
/// Verifies that all structured logging event identifiers in <see cref="LogEventIds"/> have unique integer values
/// and are each used exactly once across the application's <see cref="LoggerMessageAttribute"/> declarations.
/// </summary>
public class LogEventIdsTests
{
    private static readonly Assembly[] LoggedAssemblies =
    [
        typeof(LogEventIds).Assembly,
        typeof(ApplicationDbContext).Assembly,
        typeof(ProjectsController).Assembly,
    ];

    [Fact]
    public void AllLogEventIdsShouldHaveUniqueValues()
    {
        FieldInfo[] fields = GetLogEventIdFields();

        Assert.NotEmpty(fields);

        var duplicates = fields
            .GroupBy(f => (int)f.GetRawConstantValue()!)
            .Where(g => g.Count() > 1)
            .Select(g => $"EventId {g.Key}: {string.Join(", ", g.Select(f => f.Name))}")
            .ToList();

        Assert.True(
            duplicates.Count == 0,
            $"Duplicate LogEventIds values found: {string.Join("; ", duplicates)}");
    }

    [Fact]
    public void AllLogEventIdsShouldBeUsedExactlyOnceAcrossApplication()
    {
        Dictionary<int, string> definedConstants = GetLogEventIdFields()
            .ToDictionary(f => (int)f.GetRawConstantValue()!, f => f.Name);

        List<(int EventId, string Location)> attributeUsages = [
            .. LoggedAssemblies
                .SelectMany(a => a.GetTypes())
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<LoggerMessageAttribute>(inherit: false)
                        .Select(attr => (attr.EventId, $"{t.FullName}.{m.Name}"))))
                .Distinct(),
        ];

        var usageCounts = attributeUsages
            .GroupBy(u => u.EventId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Location).ToList());

        List<string> unusedConstants = [
            .. definedConstants
                .Where(kvp => !usageCounts.ContainsKey(kvp.Key))
                .Select(kvp => $"{kvp.Value} ({kvp.Key})"),
        ];

        List<string> multiUsedConstants = [
            .. usageCounts
                .Where(kvp => kvp.Value.Count > 1)
                .Select(kvp =>
                {
                    string name = definedConstants.TryGetValue(kvp.Key, out string? n) ? n : "Unknown";
                    return $"{name} ({kvp.Key}) used in: {string.Join(", ", kvp.Value)}";
                }),
        ];

        Assert.True(
            unusedConstants.Count == 0,
            $"Unused LogEventIds constants found: {string.Join(", ", unusedConstants)}");

        Assert.True(
            multiUsedConstants.Count == 0,
            $"LogEventIds constants used more than once: {string.Join("; ", multiUsedConstants)}");
    }

    private static FieldInfo[] GetLogEventIdFields()
        => [
            .. typeof(LogEventIds)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(int)),
        ];
}
