using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Crm.Infrastructure.Persistence.ValueComparers;

/// <summary>
/// Provides structural equality, deterministic hashing, and deep-clone snapshotting for mutable
/// <see cref="JsonObject"/> properties mapped to PostgreSQL <c>jsonb</c> columns so EF Core's change tracker
/// accurately detects in-place mutations and ignores semantically identical replacements.
/// </summary>
public sealed class JsonObjectValueComparer : ValueComparer<JsonObject>
{
    /// <summary>
    /// Gets the singleton instance of <see cref="JsonObjectValueComparer"/>.
    /// </summary>
    public static readonly JsonObjectValueComparer Instance = new();

    private JsonObjectValueComparer()
        : base(
            (left, right) => AreEqual(left, right),
            value => ComputeHashCode(value),
            value => CreateSnapshot(value))
    {
    }

    private static bool AreEqual(JsonObject? left, JsonObject? right)
        => ReferenceEquals(left, right) || JsonNode.DeepEquals(left, right);

    private static JsonObject CreateSnapshot(JsonObject value)
        => value is null ? [] : (JsonObject)value.DeepClone();

    private static int ComputeHashCode(JsonObject? value)
    {
        if (value is null)
        {
            return 0;
        }

        var hash = default(HashCode);
        AddNodeHash(ref hash, value);
        return hash.ToHashCode();
    }

    private static void AddNodeHash(ref HashCode hash, JsonNode? node)
    {
        if (node is null)
        {
            hash.Add(0);
            return;
        }

        switch (node)
        {
            case JsonObject obj:
                hash.Add((int)JsonValueKind.Object);
                foreach (var kvp in obj.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    hash.Add(kvp.Key, StringComparer.Ordinal);
                    AddNodeHash(ref hash, kvp.Value);
                }

                break;

            case JsonArray array:
                hash.Add((int)JsonValueKind.Array);
                foreach (var element in array)
                {
                    AddNodeHash(ref hash, element);
                }

                break;

            case JsonValue val:
                hash.Add((int)val.GetValueKind());
                hash.Add(val.ToJsonString(), StringComparer.Ordinal);
                break;

            default:
                hash.Add(node.ToJsonString(), StringComparer.Ordinal);
                break;
        }
    }
}
