#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.Proposal
{
    internal sealed class OpenAiProposalDialogueProvider : IProposalDialogueProvider
    {
        private const string Model = "gpt-6-sol";
        private static readonly HttpClient Client = new HttpClient();
        private readonly string _key;
        private readonly TimeSpan _timeout;
        private int _hasReportedError;

        private OpenAiProposalDialogueProvider(string key, float timeoutSeconds)
        {
            _key = key;
            _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        public static IProposalDialogueProvider Create(float timeoutSeconds)
        {
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), ".openai-api-key");
                if (!File.Exists(path)) { return null; }
                string key = File.ReadAllText(path).Trim();
                return key.Length > 0 ? new OpenAiProposalDialogueProvider(key, timeoutSeconds) : null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning("OpenAI 키 파일을 읽을 수 없어 기본 제안 대사를 사용합니다.");
                return null;
            }
        }

        public void Request(ProposalRequest request, Action<ProposalReply> reply)
        {
            _ = RequestAsync(request, reply);
        }

        private async Task RequestAsync(ProposalRequest request, Action<ProposalReply> reply)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(request.Cancellation);
            cancellation.CancelAfter(_timeout);
            var choices = new List<ProposalReply>();
            var options = new JArray();
            foreach (EZoneId zone in request.Candidates)
            {
                AddChoice(request, zone, false, choices, options);
                AddChoice(request, zone, true, choices, options);
            }

            try
            {
                var payload = new JObject
                {
                    ["model"] = Model,
                    ["store"] = false,
                    ["reasoning"] = new JObject { ["effort"] = "none" },
                    ["max_output_tokens"] = 128,
                    ["instructions"] = "Choose one supplied option for the speaker. Options have equal priority. Return its index only in the required JSON.",
                    ["input"] = new JObject
                    {
                        ["worker"] = request.Worker.ToString(),
                        ["reason"] = request.Reason.ToString(),
                        ["isHelpRequest"] = request.IsOrcHelp,
                        ["options"] = options
                    }.ToString(),
                    ["text"] = new JObject
                    {
                        ["format"] = new JObject
                        {
                            ["type"] = "json_schema",
                            ["name"] = "proposal",
                            ["strict"] = true,
                            ["schema"] = new JObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JObject { ["index"] = new JObject { ["type"] = "integer" } },
                                ["required"] = new JArray("index"),
                                ["additionalProperties"] = false
                            }
                        }
                    }
                };

                using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
                message.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await Client.SendAsync(message, cancellation.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    ReportError($"HTTP {(int)response.StatusCode}");
                    reply(default);
                    return;
                }

                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (request.Cancellation.IsCancellationRequested) { return; }
                ProposalReply selected = ParseReply(json, choices);
                if (selected.Text == null) { ReportError("선택 응답 형식 오류"); }
                reply(selected);
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException
                or Newtonsoft.Json.JsonException or FormatException or ArgumentException)
            {
                if (!request.Cancellation.IsCancellationRequested)
                {
                    ReportError(exception.GetType().Name);
                    reply(default);
                }
            }
        }

        private void ReportError(string reason)
        {
            if (Interlocked.Exchange(ref _hasReportedError, 1) == 0)
            {
                Debug.LogWarning($"OpenAI 제안 호출 실패 ({reason}). 기본 제안 대사를 사용합니다.");
            }
        }

        private static void AddChoice(ProposalRequest request, EZoneId zone, bool useAlternate,
            List<ProposalReply> choices, JArray options)
        {
            string line = DefaultProposalDialogueProvider.Format(request, zone, useAlternate);
            options.Add(new JObject { ["index"] = choices.Count, ["line"] = line });
            choices.Add(new ProposalReply(zone, line));
        }

        private static ProposalReply ParseReply(string json, IReadOnlyList<ProposalReply> choices)
        {
            JObject result = JObject.Parse(json);
            if ((string)result["status"] != "completed") { return default; }
            foreach (JToken part in result.SelectTokens("output[?(@.type == 'message')].content[?(@.type == 'output_text')]"))
            {
                string text = (string)part["text"];
                if (string.IsNullOrWhiteSpace(text)) { continue; }
                JToken index = JObject.Parse(text)["index"];
                if (index?.Type == JTokenType.Integer && int.TryParse(index.ToString(), out int choice)
                    && choice >= 0 && choice < choices.Count)
                {
                    return choices[choice];
                }
            }
            return default;
        }
    }
}
#endif
