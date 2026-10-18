using System.Text.Json;
using System.Text.Json.Serialization;

namespace Identity.Infrastructure.Persistence.Configurations;

internal static class JsonConfiguration
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
