#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    internal sealed class OpenAiProposalDialogueProvider : IProposalDialogueProvider, ITaskPreferenceProvider
    {
        private const string Model = "gpt-6-luna";
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
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
            string environmentKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim();
            if (!string.IsNullOrEmpty(environmentKey)) { return new OpenAiProposalDialogueProvider(environmentKey, timeoutSeconds); }
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
            var choices = new List<ProposalReply>();
            var options = new JArray();
            if (request.Plans.Count > 0)
            {
                for (int index = 0; index < request.Plans.Count; index++)
                {
                    AddPlanChoice(request, index, false, choices, options);
                    AddPlanChoice(request, index, true, choices, options);
                }
            }
            else
            {
                for (int index = 0; index < request.Candidates.Count; index++)
                {
                    EZoneId zone = request.Candidates[index];
                    AddChoice(request, zone, index, false, choices, options);
                    AddChoice(request, zone, index, true, choices, options);
                }
            }

            try
            {
                var payload = new JObject
                {
                    ["model"] = Model,
                    ["input"] = new JObject
                    {
                        ["worker"] = request.Worker.ToString(),
                        ["persona"] = Persona(request.Worker),
                        ["reason"] = request.Reason.ToString(),
                        ["isHelpRequest"] = request.IsOrcHelp,
                        ["preferredTask"] = request.PreferredTask?.ToString(),
                        ["remainingSeconds"] = request.RemainingSeconds is float seconds ? new JValue(seconds) : JValue.CreateNull()
                    }.ToString(Newtonsoft.Json.Formatting.None),
                    ["questions"] = new JArray(new JObject
                    {
                        ["type"] = "choice",
                        ["name"] = "proposal",
                        ["instructions"] = request.Plans.Count > 0
                            ? "승인 가능한 분업 계획 중 하나를 고른다. 두 작업이 모두 점수에 반영되기까지의 예상 시간이 짧은 후보를 우선한다. "
                                + "시간이 비슷하면 층 이동이 적거나 화자가 선호 작업을 맡는 계획을 고른다."
                            : "주어진 방과 대사 조합 중 하나를 고른다. 낮은 후보 순위가 게임의 우선 추천이다. "
                                + "후보 순위에 확인된 작업량과 선호 작업을 반영한다. 조건이 비슷하면 화자의 성격에 맞는 대사를 고른다.",
                        ["choices"] = options
                    })
                };

                string json = await PostDecisionAsync(payload, request.Cancellation).ConfigureAwait(false);
                if (request.Cancellation.IsCancellationRequested) { return; }
                if (json == null) { reply(default); return; }

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

        public void RequestTaskPreferences(IReadOnlyDictionary<EWorkerType, EContaminationType[]> allowed, CancellationToken cancellation,
            Action<IReadOnlyDictionary<EWorkerType, EContaminationType>> reply)
        {
            _ = RequestTaskPreferencesAsync(allowed, cancellation, reply);
        }

        private async Task RequestTaskPreferencesAsync(IReadOnlyDictionary<EWorkerType, EContaminationType[]> allowed,
            CancellationToken cancellation, Action<IReadOnlyDictionary<EWorkerType, EContaminationType>> reply)
        {
            try
            {
                var questions = new JArray();
                foreach (var entry in allowed)
                {
                    var choices = new JArray();
                    foreach (EContaminationType type in entry.Value)
                    {
                        choices.Add(new JObject { ["value"] = type.ToString(), ["description"] = TaskDescription(type) });
                    }
                    questions.Add(new JObject
                    {
                        ["type"] = "choice",
                        ["name"] = entry.Key.ToString(),
                        ["instructions"] = $"{Persona(entry.Key)} {entry.Key}에게 맞는 선호 청소 작업 하나를 고른다. "
                            + "고블린은 작은 물건 정리, 오크는 무거운 잔해 운반, 슬라임은 폐기물 소화에 강하다.",
                        ["choices"] = choices
                    });
                }
                var payload = new JObject
                {
                    ["model"] = Model,
                    ["input"] = "시설 청소 팀에서 각 졸개의 성격과 역할에 맞는 선호 작업을 고른다.",
                    ["questions"] = questions
                };
                string json = await PostDecisionAsync(payload, cancellation).ConfigureAwait(false);
                if (json == null || cancellation.IsCancellationRequested || JObject.Parse(json)["answers"] is not JArray answers) { return; }

                var preferences = new Dictionary<EWorkerType, EContaminationType>();
                foreach (JObject answer in answers.OfType<JObject>())
                {
                    if ((string)answer["type"] != "choice"
                        || !Enum.TryParse((string)answer["name"], out EWorkerType worker)
                        || !allowed.TryGetValue(worker, out EContaminationType[] tasks)
                        || !Enum.TryParse((string)answer["choice"], out EContaminationType task)
                        || !tasks.Contains(task)) { continue; }
                    preferences.TryAdd(worker, task);
                }
                if (preferences.Count > 0 && !cancellation.IsCancellationRequested) { reply(preferences); }
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException
                or Newtonsoft.Json.JsonException or FormatException or ArgumentException)
            {
                if (!cancellation.IsCancellationRequested) { ReportError(exception.GetType().Name); }
            }
        }

        private async Task<string> PostDecisionAsync(JObject payload, CancellationToken requestCancellation)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation);
            cancellation.CancelAfter(_timeout);
            using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/decisions");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
            message.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await Client.SendAsync(message, cancellation.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                ReportError($"HTTP {(int)response.StatusCode}");
                return null;
            }
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private static string TaskDescription(EContaminationType type) => type switch
        {
            EContaminationType.HeavyDebris => "무거운 잔해 처리",
            EContaminationType.Trash => "일반 쓰레기 처리",
            EContaminationType.ClutterItem => "작은 물건을 제자리에 놓기",
            EContaminationType.FloorStain => "걸레로 바닥 얼룩 닦기",
            _ => type.ToString()
        };

        private void ReportError(string reason)
        {
            if (Interlocked.Exchange(ref _hasReportedError, 1) == 0)
            {
                Debug.LogWarning($"OpenAI 결정 호출 실패 ({reason}). 기본 설정을 사용합니다.");
            }
        }

        private static string Persona(EWorkerType worker) => worker switch
        {
            EWorkerType.Goblin => "호기심 많고 대장에게 친근하게 말한다.",
            EWorkerType.Orc => "일과 동료 지원에 적극적이고 간단명료하게 말한다.",
            EWorkerType.Skeleton => "꼼꼼하고 정중하게 확인한다.",
            EWorkerType.Slime => "느긋하고 붙임성 있게 말한다.",
            _ => "상황에 맞게 짧게 말한다."
        };

        private static void AddChoice(ProposalRequest request, EZoneId zone, int rank, bool useAlternate,
            List<ProposalReply> choices, JArray options)
        {
            string line = DefaultProposalDialogueProvider.Format(request, zone, useAlternate);
            options.Add(new JObject
            {
                ["value"] = choices.Count.ToString(CultureInfo.InvariantCulture),
                ["description"] = $"후보 순위 {rank + 1}, {ZoneDisplayName.GetFullName(zone)}. {line}"
            });
            choices.Add(new ProposalReply(zone, line));
        }

        private static void AddPlanChoice(ProposalRequest request, int index, bool alternate,
            List<ProposalReply> choices, JArray options)
        {
            CollaborationPlan plan = request.Plans[index];
            ProposalReply reply = DefaultProposalDialogueProvider.CollaborationReply(request.Worker, plan, alternate);
            options.Add(new JObject
            {
                ["value"] = choices.Count.ToString(CultureInfo.InvariantCulture),
                ["description"] = $"후보 순위 {index + 1}, {plan.Kind}, 점수 반영까지 약 {Math.Ceiling(plan.EstimatedSeconds)}초, "
                    + $"두 작업자의 층 이동 합계 {plan.FloorChanges}회. {reply.Text}"
            });
            choices.Add(reply);
        }

        internal static ProposalReply ParseReply(string json, IReadOnlyList<ProposalReply> choices)
        {
            if (JObject.Parse(json)["answers"] is not JArray answers) { return default; }
            foreach (JToken entry in answers)
            {
                if (entry is not JObject answer || (string)answer["type"] != "choice" || (string)answer["name"] != "proposal") { continue; }
                JToken value = answer["choice"];
                if (value?.Type == JTokenType.String && int.TryParse((string)value, NumberStyles.None, CultureInfo.InvariantCulture, out int choice)
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
