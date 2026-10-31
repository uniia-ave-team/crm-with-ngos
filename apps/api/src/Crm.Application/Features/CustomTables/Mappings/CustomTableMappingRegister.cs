using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.CustomTables.Commands;
using Crm.Application.Dtos.CustomTemplates;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.CustomTables.Mappings;

/// <summary>
/// Registers Mapster mapping rules between <see cref="TableTemplateDto"/> and <see cref="CustomTable"/>.
/// </summary>
public class CustomTableMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CustomTable, CustomTableDto>()
            .Map(dest => dest.Fields, src => src.Fields.OrderBy(f => f.Order));

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
    }
}
