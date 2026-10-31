using Crm.Application.Dtos.CustomTemplates;
using Crm.Application.Dtos.CustomTemplates.Commands;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.CustomTemplates.Mappings;

/// <summary>
/// Mapster configuration for TableTemplate and TemplateField entity mappings.
/// </summary>
public class TableTemplateMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<TableTemplate, TableTemplateDto>()
            .Map(dest => dest.Fields, src => src.Fields.OrderBy(f => f.Order));

        config.NewConfig<UpdateTableTemplateCommand, TableTemplate>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Fields);

        config.NewConfig<CreateTableTemplateCommand, TableTemplate>()
            .Ignore(dest => dest.Id);

        config.NewConfig<CreateTemplateFieldDto, TemplateField>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.TemplateId)
            .Ignore(dest => dest.Template);

        config.NewConfig<UpdateTemplateFieldDto, TemplateField>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.TemplateId)
            .Ignore(dest => dest.Template);
    }
}
