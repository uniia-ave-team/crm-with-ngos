using Crm.Application.Dtos.TableTemplates;
using Crm.Application.Features.TableTemplates.Commands;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.TableTemplates.Mappings;

/// <summary>
/// Registers Mapster mapping rules for <see cref="TableTemplate"/> and <see cref="TemplateField"/> entities, DTOs, and commands.
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
                    .ThenBy(f => f.Id)
                    .Select(f => new TemplateFieldDto(
                        f.Id,
                        f.Name,
                        f.Key,
                        f.DataType,
                        f.IsRequired,
                        f.Order))
                    .ToList()));

        config.NewConfig<UpdateTableTemplateCommand, TableTemplate>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Fields);

        config.NewConfig<UpdateTemplateFieldDto, TemplateField>()
            .Ignore(dest => dest.Id);
    }
}
