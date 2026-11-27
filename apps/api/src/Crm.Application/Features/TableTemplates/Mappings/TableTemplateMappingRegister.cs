using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.TableTemplates;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.TableTemplates.Mappings;

/// <summary>
/// Registers Mapster mapping rules for <see cref="TableTemplate"/> entities and DTOs.
/// </summary>
public sealed class TableTemplateMappingRegister : IRegister
{
    /// <inheritdoc />
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<TableTemplate, TableTemplateDto>()
            .MapWith(src => new TableTemplateDto(
                src.Id,
                src.Name,
                src.Description,
                src.IsSystemPreset,
                src.Fields
                    .OrderBy(f => f.Order)
                    .Select(f => new FieldDefinitionDto(
                        f.Name,
                        f.IsRequired,
                        f.Order))
                    .ToList()));
    }
}
