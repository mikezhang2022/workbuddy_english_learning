using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CursorDesk.Api
{
    public sealed class CursorApiClient : IDisposable
    {
        public const string DefaultModelId = "default";
        public const string BaseUrl = "https://api.cursor.com/";

        private const int PollDelayMs = 2500;
        private const int MaxPollAttempts = 240;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly HttpClient _http;

        public CursorApiClient(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key is required.", nameof(apiKey));
            }

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            // HttpClient timeout: POST /v1/agents can take >60s when server is busy.
            // 180s gives enough margin; polling (PollUntilFinished) has its own 10min ceiling.
            _http = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(180)
            };

            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey + ":"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("CursorDesk/1.0");
        }

        public Task<MeResponse> GetMeAsync(CancellationToken cancellationToken)
        {
            return SendAsync<MeResponse>(HttpMethod.Get, "v1/me", null, HttpStatusCode.OK, cancellationToken);
        }

        public async Task<IList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken)
        {
            var response = await SendAsync<ModelsResponse>(
                HttpMethod.Get,
                "v1/models",
                null,
                HttpStatusCode.OK,
                cancellationToken).ConfigureAwait(false);

            if (response == null || response.Items == null)
            {
                return new List<ModelInfo>();
            }

            return response.Items;
        }

        public async Task<AgentCreateResponse> CreateAgentAsync(
            string promptText,
            string modelId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(promptText))
            {
                throw new ArgumentException("Prompt is required.", nameof(promptText));
            }

            if (string.IsNullOrWhiteSpace(modelId))
            {
                modelId = DefaultModelId;
            }

            // Q&A only: no repos field. model.params MUST be [] — never param objects.
            var body = new AgentCreateRequest
            {
                Prompt = new PromptBody { Text = promptText },
                Model = new ModelSpec
                {
                    Id = modelId,
                    Params = new object[0]
                },
                AutoCreatePR = true
            };

            var json = JsonConvert.SerializeObject(body, JsonSettings);
            var raw = await SendRawAsync(
                HttpMethod.Post,
                "v1/agents",
                json,
                HttpStatusCode.Created,
                cancellationToken).ConfigureAwait(false);

            // API returns {"agent": {...}, "run": {...}} — unwrap.
            var wrapped = JsonConvert.DeserializeObject<AgentCreateResponseWrapper>(raw, JsonSettings);
            if (wrapped == null || wrapped.Agent == null)
            {
                throw new CursorApiException("Create agent returned unexpected response.");
            }

            // Merge run id from the top-level "run" field if agent lacks it.
            if (string.IsNullOrWhiteSpace(wrapped.Agent.LatestRunId) && wrapped.Run != null)
            {
                wrapped.Agent.LatestRunId = wrapped.Run.Id;
                if (wrapped.Agent.LatestRun == null && !string.IsNullOrWhiteSpace(wrapped.Run.Id))
                {
                    wrapped.Agent.LatestRun = new RunRef { Id = wrapped.Run.Id };
                }
            }

            return wrapped.Agent;
        }

        public Task<RunResponse> GetRunAsync(string agentId, string runId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                throw new ArgumentException("Agent id is required.", nameof(agentId));
            }

            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new ArgumentException("Run id is required.", nameof(runId));
            }

            var path = "v1/agents/" + Uri.EscapeDataString(agentId) + "/runs/" + Uri.EscapeDataString(runId);
            return SendAsync<RunResponse>(HttpMethod.Get, path, null, HttpStatusCode.OK, cancellationToken);
        }

        public async Task<RunResponse> PollUntilFinishedAsync(
            string agentId,
            string runId,
            CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < MaxPollAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var run = await GetRunAsync(agentId, runId, cancellationToken).ConfigureAwait(false);
                if (run == null)
                {
                    throw new CursorApiException("Empty run response.");
                }

                var status = (run.Status ?? string.Empty).Trim();
                if (string.Equals(status, "FINISHED", StringComparison.OrdinalIgnoreCase))
                {
                    return run;
                }

                if (IsTerminalFailure(status))
                {
                    var detail = run.GetResultText();
                    var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : " — " + detail;
                    throw new CursorApiException("Agent run failed: " + status + suffix);
                }

                await Task.Delay(PollDelayMs, cancellationToken).ConfigureAwait(false);
            }

            throw new CursorApiException("Timed out waiting for the agent run to finish.");
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        private static bool IsTerminalFailure(string status)
        {
            return string.Equals(status, "ERROR", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "CANCELED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "EXPIRED", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<T> SendAsync<T>(
            HttpMethod method,
            string relativePath,
            string jsonBody,
            HttpStatusCode expectedStatus,
            CancellationToken cancellationToken)
        {
            var body = await SendRawAsync(method, relativePath, jsonBody, expectedStatus, cancellationToken).ConfigureAwait(false);
            return Deserialize<T>(body);
        }

        /// <summary>
        /// Sends an HTTP request and returns the raw response body as a string.
        /// Used when the caller needs to do custom deserialization (e.g. wrapped responses).
        /// </summary>
        private async Task<string> SendRawAsync(
            HttpMethod method,
            string relativePath,
            string jsonBody,
            HttpStatusCode expectedStatus,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            try
            {
                using (var request = new HttpRequestMessage(method, relativePath))
                {
                    if (jsonBody != null)
                    {
                        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    }

                    response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (HttpRequestException ex)
            {
                throw new CursorApiException("Network error: " + ex.Message, ex);
            }
            catch (TaskCanceledException ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                throw new CursorApiException("Request timed out.", ex);
            }

            using (response)
            {
                var body = await ReadBodyAsync(response).ConfigureAwait(false);
                ThrowIfFailed(response, body, expectedStatus);
                return body;
            }
        }

        private static async Task<string> ReadBodyAsync(HttpResponseMessage response)
        {
            if (response.Content == null)
            {
                return string.Empty;
            }

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private static void ThrowIfFailed(HttpResponseMessage response, string body, HttpStatusCode expectedStatus)
        {
            var code = (int)response.StatusCode;
            if (code == 401)
            {
                throw CursorApiException.InvalidKey();
            }

            if (code == 429)
            {
                throw CursorApiException.RateLimited(ReadRetryAfterSeconds(response));
            }

            if (response.StatusCode == expectedStatus)
            {
                return;
            }

            // POST /v1/agents is documented as 201; accept 200 as well.
            if (expectedStatus == HttpStatusCode.Created && response.StatusCode == HttpStatusCode.OK)
            {
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new CursorApiException("HTTP " + code + ": " + TrimBody(body), code);
            }
        }

        private static int ReadRetryAfterSeconds(HttpResponseMessage response)
        {
            var header = response.Headers.RetryAfter;
            if (header != null)
            {
                if (header.Delta.HasValue)
                {
                    return Math.Max(1, (int)Math.Ceiling(header.Delta.Value.TotalSeconds));
                }

                if (header.Date.HasValue)
                {
                    var seconds = (header.Date.Value - DateTimeOffset.UtcNow).TotalSeconds;
                    return Math.Max(1, (int)Math.Ceiling(seconds));
                }
            }

            IEnumerable<string> rawValues;
            if (response.Headers.TryGetValues("Retry-After", out rawValues))
            {
                foreach (var raw in rawValues)
                {
                    int parsed;
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                    {
                        return Math.Max(1, parsed);
                    }
                }
            }

            return 60;
        }

        private static T Deserialize<T>(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new CursorApiException("Empty JSON from API.");
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(body);
            }
            catch (JsonException ex)
            {
                throw new CursorApiException("Invalid JSON from API.", ex);
            }
        }

        private static string TrimBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "(no body)";
            }

            body = body.Replace("\r", " ").Replace("\n", " ").Trim();
            if (body.Length > 300)
            {
                return body.Substring(0, 300) + "...";
            }

            return body;
        }
    }
}
