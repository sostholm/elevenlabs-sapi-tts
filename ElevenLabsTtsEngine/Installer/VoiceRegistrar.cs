using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ElevenLabsTtsEngine.Config;
using ElevenLabsTtsEngine.Interop;
using Microsoft.Win32;

namespace ElevenLabsTtsEngine.Installer
{
    public sealed class VoiceRegistrar
    {
        private const string TokensPath = @"SOFTWARE\Microsoft\Speech\Voices\Tokens";
        private const string TokenPrefix = "ElevenLabs_";

        public void Refresh(ElevenLabsConfig config)
        {
            using (var api = new ElevenLabsApiClient())
            {
                var voices = api.GetVoicesAsync(config.ApiKey, CancellationToken.None).GetAwaiter().GetResult();
                foreach (var view in GetRegistryViews())
                {
                    RefreshView(view, voices);
                }
            }
        }

        public void RegisterManualVoice(string voiceId, string name, string gender = "Neutral", string age = "Adult")
        {
            if (string.IsNullOrWhiteSpace(voiceId))
            {
                throw new ArgumentException("Voice id is required.", nameof(voiceId));
            }

            var voice = new ElevenLabsVoice
            {
                VoiceId = voiceId,
                Name = string.IsNullOrWhiteSpace(name) ? voiceId : name,
                Labels = new Dictionary<string, string>
                {
                    ["gender"] = string.IsNullOrWhiteSpace(gender) ? "Neutral" : gender,
                    ["age"] = string.IsNullOrWhiteSpace(age) ? "Adult" : age
                }
            };

            foreach (var view in GetRegistryViews())
            {
                using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var tokens = root.CreateSubKey(TokensPath))
                {
                    if (tokens == null)
                    {
                        throw new InvalidOperationException("Unable to open SAPI voice token registry path.");
                    }

                    WriteToken(tokens, TokenPrefix + SanitizeTokenPart(voiceId), voice);
                }
            }
        }

        public void UnregisterAll()
        {
            foreach (var view in GetRegistryViews())
            {
                using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var tokens = root.OpenSubKey(TokensPath, true))
                {
                    if (tokens == null)
                    {
                        continue;
                    }

                    foreach (var name in tokens.GetSubKeyNames().Where(n => n.StartsWith(TokenPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
                    {
                        tokens.DeleteSubKeyTree(name, false);
                    }
                }
            }
        }

        private static void RefreshView(RegistryView view, IReadOnlyList<ElevenLabsVoice> voices)
        {
            using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            using (var tokens = root.CreateSubKey(TokensPath))
            {
                if (tokens == null)
                {
                    throw new InvalidOperationException("Unable to open SAPI voice token registry path.");
                }

                var desiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var voice in voices.Where(v => !string.IsNullOrWhiteSpace(v.VoiceId)))
                {
                    var tokenName = TokenPrefix + SanitizeTokenPart(voice.VoiceId);
                    desiredNames.Add(tokenName);
                    WriteToken(tokens, tokenName, voice);
                }

                foreach (var existing in tokens.GetSubKeyNames().Where(n => n.StartsWith(TokenPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
                {
                    if (!desiredNames.Contains(existing))
                    {
                        tokens.DeleteSubKeyTree(existing, false);
                    }
                }
            }
        }

        private static void WriteToken(RegistryKey tokens, string tokenName, ElevenLabsVoice voice)
        {
            using (var token = tokens.CreateSubKey(tokenName))
            {
                if (token == null)
                {
                    throw new InvalidOperationException("Unable to create SAPI voice token.");
                }

                var displayName = "ElevenLabs - " + (string.IsNullOrWhiteSpace(voice.Name) ? voice.VoiceId : voice.Name);
                token.SetValue("", displayName, RegistryValueKind.String);
                token.SetValue("409", displayName, RegistryValueKind.String);
                token.SetValue("CLSID", SapiConstants.EngineClassId.ToString("B").ToUpperInvariant(), RegistryValueKind.String);
                token.SetValue("ElevenLabsVoiceId", voice.VoiceId, RegistryValueKind.String);

                using (var attributes = token.CreateSubKey("Attributes"))
                {
                    if (attributes == null)
                    {
                        throw new InvalidOperationException("Unable to create SAPI voice token attributes.");
                    }

                    attributes.SetValue("Name", displayName, RegistryValueKind.String);
                    attributes.SetValue("Vendor", "ElevenLabs", RegistryValueKind.String);
                    attributes.SetValue("Language", "409", RegistryValueKind.String);
                    attributes.SetValue("Gender", NormalizeGender(GetLabel(voice, "gender", "Neutral")), RegistryValueKind.String);
                    attributes.SetValue("Age", NormalizeAge(GetLabel(voice, "age", "Adult")), RegistryValueKind.String);
                }
            }
        }

        private static IEnumerable<RegistryView> GetRegistryViews()
        {
            yield return RegistryView.Registry64;
            yield return RegistryView.Registry32;
        }

        private static string SanitizeTokenPart(string value)
        {
            var chars = value.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray();
            return new string(chars);
        }

        private static string GetLabel(ElevenLabsVoice voice, string key, string fallback)
        {
            string value;
            if (voice.Labels != null && voice.Labels.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return fallback;
        }

        private static string NormalizeGender(string value)
        {
            if (string.Equals(value, "male", StringComparison.OrdinalIgnoreCase))
            {
                return "Male";
            }

            if (string.Equals(value, "female", StringComparison.OrdinalIgnoreCase))
            {
                return "Female";
            }

            return "Neutral";
        }

        private static string NormalizeAge(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Adult";
            }

            var lower = value.Trim().ToLowerInvariant();
            if (lower == "baby" || lower == "toddler" || lower == "child" || lower == "teen" || lower == "adult" || lower == "senior")
            {
                return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
            }

            if (lower == "young")
            {
                return "Adult";
            }

            if (lower == "old" || lower == "elderly")
            {
                return "Senior";
            }

            return "Adult";
        }
    }
}
