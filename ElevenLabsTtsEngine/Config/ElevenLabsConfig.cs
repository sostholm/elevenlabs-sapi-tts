using Newtonsoft.Json;

namespace ElevenLabsTtsEngine.Config
{
    public sealed class ElevenLabsConfig
    {
        [JsonIgnore]
        public string ApiKey { get; set; }

        [JsonProperty("apiKeyProtected")]
        public string ApiKeyProtected { get; set; }

        [JsonProperty("apiKey")]
        private string PlaintextApiKeyMigration
        {
            set { ApiKey = value; }
        }

        [JsonProperty("modelId")]
        public string ModelId { get; set; } = "eleven_turbo_v2_5";

        [JsonProperty("defaultOutputFormat")]
        public string DefaultOutputFormat { get; set; } = "pcm_24000";

        [JsonProperty("requestTimeoutSeconds")]
        public int RequestTimeoutSeconds { get; set; } = 30;

        [JsonProperty("maxCharactersPerRequest")]
        public int MaxCharactersPerRequest { get; set; } = 1200;

        [JsonProperty("maxCharactersPerSpeak")]
        public int MaxCharactersPerSpeak { get; set; } = 3000;
    }
}
