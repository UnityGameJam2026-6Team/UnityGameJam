using System;
using System.Collections.Generic;
using System.IO;
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
        [SerializeField] private LeaderboardView _leaderboard;
        private InputAction _leaderboardNavigation;
        private int _pageDirection;
        private bool _isReloading;
        private bool _hasRecorded;

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
            GameSfxPlayer.ShowResult(result.IsSuccess);
        }

        internal void RecordMatch(MatchResult result, MatchBootstrap bootstrap)
        {
            if (_hasRecorded) { return; }
            _hasRecorded = true;
            float seconds = bootstrap.CompletionSeconds ?? bootstrap.MatchDuration;
            bool isComplete = result.FacilityCleanliness >= 100f;
            string time = LeaderboardView.FormatTime(seconds);
            _record.text = isComplete ? $"청소 완료  {time}" : $"종료 진행도  {result.FacilityCleanliness:0.##}%";
            string nickname = PlayerNickname.Current;
            bool hasNickname = !string.IsNullOrWhiteSpace(nickname);
            LocalLeaderboard leaderboard = LocalLeaderboard.ForThisPc();
            try
            {
                if (!hasNickname)
                {
                    _record.text += "  ·  닉네임 미등록";
                    _leaderboard.Show(leaderboard.Read(), useKeyboardPaging: true);
                    return;
                }

                var entry = new LocalLeaderboard.Entry
                {
                    Nickname = nickname,
                    Progress = result.FacilityCleanliness,
                    Seconds = seconds,
                    IsComplete = isComplete
                };
                List<LocalLeaderboard.Entry> entries = leaderboard.Add(entry);
                int index = entries.FindIndex(saved => saved.Nickname == nickname);
                string best = ReferenceEquals(entries[index], entry) ? "새 최고 기록" : "최고 기록 유지";
                _record.text += $"  ·  {best} {index + 1}위";
                _leaderboard.Show(entries, nickname, useKeyboardPaging: true);
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException ||
                exception is ArgumentException)
            {
                _leaderboard.ShowMessage(hasNickname ? "기록을 저장하지 못했습니다." : "순위표를 읽지 못했습니다.");
                Debug.LogWarning($"[순위표] 기록 처리 실패 · {exception.Message}", this);
            }
        }

        private void OnEnable()
        {
            _restartButton.onClick.AddListener(Restart);
            // 결과가 열린 뒤의 새 누름만 받는다. UI/Restart는 초기 키 상태 검사를 끈 Button 액션이다.
            _restartAction.action.performed += OnRestartPressed;
            _restartAction.action.Enable();
            _leaderboardNavigation = InputSystem.actions?.FindAction("UI/Navigate");
            if (_leaderboardNavigation != null) { _leaderboardNavigation.performed += OnLeaderboardNavigate; }
        }

        private void OnDisable()
        {
            _restartButton.onClick.RemoveListener(Restart);
            _restartAction.action.performed -= OnRestartPressed;
            _restartAction.action.Disable();
            if (_leaderboardNavigation != null) { _leaderboardNavigation.performed -= OnLeaderboardNavigate; }
            _pageDirection = 0;
        }

        private void OnLeaderboardNavigate(InputAction.CallbackContext context)
        {
            if (GameMenu.BlocksGameplayInput) { return; }
            float horizontal = context.ReadValue<Vector2>().x;
            int direction = horizontal > 0.5f ? 1 : horizontal < -0.5f ? -1 : 0;
            if (!_isReloading && direction != 0 && direction != _pageDirection) { _leaderboard.ChangePage(direction); }
            _pageDirection = direction;
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
            if (_isReloading || GameMenu.BlocksGameplayInput) { return; }
            _isReloading = true;
            GameSfxPlayer.Restart();
            _restartButton.interactable = false;
            SceneManager.LoadSceneAsync(gameObject.scene.buildIndex, LoadSceneMode.Single);
        }
    }
}
