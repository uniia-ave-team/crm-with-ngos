using Crm.Domain.Interfaces.Entities;

namespace Crm.Application.Dtos.Common;

/// <summary>
/// Shared data transfer object representing a dynamic column definition for custom table creation
/// and table template blueprints (keys are generated automatically by the server when persisted).
/// </summary>
/// <param name="Name">The human-readable display name of the field.</param>
/// <param name="IsRequired">Whether a non-null string value is required for this field.</param>
/// <param name="Order">The zero-based display order of the field.</param>
public sealed record FieldDefinitionDto(
    string Name,
    bool IsRequired,
    byte Order) : IFieldDefinition;
