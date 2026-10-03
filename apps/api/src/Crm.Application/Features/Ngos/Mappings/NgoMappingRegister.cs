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
        config.NewConfig<Ngo, NgoDto>();

        config.NewConfig<UpdateNgoCommand, Ngo>()
            .IgnoreNullValues(true)
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Users)
            .Ignore(dest => dest.CreatedAt);
    }
}
