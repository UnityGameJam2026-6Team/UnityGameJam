using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityGameJam.View;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityGameJam.Contract;

namespace UnityGameJam.UI
{
    public sealed class ResultPanel : MonoBehaviour
    {
        private static readonly Color32 _passColor = new Color32(141, 225, 187, 255);
        private static readonly Color32 _failColor = new Color32(255, 181, 144, 255);

        [SerializeField] private TMP_Text _verdict;
        [SerializeField] private TMP_Text _facility;
        [SerializeField] private TMP_Text[] _zones;
        [SerializeField] private Button _restartButton;
        [SerializeField] private InputActionReference _restartAction;

        [SerializeField] private TMP_Text _record;
        [SerializeField] private TMP_Text _leaderboard;
        private bool _isReloading;
        private bool _hasRecorded;
        private readonly string _recordId = Guid.NewGuid().ToString("N");
        private const int DisplayCount = 10;

        public void Show(MatchResult result, float facilityThreshold, float zoneThreshold)
        {
            _verdict.text = result.IsSuccess ? "성공" : "실패";
            _verdict.color = result.IsSuccess ? _passColor : _failColor;
            ShowScore(_facility, "시설 전체", result.FacilityCleanliness, facilityThreshold);
            foreach (EZoneId zone in Enum.GetValues(typeof(EZoneId)))
            {
                ShowScore(_zones[(int)zone], ZoneDisplayName.GetFullName(zone), result.ZoneCleanliness[zone], zoneThreshold);
            }
            gameObject.SetActive(true);
            _restartButton.Select();
        }

        internal void RecordMatch(MatchResult result, MatchBootstrap bootstrap)
        {
            if (_hasRecorded) { return; }
            _hasRecorded = true;
            float seconds = bootstrap.CompletionSeconds ?? bootstrap.MatchDuration;
            bool isComplete = result.FacilityCleanliness >= 100f;
            var entry = new LocalLeaderboard.Entry
            {
                Id = _recordId, Progress = result.FacilityCleanliness, Seconds = seconds, IsComplete = isComplete
            };
            string time = FormatTime(seconds);
            _record.text = isComplete ? $"청소 완료  {time}" : $"종료 진행도  {result.FacilityCleanliness:0.##}%";
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "leaderboard.json");
                List<LocalLeaderboard.Entry> entries = new LocalLeaderboard(path).Add(entry);
                int rank = entries.FindIndex(saved => saved.Id == _recordId) + 1;
                _record.text += $"  ·  이번 기록 {rank}위";
                var text = new StringBuilder("이 PC 순위표\n");
                for (int i = 0; i < entries.Count && i < DisplayCount; i++)
                {
                    LocalLeaderboard.Entry saved = entries[i];
                    string value = saved.IsComplete ? $"완료  {FormatTime(saved.Seconds)}" : $"{saved.Progress:0.##}%";
                    string marker = saved.Id == _recordId ? "  ◀ 이번 기록" : "";
                    text.AppendLine($"{i + 1}위    {value}{marker}");
                }
                _leaderboard.text = text.ToString();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is ArgumentException)
            {
                _leaderboard.text = "기록을 저장하지 못했습니다.";
                Debug.LogWarning($"[순위표] 기록 저장 실패 · {exception.Message}", this);
            }
        }

        private static string FormatTime(float seconds)
        {
            int centiseconds = Mathf.Max(0, Mathf.RoundToInt(seconds * 100f));
            return $"{centiseconds / 6000:00}:{centiseconds / 100 % 60:00}.{centiseconds % 100:00}";
        }

        private void OnEnable()
        {
            _restartButton.onClick.AddListener(Restart);
            // 결과가 열린 뒤의 새 누름만 받는다. UI/Restart는 초기 키 상태 검사를 끈 Button 액션이다.
            _restartAction.action.performed += OnRestartPressed;
            _restartAction.action.Enable();
        }

        private void OnDisable()
        {
            _restartButton.onClick.RemoveListener(Restart);
            _restartAction.action.performed -= OnRestartPressed;
            _restartAction.action.Disable();
        }

        private static void ShowScore(TMP_Text row, string name, float score, float threshold)
        {
            bool isPassing = score >= threshold;
            string state = isPassing ? "통과" : "미달";
            string color = ColorUtility.ToHtmlStringRGB(isPassing ? _passColor : _failColor);
            row.text = $"{name}<pos=270px>{Mathf.FloorToInt(score)}% / {threshold}%<pos=475px><color=#{color}>{state}</color>";
        }

        private void OnRestartPressed(InputAction.CallbackContext context)
        {
            Restart();
        }

        private void Restart()
        {
            if (_isReloading) { return; }
            _isReloading = true;
            _restartButton.interactable = false;
            SceneManager.LoadSceneAsync(gameObject.scene.buildIndex, LoadSceneMode.Single);
        }
    }
}
