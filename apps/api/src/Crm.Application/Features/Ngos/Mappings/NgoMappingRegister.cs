using Crm.Application.Dtos.Ngo;
using Crm.Application.Dtos.Ngo.Commands;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.Ngos.Mappings;

/// <summary>
/// Mapster configuration for NGO entity mappings.
/// </summary>
public class NgoMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Ngo, NgoDto>()
            .Map(dest => dest.LogoUrl, src => src.Logo);

        config.NewConfig<CreateNgoCommand, Ngo>()
            .Map(dest => dest.Logo, src => src.LogoUrl)
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Users)
            .Ignore(dest => dest.CreatedAt);

        config.NewConfig<UpdateNgoCommand, Ngo>()
            .Map(dest => dest.Logo, src => src.LogoUrl)
            .IgnoreNullValues(true)
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Users)
            .Ignore(dest => dest.CreatedAt);
    }
}
