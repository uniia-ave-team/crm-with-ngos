namespace Crm.Application.Dtos.CustomTables;

/// <summary>
/// Represents a lightweight summary DTO of a custom table for paginated dashboard lists.
/// </summary>
/// <param name="Id">The unique identifier of the custom table.</param>
/// <param name="Name">The display name of the table.</param>
/// <param name="Description">An optional description explaining the table's purpose.</param>
/// <param name="SourceTemplateId">The optional identifier of the source template.</param>
/// <param name="FieldsCount">The total number of columns defined in this table.</param>
/// <param name="RecordsCount">The total number of stored records in this table.</param>
public record CustomTableSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? SourceTemplateId,
    int FieldsCount,
    int RecordsCount);
