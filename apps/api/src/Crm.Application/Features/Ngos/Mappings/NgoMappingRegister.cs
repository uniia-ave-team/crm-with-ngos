using Crm.Application.Dtos.Ngos;
using Crm.Application.Features.Ngos.Commands;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.Ngos.Mappings;

/// <summary>
/// Mapster configuration for NGO entity mappings.
/// </summary>
public sealed class NgoMappingRegister : IRegister
{
    /// <inheritdoc />
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Ngo, NgoDto>()
            .Map(dest => dest.LogoUrl, src => src.Logo);

        config.NewConfig<CreateNgoCommand, Ngo>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Logo)
            .Ignore(dest => dest.Users)
            .Ignore(dest => dest.CustomTables)
            .Ignore(dest => dest.CreatedAt)
            .Ignore(dest => dest.UpdatedAt);

        config.NewConfig<UpdateNgoCommand, Ngo>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Logo)
            .Ignore(dest => dest.Users)
            .Ignore(dest => dest.CustomTables)
            .Ignore(dest => dest.CreatedAt)
            .Ignore(dest => dest.UpdatedAt);
    }
}
