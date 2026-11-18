using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UnityGameJam.UI
{
    public sealed class LeaderboardView : MonoBehaviour
    {
        private const int DisplayCount = 10;

        [SerializeField] private TMP_Text[] _rows;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private GameObject _pager;
        [SerializeField] private TMP_Text _pageIndicator;
        [SerializeField] private UnityEngine.UI.Button _previous;
        [SerializeField] private UnityEngine.UI.Button _next;

        private IReadOnlyList<LocalLeaderboard.Entry> _entries = Array.Empty<LocalLeaderboard.Entry>();
        private string _highlightedEntry;
        private int _page;
        private bool _useKeyboardPaging;

        private int EntryCount => Math.Min(_entries.Count, DisplayCount);
        private int LastPage => Math.Max(0, (EntryCount - 1) / _rows.Length);

        internal void Show(IReadOnlyList<LocalLeaderboard.Entry> entries, string highlightedEntry = null)
        {
            _entries = entries;
            _highlightedEntry = highlightedEntry;
            _useKeyboardPaging = highlightedEntry != null;
            _page = 0;
            if (EntryCount == 0) { ShowMessage("아직 기록이 없습니다."); }
            else { RenderPage(); }
        }

        internal void ShowMessage(string message)
        {
            _entries = Array.Empty<LocalLeaderboard.Entry>();
            _page = 0;
            foreach (TMP_Text row in _rows) { row.gameObject.SetActive(false); }
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
            RenderPage();
        }

        private void RenderPage()
        {
            int firstRank = _page * _rows.Length;
            _message.gameObject.SetActive(false);
            for (int i = 0; i < _rows.Length; i++)
            {
                int index = firstRank + i;
                TMP_Text row = _rows[i];
                row.gameObject.SetActive(index < EntryCount);
                if (index >= EntryCount) { continue; }
                LocalLeaderboard.Entry entry = _entries[index];
                string result = entry.IsComplete ? $"완료  {FormatTime(entry.Seconds)}" : $"{entry.Progress:0.##}%";
                string color = index == 0 ? "#FFD198" : "#F9E3D2";
                string marker = entry.Id == _highlightedEntry ? "<pos=310><color=#FFB5D1>이번 기록</color>" : "";
                row.text = $"<color={color}>{index + 1}위</color><pos=94>{result}{marker}";
            }

            _pager.SetActive(LastPage > 0);
            _pageIndicator.text = _useKeyboardPaging ? $"A / D  ·  {_page + 1} / {LastPage + 1}" : $"{_page + 1} / {LastPage + 1}";
            _previous.interactable = _page > 0;
            _next.interactable = _page < LastPage;
        }

        internal static string FormatTime(float seconds)
        {
            int centiseconds = Mathf.Max(0, Mathf.RoundToInt(seconds * 100f));
            return $"{centiseconds / 6000:00}:{centiseconds / 100 % 60:00}.{centiseconds % 100:00}";
        }
    }
}
