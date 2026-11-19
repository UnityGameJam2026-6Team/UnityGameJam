using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameJam.View;
using UnityEngine.SceneManagement;

namespace UnityGameJam.UI
{
    public sealed class TitleMenu : MonoBehaviour
    {
        [SerializeField] private string _gameScene;
        [SerializeField] private GameObject _menu;
        [SerializeField] private CanvasGroup _menuInput;
        [SerializeField] private CanvasGroup _fade;
        [SerializeField] private GameObject _loadingLabel;
        [SerializeField] private GameObject _leaderboardOverlay;
        [SerializeField] private LeaderboardView _leaderboard;
        [SerializeField] private NameInputPanel _nameInput;
        [SerializeField, Min(0.01f)] private float _fadeSeconds = 0.45f;

        [Header("로딩 만화")]
        [SerializeField] private CanvasGroup _comic;
        [SerializeField] private CanvasGroup[] _comicPanels = Array.Empty<CanvasGroup>();
        [SerializeField] private TMP_Text _comicHint;
        [SerializeField, Min(0.01f)] private float _comicRevealSeconds = 0.24f;

        private bool _isLoading;
        private AsyncOperation _pendingLoad;

        public void OpenLeaderboard()
        {
            if (_isLoading || _nameInput.IsOpen) { return; }
            try
            {
                _leaderboard.Show(LocalLeaderboard.ForThisPc().Read());
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException ||
                exception is ArgumentException)
            {
                _leaderboard.ShowMessage("순위표를 읽지 못했습니다.");
                Debug.LogWarning($"[순위표] 기록 조회 실패 · {exception.Message}", this);
            }
            _leaderboardOverlay.SetActive(true);
            GameSfxPlayer.Play(ESfxCue.PanelOpen);
        }

        public void CloseLeaderboard()
        {
            if (_leaderboardOverlay.activeSelf) { GameSfxPlayer.Play(ESfxCue.PanelClose); }
            _leaderboardOverlay.SetActive(false);
        }

        public void StartGame()
        {
            if (_isLoading || _nameInput.IsOpen || _leaderboardOverlay.activeSelf) { return; }
            if (!Application.CanStreamedLevelBeLoaded(_gameScene))
            {
                Debug.LogError($"빌드 씬 목록에서 게임 씬을 찾을 수 없습니다. ({_gameScene})", this);
                return;
            }

            _nameInput.Open(BeginLoading);
            GameSfxPlayer.Play(ESfxCue.PanelOpen);
        }

        private void BeginLoading()
        {
            _isLoading = true;
            GameSfxPlayer.Play(ESfxCue.Confirm);
            _menuInput.interactable = false;
            _fade.blocksRaycasts = true;
            StartCoroutine(LoadGame());
        }

        public void QuitGame()
        {
            if (_isLoading || _nameInput.IsOpen) { return; }
            GameSfxPlayer.Play(ESfxCue.Confirm);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator LoadGame()
        {
            // 페이드부터 로드를 시작하고 타이틀 연출이 끝난 뒤 씬을 활성화한다.
            _pendingLoad = SceneManager.LoadSceneAsync(_gameScene, LoadSceneMode.Single);
            _pendingLoad.allowSceneActivation = false;
            yield return FadeTo(_fade, 1f);

            DontDestroyOnLoad(gameObject);
            _menu.SetActive(false);
            if (_comic != null && _comicPanels.Length > 0)
            {
                yield return PlayComic();
            }

            _loadingLabel.SetActive(_pendingLoad.progress < 0.9f);
            _pendingLoad.allowSceneActivation = true;
            yield return _pendingLoad;
            _pendingLoad = null;
            yield return null;

            _loadingLabel.SetActive(false);
            yield return FadeTo(_fade, 0f);
            Destroy(gameObject);
        }

        private IEnumerator PlayComic()
        {
            foreach (CanvasGroup panel in _comicPanels) { panel.alpha = 0f; }
            _comicHint.text = "아무 키를 누르세요";
            _comic.gameObject.SetActive(true);
            _comic.alpha = 1f;

            foreach (CanvasGroup panel in _comicPanels)
            {
                yield return WaitForComicKey();
                GameSfxPlayer.Play(ESfxCue.ComicPanel);
                yield return RevealPanel(panel);
            }

            _comicHint.text = "아무 키를 눌러 시작";
            yield return WaitForComicKey();
            yield return FadeTo(_comic, 0f);
            _comic.gameObject.SetActive(false);
        }

        private static IEnumerator WaitForComicKey()
        {
            // 같은 프레임의 입력을 다음 컷에서 다시 읽지 않는다.
            do { yield return null; } while (!WasComicKeyPressed());
        }

        private static bool WasComicKeyPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) { return false; }
            foreach (var key in keyboard.allKeys)
            {
                if (key.wasPressedThisFrame) { return true; }
            }
            return false;
        }

        private IEnumerator RevealPanel(CanvasGroup panel)
        {
            Transform panelTransform = panel.transform;
            Vector3 startScale = Vector3.one * 0.96f;
            panelTransform.localScale = startScale;
            float elapsed = 0f;
            while (elapsed < _comicRevealSeconds)
            {
                yield return null;
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / _comicRevealSeconds);
                panel.alpha = progress;
                panelTransform.localScale = Vector3.Lerp(startScale, Vector3.one, progress);
            }
            panel.alpha = 1f;
            panelTransform.localScale = Vector3.one;
        }

        private IEnumerator FadeTo(CanvasGroup group, float target)
        {
            float start = group.alpha;
            float elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                yield return null;
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                group.alpha = Mathf.SmoothStep(start, target, elapsed / _fadeSeconds);
            }
            group.alpha = target;
        }

        private void OnDisable()
        {
            if (_pendingLoad != null) { _pendingLoad.allowSceneActivation = true; }
        }
    }
}
