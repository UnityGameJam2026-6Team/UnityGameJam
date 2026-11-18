using System;
using System.Collections;
using System.IO;
using UnityEngine;
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

        private bool _isLoading;

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
        }

        public void CloseLeaderboard()
        {
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
        }

        private void BeginLoading()
        {
            _isLoading = true;
            _menuInput.interactable = false;
            _fade.blocksRaycasts = true;
            StartCoroutine(LoadGame());
        }

        public void QuitGame()
        {
            if (_isLoading || _nameInput.IsOpen) { return; }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator LoadGame()
        {
            yield return FadeTo(1f);

            _loadingLabel.SetActive(true);
            yield return null;

            // 씬이 교체되는 프레임을 검은 화면으로 덮은 뒤 게임 화면을 드러낸다.
            DontDestroyOnLoad(gameObject);
            _menu.SetActive(false);
            yield return SceneManager.LoadSceneAsync(_gameScene, LoadSceneMode.Single);
            yield return null;

            _loadingLabel.SetActive(false);
            yield return FadeTo(0f);
            Destroy(gameObject);
        }

        private IEnumerator FadeTo(float target)
        {
            float start = _fade.alpha;
            float elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                yield return null;
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                _fade.alpha = Mathf.SmoothStep(start, target, elapsed / _fadeSeconds);
            }
            _fade.alpha = target;
        }
    }
}
