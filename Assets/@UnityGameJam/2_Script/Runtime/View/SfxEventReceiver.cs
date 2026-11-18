using UnityEngine;

namespace UnityGameJam.View
{
    public sealed class SfxEventReceiver : MonoBehaviour
    {
        [SerializeField] private GameSfxPlayer _audioPrefab;

        private void Awake()
        {
            if (FindFirstObjectByType<GameSfxPlayer>() == null && _audioPrefab != null)
            {
                Instantiate(_audioPrefab);
            }
        }

        public void TitleStamp() { GameSfxPlayer.Play(ESfxCue.TitleStamp); }
        public void TitleTextPop() { GameSfxPlayer.Play(ESfxCue.TitleTextPop); }
        public void TitleCrownLand() { GameSfxPlayer.Play(ESfxCue.TitleCrownLand); }
        public void PanelOpen() { GameSfxPlayer.Play(ESfxCue.PanelOpen); }
        public void PanelClose() { GameSfxPlayer.Play(ESfxCue.PanelClose); }
    }
}
