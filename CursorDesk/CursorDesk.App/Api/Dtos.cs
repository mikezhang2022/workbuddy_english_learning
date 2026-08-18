using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CursorDesk.Api
{
    public sealed class MeResponse
    {
        [JsonProperty("apiKeyName")]
        public string ApiKeyName { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("userEmail")]
        public string UserEmail { get; set; }

        [JsonProperty("userFirstName")]
        public string UserFirstName { get; set; }

        [JsonProperty("userLastName")]
        public string UserLastName { get; set; }

        public string DisplayName()
        {
            var name = ((UserFirstName ?? string.Empty) + " " + (UserLastName ?? string.Empty)).Trim();
            if (name.Length > 0)
            {
                return name;
            }

            if (!string.IsNullOrWhiteSpace(ApiKeyName))
            {
                return ApiKeyName;
            }

            return UserId ?? string.Empty;
        }
    }

    public sealed class ModelInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public sealed class ModelsResponse
    {
        [JsonProperty("items")]
        public List<ModelInfo> Items { get; set; }
    }

    public sealed class PromptBody
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public sealed class ModelSpec
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// Must always be an empty JSON array. Sending param objects yields HTTP 400 invalid_model.
        /// </summary>
        [JsonProperty("params")]
        public object[] Params { get; set; }
    }

    public sealed class AgentCreateRequest
    {
        [JsonProperty("prompt")]
        public PromptBody Prompt { get; set; }

        [JsonProperty("model")]
        public ModelSpec Model { get; set; }

        [JsonProperty("autoCreatePR")]
        public bool AutoCreatePR { get; set; }
    }

    public sealed class AgentCreateResponseWrapper
    {
        /// <summary>
        /// POST /v1/agents returns {"agent": {...}, "run": {...}} — not flat.
        /// This wrapper unpacks the real fields.
        /// </summary>
        [JsonProperty("agent")]
        public AgentCreateResponse Agent { get; set; }

        [JsonProperty("run")]
        public RunRef Run { get; set; }
    }

    public sealed class AgentCreateResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("latestRunId")]
        public string LatestRunId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("latestRun")]
        public RunRef LatestRun { get; set; }

        public string ResolveRunId()
        {
            if (!string.IsNullOrWhiteSpace(LatestRunId))
            {
                return LatestRunId;
            }

            if (LatestRun != null && !string.IsNullOrWhiteSpace(LatestRun.Id))
            {
                return LatestRun.Id;
            }

            return null;
        }
    }

    /// <summary>
    /// GET /v1/agents/{id} — used to check whether a reused agent is still alive.
    /// </summary>
    public sealed class AgentResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        public bool IsReusable()
        {
            if (string.IsNullOrWhiteSpace(Status))
            {
                return false;
            }

            // ACTIVE / CREATING / BUSY are usable; ARCHIVED / EXPIRED are not.
            return !Status.Equals("ARCHIVED", StringComparison.OrdinalIgnoreCase)
                && !Status.Equals("EXPIRED", StringComparison.OrdinalIgnoreCase)
                && !Status.Equals("FAILED", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// POST /v1/agents/{agentId}/runs — creates a follow-up run on a warm agent (no VM cold start).
    /// </summary>
    public sealed class RunCreateRequest
    {
        [JsonProperty("prompt")]
        public PromptBody Prompt { get; set; }

        [JsonProperty("model")]
        public ModelSpec Model { get; set; }
    }

    public sealed class RunCreateResponseWrapper
    {
        [JsonProperty("agent")]
        public AgentRef Agent { get; set; }

        [JsonProperty("run")]
        public RunRef Run { get; set; }
    }

    public sealed class AgentRef
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public sealed class RunRef
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public sealed class RunResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("result")]
        public JToken Result { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        public string GetResultText()
        {
            if (Result == null || Result.Type == JTokenType.Null)
            {
                return string.Empty;
            }

            if (Result.Type == JTokenType.String)
            {
                return Result.Value<string>() ?? string.Empty;
            }

            if (Result.Type == JTokenType.Object)
            {
                var text = Result["text"];
                if (text != null && text.Type != JTokenType.Null)
                {
                    return text.Value<string>() ?? string.Empty;
                }
            }

            return Result.ToString(Formatting.Indented);
        }
    }
}
