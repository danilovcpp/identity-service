using System.Text.Json.Serialization;

namespace Identity.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateProfileRequest
{
    [JsonRequired]
    public string? DisplayName { get; set; }
}
