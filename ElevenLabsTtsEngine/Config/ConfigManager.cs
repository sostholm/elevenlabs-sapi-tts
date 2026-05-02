using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace ElevenLabsTtsEngine.Config
{
    public static class ConfigManager
    {
        public static string ConfigDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ElevenLabsTTS");

        public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

        public static ElevenLabsConfig LoadOrCreate()
        {
            Directory.CreateDirectory(ConfigDirectory);

            if (!File.Exists(ConfigPath))
            {
                var template = new ElevenLabsConfig();
                Save(template);
                return template;
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonConvert.DeserializeObject<ElevenLabsConfig>(json) ?? new ElevenLabsConfig();
            if (!string.IsNullOrWhiteSpace(config.ApiKeyProtected))
            {
                config.ApiKey = Unprotect(config.ApiKeyProtected);
            }

            if (string.IsNullOrWhiteSpace(config.ModelId))
            {
                config.ModelId = "eleven_turbo_v2_5";
            }

            if (string.IsNullOrWhiteSpace(config.DefaultOutputFormat))
            {
                config.DefaultOutputFormat = "pcm_24000";
            }

            if (config.RequestTimeoutSeconds <= 0)
            {
                config.RequestTimeoutSeconds = 30;
            }

            if (config.MaxCharactersPerRequest <= 0)
            {
                config.MaxCharactersPerRequest = 1200;
            }

            if (config.MaxCharactersPerSpeak <= 0)
            {
                config.MaxCharactersPerSpeak = 3000;
            }

            if (!string.IsNullOrWhiteSpace(config.ApiKey) && string.IsNullOrWhiteSpace(config.ApiKeyProtected))
            {
                Save(config);
            }

            return config;
        }

        public static void Save(ElevenLabsConfig config)
        {
            Directory.CreateDirectory(ConfigDirectory);
            config.ApiKeyProtected = string.IsNullOrWhiteSpace(config.ApiKey) ? null : Protect(config.ApiKey);
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }

        public static void DeleteConfig()
        {
            if (File.Exists(ConfigPath))
            {
                File.Delete(ConfigPath);
            }
        }

        private static string Protect(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static string Unprotect(string protectedValue)
        {
            var protectedBytes = Convert.FromBase64String(protectedValue);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
