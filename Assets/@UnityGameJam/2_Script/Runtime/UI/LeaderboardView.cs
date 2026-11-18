using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameJam.View;

namespace UnityGameJam.UI
{
    public sealed class LeaderboardView : MonoBehaviour
    {
        private const int DisplayCount = 10;
        private static readonly Color32 _firstPlaceColor = new Color32(255, 209, 152, 255);
        private static readonly Color32 _recordColor = new Color32(249, 227, 210, 255);
        private static readonly Color32 _highlightColor = new Color32(255, 181, 209, 255);

        [Serializable]
        private sealed class Row
        {
            public Image Background;
            public TMP_Text Rank;
            public TMP_Text Nickname;
            public TMP_Text Record;
        }

        [SerializeField] private Row[] _rows;
        [SerializeField] private GameObject _columnHeaders;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private GameObject _pager;
        [SerializeField] private TMP_Text _pageIndicator;
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;

        private IReadOnlyList<LocalLeaderboard.Entry> _entries = Array.Empty<LocalLeaderboard.Entry>();
        private string _highlightedNickname;
        private int _page;
        private bool _useKeyboardPaging;

        private int EntryCount => Math.Min(_entries.Count, DisplayCount);
        private int LastPage => Math.Max(0, (EntryCount - 1) / _rows.Length);

        internal void Show(IReadOnlyList<LocalLeaderboard.Entry> entries, string highlightedNickname = null, bool useKeyboardPaging = false)
        {
            _entries = entries;
            _highlightedNickname = highlightedNickname;
            _useKeyboardPaging = useKeyboardPaging;
            _page = 0;
            if (EntryCount == 0)
            {
                ShowMessage("아직 기록이 없습니다.");
                return;
            }

            for (int i = 0; i < EntryCount; i++)
            {
                if (IsHighlighted(_entries[i]))
                {
                    _page = i / _rows.Length;
                    break;
                }
            }
            RenderPage();
        }

        internal void ShowMessage(string message)
        {
            _entries = Array.Empty<LocalLeaderboard.Entry>();
            _page = 0;
            foreach (Row row in _rows) { row.Background.gameObject.SetActive(false); }
            _columnHeaders.SetActive(false);
            _pager.SetActive(false);
            _message.text = message;
            _message.gameObject.SetActive(true);
        }

        public void PreviousPage() => ChangePage(-1);
        public void NextPage() => ChangePage(1);

        internal void ChangePage(int direction)
        {
            int next = Mathf.Clamp(_page + direction, 0, LastPage);
            if (next == _page) { return; }
            _page = next;
            GameSfxPlayer.Play(ESfxCue.Navigate);
            RenderPage();
        }

        private void RenderPage()
        {
            int firstRank = _page * _rows.Length;
            _message.gameObject.SetActive(false);
            _columnHeaders.SetActive(true);
            for (int i = 0; i < _rows.Length; i++)
            {
                int index = firstRank + i;
                Row row = _rows[i];
                row.Background.gameObject.SetActive(index < EntryCount);
                if (index >= EntryCount) { continue; }
                LocalLeaderboard.Entry entry = _entries[index];
                bool isHighlighted = IsHighlighted(entry);
                row.Background.enabled = isHighlighted;
                row.Rank.text = $"{index + 1}위";
                row.Rank.color = index == 0 ? _firstPlaceColor : _recordColor;
                row.Nickname.text = string.IsNullOrWhiteSpace(entry.Nickname) ? "이름 없음" : entry.Nickname;
                row.Nickname.color = isHighlighted ? _highlightColor : _recordColor;
                row.Record.text = entry.IsComplete ? FormatTime(entry.Seconds) : $"{entry.Progress:0.##}%";
            }

            _pager.SetActive(LastPage > 0);
            _pageIndicator.text = _useKeyboardPaging ? $"A / D  ·  {_page + 1} / {LastPage + 1}" : $"{_page + 1} / {LastPage + 1}";
            _previous.interactable = _page > 0;
            _next.interactable = _page < LastPage;
        }

        private bool IsHighlighted(LocalLeaderboard.Entry entry) =>
            !string.IsNullOrEmpty(_highlightedNickname) && entry.Nickname == _highlightedNickname;

        internal static string FormatTime(float seconds)
        {
            int centiseconds = Mathf.Max(0, Mathf.RoundToInt(seconds * 100f));
            return $"{centiseconds / 6000:00}:{centiseconds / 100 % 60:00}.{centiseconds % 100:00}";
        }
    }
}
