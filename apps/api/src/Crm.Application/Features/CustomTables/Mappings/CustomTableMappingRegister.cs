using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.TableTemplates;
using Crm.Application.Features.CustomTables.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Entities;
using Mapster;

namespace Crm.Application.Features.CustomTables.Mappings;

/// <summary>
/// Registers Mapster mapping rules for <see cref="CustomTable"/> and <see cref="CustomField"/> entities, DTOs, and commands.
/// </summary>
public sealed class CustomTableMappingRegister : IRegister
{
    /// <inheritdoc />
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CustomTable, CustomTableSummaryDto>()
            .MapWith(src => new CustomTableSummaryDto(
                src.Id,
                src.Name,
                src.Description,
                src.SourceTemplateId,
                src.Fields.Count(),
                src.Records.Count()));

        config.NewConfig<CustomTable, CustomTableDto>()
            .MapWith(src => new CustomTableDto(
                src.Id,
                src.NgoId,
                src.Name,
                src.Description,
                src.SourceTemplateId,
                src.Fields
                    .OrderBy(f => f.Order)
                    .ThenBy(f => f.Id)
                    .Select(f => new CustomFieldDto(
                        f.Id,
                        f.Name,
                        f.Key,
                        f.DataType,
                        f.IsRequired,
                        f.Order))
                    .ToList()));

        config.NewConfig<TableTemplateDto, CustomTable>()
            .Map(dest => dest.SourceTemplateId, src => src.Id)
            .Ignore(dest => dest.Id);

        config.NewConfig<TemplateFieldDto, CustomField>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.CustomTableId);

        config.NewConfig<UpdateCustomTableCommand, CustomTable>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Fields);

        config.NewConfig<UpdateCustomFieldDto, CustomField>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.CustomTableId)
            .Ignore(dest => dest.CustomTable);

        config.NewConfig<IIdentifiableField, CustomField>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.CustomTableId)
            .Ignore(dest => dest.CustomTable);
    }
}
