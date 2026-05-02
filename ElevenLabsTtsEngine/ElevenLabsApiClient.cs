using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ElevenLabsTtsEngine
{
    public sealed class ElevenLabsApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public ElevenLabsApiClient(int timeoutSeconds = 30)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.elevenlabs.io/"),
                Timeout = TimeSpan.FromSeconds(Math.Max(5, Math.Min(timeoutSeconds, 120)))
            };
        }

        public async Task<Stream> SynthesizeStreamAsync(string text, string voiceId, string apiKey, string modelId, string outputFormat, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new COMException("ElevenLabs API key is not configured.", unchecked((int)0x80004005));
            }

            if (string.IsNullOrWhiteSpace(voiceId))
            {
                throw new COMException("ElevenLabs voice id is not configured.", unchecked((int)0x80004005));
            }

            var requestUri = $"v1/text-to-speech/{Uri.EscapeDataString(voiceId)}/stream?output_format={Uri.EscapeDataString(outputFormat ?? "pcm_24000")}";
            var payload = new
            {
                text,
                model_id = string.IsNullOrWhiteSpace(modelId) ? "eleven_turbo_v2_5" : modelId,
                voice_settings = new
                {
                    stability = 0.5,
                    similarity_boost = 0.75
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("xi-api-key", apiKey);
            request.Headers.Accept.ParseAdd("audio/*");

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await ReadErrorBodyAsync(response).ConfigureAwait(false);
                response.Dispose();
                throw CreateApiException(response.StatusCode, detail);
            }

            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return new ResponseStream(response, stream);
        }

        public async Task<IReadOnlyList<ElevenLabsVoice>> GetVoicesAsync(string apiKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("ElevenLabs API key is not configured.");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, "v1/voices");
            request.Headers.TryAddWithoutValidation("xi-api-key", apiKey);
            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateApiException(response.StatusCode, json);
            }

            var result = JsonConvert.DeserializeObject<ElevenLabsVoicesResponse>(json);
            return result?.Voices ?? new List<ElevenLabsVoice>();
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        private static async Task<string> ReadErrorBodyAsync(HttpResponseMessage response)
        {
            try
            {
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch
            {
                return response.ReasonPhrase;
            }
        }

        private static Exception CreateApiException(HttpStatusCode statusCode, string detail)
        {
            var message = $"ElevenLabs API returned {(int)statusCode} {statusCode}. {SanitizeErrorDetail(detail)}";
            return new COMException(message, unchecked((int)0x80004005));
        }

        private static string SanitizeErrorDetail(string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                return string.Empty;
            }

            detail = detail.Replace("\r", " ").Replace("\n", " ");
            return detail.Length > 500 ? detail.Substring(0, 500) : detail;
        }

        private sealed class ResponseStream : Stream
        {
            private readonly HttpResponseMessage _response;
            private readonly Stream _inner;

            public ResponseStream(HttpResponseMessage response, Stream inner)
            {
                _response = response;
                _inner = inner;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _inner.Dispose();
                    _response.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
