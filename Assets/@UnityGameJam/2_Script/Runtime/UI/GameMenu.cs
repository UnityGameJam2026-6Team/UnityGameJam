using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.UI
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameMenu : MonoBehaviour
    {
        [SerializeField] private GameObject _overlay;
        [SerializeField] private GameObject _settings;
        [SerializeField] private GameObject _help;
        [SerializeField] private TMP_Text _modeLabel;
        [SerializeField] private TMP_Text _modeDescription;
        [SerializeField] private TMP_Text _helpMode;
        [SerializeField] private TMP_Text _zoomLabel;
        [SerializeField] private UnityEngine.UI.Button _resumeButton;
        [SerializeField] private UnityEngine.UI.Button _helpBackButton;
        [SerializeField] private UnityEngine.UI.ScrollRect _helpScroll;
        [SerializeField] private RectTransform _helpContent;
        [SerializeField] private UnityEngine.UI.RawImage _helpImage;
        [SerializeField] private string _titleScene = "TitleScene";
        [SerializeField, Min(1f)] private float _maximumZoom = 3f;
        [SerializeField, Min(0.1f)] private float _zoomStep = 0.5f;
        private readonly List<InputAction> _suspendedActions = new List<InputAction>();
        private MatchBootstrap _bootstrap;
        private ResultPanel _resultPanel;
        private InputAction _pause;
        private GameObject _previousSelection;
        private float _previousTimeScale;
        private float _zoom = 1f;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLock;
        private bool _isLeaving;
        private static int _resumeFrame = -1;

        public static bool IsOpen { get; private set; }
        public static bool BlocksGameplayInput => IsOpen || Time.frameCount <= _resumeFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            IsOpen = false;
            _resumeFrame = -1;
        }

        private void Awake()
        {
            _bootstrap = FindFirstObjectByType<MatchBootstrap>();
            _resultPanel = FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            _pause = InputSystem.actions.FindAction("GameMenu/Pause", true);
            _overlay.SetActive(false);
            GameControlSettings.ApplySaved();
            RefreshControlLabels();
        }

        private void OnEnable() { _pause?.Enable(); }

        private void OnDisable()
        {
            _pause?.Disable();
            if (IsOpen) { RestoreGame(); }
        }

        private void Update()
        {
            if (_isLeaving || _pause == null || !_pause.WasPressedThisFrame()) { return; }
            if (!IsOpen) { Open(); }
            else if (_help.activeSelf) { BackToSettings(); }
            else { Resume(); }
        }

        public void Open()
        {
            if (IsOpen || _isLeaving || _bootstrap == null || _bootstrap.Session == null || _bootstrap.IsFastForwarding
                || _bootstrap.Session.MatchState == EMatchState.DemonKingArrived) { return; }
            if (_bootstrap.Session.MatchState == EMatchState.Resolved
                && (_resultPanel == null || !_resultPanel.gameObject.activeInHierarchy)) { return; }
            transform.SetAsLastSibling();
            IsOpen = true;
            _previousTimeScale = Time.timeScale;
            _previousCursorVisible = Cursor.visible;
            _previousCursorLock = Cursor.lockState;
            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _bootstrap.IsPaused = true;
            Time.timeScale = 0f;
            InputActionMap player = InputSystem.actions.FindActionMap("Player");
            _suspendedActions.Clear();
            if (player != null)
            {
                foreach (InputAction action in player.actions)
                {
                    if (action.enabled) { _suspendedActions.Add(action); }
                }
                player.Disable();
            }
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            _overlay.SetActive(true);
            _settings.SetActive(true);
            _help.SetActive(false);
            RefreshControlLabels();
            _resumeButton.Select();
            GameSfxPlayer.Play(ESfxCue.PanelOpen);
        }

        public void Resume()
        {
            if (!IsOpen || _isLeaving) { return; }
            _overlay.SetActive(false);
            RestoreGame();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
            }
            GameSfxPlayer.Play(ESfxCue.PanelClose);
        }

        private void RestoreGame()
        {
            IsOpen = false;
            _resumeFrame = Time.frameCount + 1;
            if (_bootstrap != null) { _bootstrap.IsPaused = false; }
            Time.timeScale = _previousTimeScale;
            foreach (InputAction action in _suspendedActions) { action.Enable(); }
            _suspendedActions.Clear();
            Cursor.visible = GameControlSettings.UseSpaceAndMouse || _previousCursorVisible;
            Cursor.lockState = GameControlSettings.UseSpaceAndMouse ? CursorLockMode.None : _previousCursorLock;
        }

        public void ToggleControls()
        {
            GameControlSettings.Toggle();
            RefreshControlLabels();
            GameSfxPlayer.Play(ESfxCue.Confirm);
        }

        private void RefreshControlLabels()
        {
            bool isMouse = GameControlSettings.UseSpaceAndMouse;
            _modeLabel.text = isMouse ? "조작키  ·  한손 + 마우스" : "조작키  ·  기본 양손";
            _modeDescription.text = isMouse
                ? "WASD 이동 · Space 상호작용 / 선택 확인\n제안과 방 선택은 마우스로 클릭"
                : "WASD 이동 · J 상호작용 · J / K 선택 확인\nK / Y 제안 확인 · L / N 보류";
            _helpMode.text = isMouse
                ? "현재: 한손 + 마우스  |  가이드의 J / K 확인 대신 Space · 제안은 버튼 클릭"
                : "현재: 기본 양손  |  J 상호작용 · K / Y 확인 · L / N 보류";
        }

        public void OpenHelp()
        {
            if (!IsOpen) { return; }
            _settings.SetActive(false);
            _help.SetActive(true);
            _helpBackButton.Select();
            _zoom = 1f;
            Canvas.ForceUpdateCanvases();
            ApplyZoom();
            GameSfxPlayer.Play(ESfxCue.PanelOpen);
        }

        public void BackToSettings()
        {
            _help.SetActive(false);
            _settings.SetActive(true);
            _resumeButton.Select();
            GameSfxPlayer.Play(ESfxCue.PanelClose);
        }

        public void ZoomIn() { _zoom = Mathf.Min(_maximumZoom, _zoom + _zoomStep); ApplyZoom(); }
        public void ZoomOut() { _zoom = Mathf.Max(1f, _zoom - _zoomStep); ApplyZoom(); }
        public void FitHelp() { _zoom = 1f; ApplyZoom(); }

        private void ApplyZoom()
        {
            Texture image = _helpImage.texture;
            if (image == null) { return; }
            Vector2 area = _helpScroll.viewport.rect.size;
            float fit = Mathf.Min(area.x / image.width, area.y / image.height);
            _helpContent.sizeDelta = new Vector2(image.width, image.height) * (fit * _zoom);
            _helpContent.anchoredPosition = Vector2.zero;
            _helpScroll.velocity = Vector2.zero;
            _zoomLabel.SetText("{0}%", _zoom * 100f);
        }

        public void ReturnToTitle()
        {
            if (_isLeaving || !IsOpen) { return; }
            if (!Application.CanStreamedLevelBeLoaded(_titleScene))
            {
                Debug.LogError($"빌드 씬 목록에서 타이틀 씬을 찾을 수 없습니다. ({_titleScene})", this);
                return;
            }
            _isLeaving = true;
            _overlay.SetActive(false);
            RestoreGame();
            GameSfxPlayer.Play(ESfxCue.Confirm);
            SceneManager.LoadSceneAsync(_titleScene, LoadSceneMode.Single);
        }

        public void QuitGame()
        {
            if (_isLeaving || !IsOpen) { return; }
            _isLeaving = true;
            _overlay.SetActive(false);
            RestoreGame();
            GameSfxPlayer.Play(ESfxCue.Confirm);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
