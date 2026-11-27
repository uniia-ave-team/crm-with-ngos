using System.Text.Json.Nodes;

namespace Crm.Application.Dtos.CustomRecords;

/// <summary>
/// Data transfer object representing a dynamic data row stored in a custom table.
/// </summary>
/// <param name="Id">The unique identifier of the custom record.</param>
/// <param name="Data">The JSON object containing string cell values keyed by column field keys.</param>
public sealed record CustomRecordDto(
    Guid Id,
    JsonObject Data);
