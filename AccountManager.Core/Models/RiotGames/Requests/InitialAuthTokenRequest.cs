using AccountManager.Core.Static;
using System.Text.Json.Serialization;

namespace AccountManager.Core.Models.RiotGames.Requests
{
    public class RiotSessionRequest
    {
        [JsonPropertyName("client_id")]
        public string? Id { get; set; }
        [JsonPropertyName("nonce")]
        public string? Nonce { get; set; }
        [JsonPropertyName("redirect_uri")]
        public string? RedirectUri { get; set; }
        [JsonPropertyName("response_type")]
        public string? ResponseType { get; set; }
        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
        public string GetHashId()
        {
            return StringEncryption.Hash($"{Id}.{ResponseType}.{Scope}");
        }
    }
}
