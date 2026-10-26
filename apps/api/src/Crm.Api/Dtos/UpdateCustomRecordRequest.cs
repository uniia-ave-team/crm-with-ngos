using System.Text.Json.Nodes;

namespace Crm.Api.Dtos;

/// <summary>
/// Represents the API request body for updating an existing custom record's JSON data payload.
/// </summary>
/// <param name="Data">The JSON object containing the updated field values for the custom record.</param>
public record UpdateCustomRecordRequest(JsonObject Data);
