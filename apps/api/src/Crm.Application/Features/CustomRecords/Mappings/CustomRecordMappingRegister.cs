using System.Text.Json.Nodes;
using Crm.Application.Dtos.CustomRecords;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.CustomRecords.Mappings;

/// <summary>
/// Registers Mapster mapping rules for <see cref="CustomRecord"/> and <see cref="JsonObject"/> payloads.
/// </summary>
public class CustomRecordMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<JsonObject, JsonObject>()
            .MapWith(src => src == null ? null : (JsonObject)src.DeepClone());

        config.NewConfig<JsonNode, JsonNode>()
            .MapWith(src => src == null ? null : src.DeepClone());

        config.NewConfig<CustomRecord, CustomRecordDto>();
    }
}
