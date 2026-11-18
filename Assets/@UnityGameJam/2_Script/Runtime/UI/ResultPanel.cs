using System;
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

        private bool _isReloading;

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
