using System.Text.Json.Nodes;

namespace Crm.Application.Dtos.CustomRecords;

/// <summary>
/// Represents a data transfer object for a dynamic custom table record.
/// </summary>
/// <param name="Id">The unique identifier of the record.</param>
/// <param name="Data">The JSON object containing the field values for the custom record.</param>
public record CustomRecordDto(
    Guid Id,
    JsonObject Data);
