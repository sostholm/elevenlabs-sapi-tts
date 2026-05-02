using System.Collections.Generic;
using Newtonsoft.Json;

namespace ElevenLabsTtsEngine
{
    public sealed class ElevenLabsVoicesResponse
    {
        [JsonProperty("voices")]
        public List<ElevenLabsVoice> Voices { get; set; } = new List<ElevenLabsVoice>();
    }

    public sealed class ElevenLabsVoice
    {
        [JsonProperty("voice_id")]
        public string VoiceId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("labels")]
        public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
    }
}
