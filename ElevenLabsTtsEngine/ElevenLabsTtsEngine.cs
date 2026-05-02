using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ElevenLabsTtsEngine.Config;
using ElevenLabsTtsEngine.Interop;

namespace ElevenLabsTtsEngine
{
    [ComVisible(true)]
    [Guid("961AE368-9B90-4277-B66B-D1593B74A888")]
    [ProgId("ElevenLabsTtsEngine.Engine")]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class ElevenLabsTtsEngine : ISpTTSEngine, ISpObjectWithToken
    {
        private ISpObjectToken _objectToken;
        private string _voiceId;
        private ElevenLabsConfig _config;
        private static readonly bool EnableTraceLogging = false;
        private static readonly object WowSplitLock = new object();
        private static string _lastWowSplitSpeaker;
        private static DateTimeOffset _lastWowSplitTime;
        private static bool _lastWowTextEndedWithSplitMarker;
        private static readonly TimeSpan WowSplitContinuationWindow = TimeSpan.FromSeconds(10);
        private static readonly Regex WowChatPrefixRegex = new Regex(
            @"^\s*(?<time>\d{1,2}:\d{2}:\d{2})\s+(?<speaker>\S+)\s+(?<body>.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public int SetObjectToken(ISpObjectToken pToken)
        {
            Trace("SetObjectToken entered.");
            if (pToken == null)
            {
                Trace("SetObjectToken received null token.");
                return SapiConstants.E_POINTER;
            }

            _objectToken = pToken;
            _voiceId = ReadTokenString(pToken, "ElevenLabsVoiceId");
            _config = ConfigManager.LoadOrCreate();
            Trace("SetObjectToken voiceId=" + (_voiceId ?? "<null>"));
            return SapiConstants.S_OK;
        }

        public int GetObjectToken(out ISpObjectToken ppToken)
        {
            ppToken = _objectToken;
            return ppToken == null ? SapiConstants.E_FAIL : SapiConstants.S_OK;
        }

        public int GetOutputFormat(IntPtr pTargetFmtId, IntPtr pTargetWaveFormatEx, out Guid pOutputFormatId, out IntPtr ppCoMemOutputWaveFormatEx)
        {
            Trace("GetOutputFormat entered.");
            pOutputFormatId = SapiConstants.SPDFID_WaveFormatEx;
            var format = WAVEFORMATEX.Pcm24Khz16BitMono();

            ppCoMemOutputWaveFormatEx = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(WAVEFORMATEX)));
            Marshal.StructureToPtr(format, ppCoMemOutputWaveFormatEx, false);
            return SapiConstants.S_OK;
        }

        public int Speak(uint dwSpeakFlags, ref Guid rguidFormatId, IntPtr pWaveFormatEx, IntPtr pTextFragList, ISpTTSEngineSite pOutputSite)
        {
            Trace("Speak entered. pTextFragList=" + pTextFragList);
            if (pOutputSite == null || pTextFragList == IntPtr.Zero)
            {
                Trace("Speak received null site or text fragment list.");
                return SapiConstants.E_POINTER;
            }

            try
            {
                var config = _config ?? ConfigManager.LoadOrCreate();
                var segments = ReadSegments(pTextFragList);
                if (IsWowProcess())
                {
                    segments = NormalizeWowSplitText(segments);
                }

                segments = ApplyCharacterLimits(segments, config);
                Trace("Speak segments=" + segments.Count);
                using (var cancellation = new CancellationTokenSource())
                using (var api = new ElevenLabsApiClient(config.RequestTimeoutSeconds))
                {
                    foreach (var segment in segments)
                    {
                        if (ShouldAbort(pOutputSite, cancellation))
                        {
                            return SapiConstants.S_OK;
                        }

                        if (segment.IsSilence)
                        {
                            var silenceResult = WriteSilence(pOutputSite, segment.SilenceMSecs, cancellation);
                            if (silenceResult != SapiConstants.S_OK)
                            {
                                return silenceResult;
                            }

                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(segment.Text))
                        {
                            Trace("Speak skipping empty text segment.");
                            continue;
                        }

                        Trace("Speak synthesizing chars=" + segment.Text.Length + " voiceId=" + (_voiceId ?? "<null>"));
                        using (var stream = api.SynthesizeStreamAsync(segment.Text, _voiceId, config.ApiKey, config.ModelId, config.DefaultOutputFormat, cancellation.Token).GetAwaiter().GetResult())
                        {
                            var writeResult = WriteStream(pOutputSite, stream, cancellation);
                            if (writeResult != SapiConstants.S_OK)
                            {
                                return writeResult;
                            }
                        }
                    }
                }

                return SapiConstants.S_OK;
            }
            catch (OperationCanceledException)
            {
                return SapiConstants.S_OK;
            }
            catch (Exception ex)
            {
                LogError(ex);
                return SapiConstants.E_FAIL;
            }
        }

        private static IReadOnlyList<SpeechSegment> ReadSegments(IntPtr pTextFragList)
        {
            var segments = new List<SpeechSegment>();
            var textBuilder = new StringBuilder();
            var current = pTextFragList;

            while (current != IntPtr.Zero)
            {
                var fragment = (SPVTEXTFRAG)Marshal.PtrToStructure(current, typeof(SPVTEXTFRAG));
                Trace("Fragment action=" + fragment.State.eAction + " textLen=" + fragment.ulTextLen + " silenceMs=" + fragment.State.SilenceMSecs + " textPtr=" + fragment.pTextStart + " next=" + fragment.pNext);
                switch (fragment.State.eAction)
                {
                    case SPVACTIONS.SPVA_Silence:
                        Trace("Fragment treated as silence.");
                        FlushText(segments, textBuilder);
                        if (fragment.State.SilenceMSecs > 0)
                        {
                            segments.Add(SpeechSegment.Silence(fragment.State.SilenceMSecs));
                        }
                        break;

                    case SPVACTIONS.SPVA_Bookmark:
                        FlushText(segments, textBuilder);
                        break;

                    case SPVACTIONS.SPVA_Speak:
                    case SPVACTIONS.SPVA_SpellOut:
                    case SPVACTIONS.SPVA_Pronounce:
                    default:
                        if (fragment.pTextStart != IntPtr.Zero && fragment.ulTextLen > 0)
                        {
                            var text = Marshal.PtrToStringUni(fragment.pTextStart, checked((int)fragment.ulTextLen));
                            Trace("Fragment text preview=" + ((text ?? "").Length > 80 ? text.Substring(0, 80) : text));
                            textBuilder.Append(text);
                        }
                        break;
                }

                current = fragment.pNext;
            }

            FlushText(segments, textBuilder);
            return segments;
        }

        private static void FlushText(List<SpeechSegment> segments, StringBuilder textBuilder)
        {
            if (textBuilder.Length == 0)
            {
                return;
            }

            segments.Add(SpeechSegment.TextSegment(textBuilder.ToString()));
            textBuilder.Length = 0;
        }

        private static IReadOnlyList<SpeechSegment> ApplyCharacterLimits(IReadOnlyList<SpeechSegment> segments, ElevenLabsConfig config)
        {
            var maxPerRequest = Math.Max(1, Math.Min(config.MaxCharactersPerRequest, 5000));
            var maxPerSpeak = Math.Max(1, Math.Min(config.MaxCharactersPerSpeak, 20000));
            var limited = new List<SpeechSegment>();
            var remaining = maxPerSpeak;

            foreach (var segment in segments)
            {
                if (segment.IsSilence)
                {
                    limited.Add(segment);
                    continue;
                }

                var text = segment.Text ?? string.Empty;
                if (text.Length == 0 || remaining <= 0)
                {
                    continue;
                }

                var allowed = Math.Min(text.Length, remaining);
                for (var offset = 0; offset < allowed; offset += maxPerRequest)
                {
                    var count = Math.Min(maxPerRequest, allowed - offset);
                    limited.Add(SpeechSegment.TextSegment(text.Substring(offset, count)));
                }

                remaining -= allowed;
            }

            return limited;
        }

        private static IReadOnlyList<SpeechSegment> NormalizeWowSplitText(IReadOnlyList<SpeechSegment> segments)
        {
            var normalized = new List<SpeechSegment>();

            foreach (var segment in segments)
            {
                if (segment.IsSilence)
                {
                    normalized.Add(segment);
                    continue;
                }

                var text = NormalizeWowSplitText(segment.Text);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    normalized.Add(SpeechSegment.TextSegment(text));
                }
            }

            return normalized;
        }

        private static string NormalizeWowSplitText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var parsed = ParseWowChatLine(text);
            var now = DateTimeOffset.UtcNow;

            lock (WowSplitLock)
            {
                if (parsed.IsContinuation &&
                    _lastWowTextEndedWithSplitMarker &&
                    string.Equals(parsed.Speaker, _lastWowSplitSpeaker, StringComparison.OrdinalIgnoreCase) &&
                    now - _lastWowSplitTime <= WowSplitContinuationWindow)
                {
                    UpdateWowSplitState(parsed.Speaker, parsed.EndsWithSplitMarker, now);
                    return CleanSplitText(parsed.BodyWithoutContinuationMarker);
                }

                var cleaned = CleanSplitText(text);
                if (parsed.HasChatPrefix)
                {
                    UpdateWowSplitState(parsed.Speaker, parsed.EndsWithSplitMarker, now);
                }
                else
                {
                    UpdateWowSplitState(null, EndsWithSplitMarker(text), now);
                }

                return cleaned;
            }
        }

        private static WowChatLine ParseWowChatLine(string text)
        {
            var result = new WowChatLine
            {
                BodyWithoutContinuationMarker = text,
                EndsWithSplitMarker = EndsWithSplitMarker(text)
            };

            var match = WowChatPrefixRegex.Match(text);
            if (!match.Success)
            {
                return result;
            }

            var body = match.Groups["body"].Value;
            result.HasChatPrefix = true;
            result.Speaker = match.Groups["speaker"].Value;
            result.EndsWithSplitMarker = EndsWithSplitMarker(body);

            var trimmedBody = body.TrimStart();
            if (StartsWithSplitMarker(trimmedBody))
            {
                result.IsContinuation = true;
                result.BodyWithoutContinuationMarker = trimmedBody.Substring(1).TrimStart();
            }
            else
            {
                result.BodyWithoutContinuationMarker = body;
            }

            return result;
        }

        private static string CleanSplitText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            text = text.Trim();
            if (EndsWithSplitMarker(text))
            {
                text = text.Substring(0, text.Length - 1).TrimEnd();
            }

            return text;
        }

        private static bool StartsWithSplitMarker(string text)
        {
            return !string.IsNullOrEmpty(text) && text[0] == '\u00bb';
        }

        private static bool EndsWithSplitMarker(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && text.TrimEnd().EndsWith("\u00bb", StringComparison.Ordinal);
        }

        private static void UpdateWowSplitState(string speaker, bool endedWithSplitMarker, DateTimeOffset now)
        {
            _lastWowSplitSpeaker = speaker;
            _lastWowTextEndedWithSplitMarker = endedWithSplitMarker;
            _lastWowSplitTime = now;
        }

        private static bool IsWowProcess()
        {
            try
            {
                return string.Equals(Process.GetCurrentProcess().ProcessName, "Wow", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static int WriteStream(ISpTTSEngineSite site, Stream stream, CancellationTokenSource cancellation)
        {
            var buffer = new byte[4096];
            var pending = new byte[1];
            var pendingCount = 0;
            while (true)
            {
                if (ShouldAbort(site, cancellation))
                {
                    return SapiConstants.S_OK;
                }

                var read = stream.ReadAsync(buffer, 0, buffer.Length, cancellation.Token).GetAwaiter().GetResult();
                Trace("Stream read bytes=" + read);
                if (read == 0)
                {
                    return SapiConstants.S_OK;
                }

                byte[] outputBuffer;
                var outputCount = read + pendingCount;
                if (pendingCount > 0)
                {
                    outputBuffer = new byte[outputCount];
                    outputBuffer[0] = pending[0];
                    Buffer.BlockCopy(buffer, 0, outputBuffer, 1, read);
                }
                else
                {
                    outputBuffer = buffer;
                }

                if ((outputCount & 1) != 0)
                {
                    pending[0] = outputBuffer[outputCount - 1];
                    outputCount--;
                    pendingCount = 1;
                }
                else
                {
                    pendingCount = 0;
                }

                if (outputCount == 0)
                {
                    continue;
                }

                var volume = GetVolume(site);
                var writeBuffer = volume < 100 ? ScalePcm16(outputBuffer, outputCount, volume) : outputBuffer;
                var hr = WriteBytes(site, writeBuffer, outputCount);
                if (hr != SapiConstants.S_OK)
                {
                    return hr;
                }
            }
        }

        private static int WriteSilence(ISpTTSEngineSite site, uint silenceMSecs, CancellationTokenSource cancellation)
        {
            var bytesRemaining = checked((int)(silenceMSecs * 24000u * 2u / 1000u));
            var buffer = new byte[4096];
            while (bytesRemaining > 0)
            {
                if (ShouldAbort(site, cancellation))
                {
                    return SapiConstants.S_OK;
                }

                var count = Math.Min(buffer.Length, bytesRemaining);
                var hr = WriteBytes(site, buffer, count);
                if (hr != SapiConstants.S_OK)
                {
                    return hr;
                }

                bytesRemaining -= count;
            }

            return SapiConstants.S_OK;
        }

        private static int WriteBytes(ISpTTSEngineSite site, byte[] buffer, int count)
        {
            var unmanaged = Marshal.AllocHGlobal(count);
            try
            {
                Marshal.Copy(buffer, 0, unmanaged, count);
                uint written;
                var hr = site.Write(unmanaged, checked((uint)count), out written);
                Trace("Write bytes=" + count + " hr=0x" + hr.ToString("X8") + " written=" + written);
                return hr == SapiConstants.S_OK ? SapiConstants.S_OK : hr;
            }
            finally
            {
                Marshal.FreeHGlobal(unmanaged);
            }
        }

        private static bool ShouldAbort(ISpTTSEngineSite site, CancellationTokenSource cancellation)
        {
            var actions = site.GetActions();
            Trace("GetActions=" + actions);
            if ((actions & SPVESACTIONS.SPVES_ABORT) == SPVESACTIONS.SPVES_ABORT)
            {
                cancellation.Cancel();
                return true;
            }

            return false;
        }

        private static ushort GetVolume(ISpTTSEngineSite site)
        {
            ushort volume;
            return site.GetVolume(out volume) == SapiConstants.S_OK ? volume : (ushort)100;
        }

        private static byte[] ScalePcm16(byte[] buffer, int count, ushort volume)
        {
            var scaled = new byte[count];
            Buffer.BlockCopy(buffer, 0, scaled, 0, count);

            var factor = Math.Max(0, Math.Min(100, (int)volume)) / 100.0;
            for (var i = 0; i + 1 < count; i += 2)
            {
                var sample = (short)(scaled[i] | (scaled[i + 1] << 8));
                var value = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, sample * factor));
                scaled[i] = (byte)(value & 0xff);
                scaled[i + 1] = (byte)((value >> 8) & 0xff);
            }

            return scaled;
        }

        private static string ReadTokenString(ISpObjectToken token, string valueName)
        {
            IntPtr value;
            var hr = token.GetStringValue(valueName, out value);
            if (hr != SapiConstants.S_OK || value == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(value);
            }
            finally
            {
                Marshal.FreeCoTaskMem(value);
            }
        }

        private static void LogError(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(ConfigManager.ConfigDirectory);
                File.AppendAllText(
                    Path.Combine(ConfigManager.ConfigDirectory, "engine.log"),
                    $"{DateTimeOffset.Now:u} {ex}\r\n");
            }
            catch
            {
            }
        }

        private static void Trace(string message)
        {
            if (!EnableTraceLogging)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(ConfigManager.ConfigDirectory);
                File.AppendAllText(
                    Path.Combine(ConfigManager.ConfigDirectory, "trace.log"),
                    $"{DateTimeOffset.Now:u} {message}\r\n");
            }
            catch
            {
            }
        }

        private sealed class SpeechSegment
        {
            private SpeechSegment()
            {
            }

            public bool IsSilence { get; private set; }
            public string Text { get; private set; }
            public uint SilenceMSecs { get; private set; }

            public static SpeechSegment TextSegment(string text)
            {
                return new SpeechSegment { Text = text };
            }

            public static SpeechSegment Silence(uint milliseconds)
            {
                return new SpeechSegment { IsSilence = true, SilenceMSecs = milliseconds };
            }
        }

        private struct WowChatLine
        {
            public bool HasChatPrefix;
            public bool IsContinuation;
            public bool EndsWithSplitMarker;
            public string Speaker;
            public string BodyWithoutContinuationMarker;
        }
    }
}
