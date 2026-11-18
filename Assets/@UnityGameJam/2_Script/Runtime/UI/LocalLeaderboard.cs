using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityGameJam.UI
{
    internal sealed class LocalLeaderboard
    {
        [Serializable]
        internal sealed class Entry
        {
            public string Id;
            public float Progress;
            public float Seconds;
            public bool IsComplete;
        }

        [Serializable]
        private sealed class History
        {
            public List<Entry> Entries = new List<Entry>();
        }

        private readonly string _path;

        internal LocalLeaderboard(string path)
        {
            _path = path;
        }

        internal List<Entry> Add(Entry entry)
        {
            History history = new History();
            if (File.Exists(_path))
            {
                history = JsonUtility.FromJson<History>(File.ReadAllText(_path));
                if (history?.Entries == null)
                {
                    throw new InvalidDataException("기존 순위표 기록을 읽을 수 없습니다.");
                }
            }
            foreach (Entry saved in history.Entries)
            {
                if (saved == null || string.IsNullOrEmpty(saved.Id) ||
                    !float.IsFinite(saved.Progress) || saved.Progress < 0f || saved.Progress > 100f ||
                    !float.IsFinite(saved.Seconds) || saved.Seconds < 0f)
                {
                    throw new InvalidDataException("순위표 기록에 올바르지 않은 값이 있습니다.");
                }
            }
            if (!history.Entries.Exists(saved => saved.Id == entry.Id))
            {
                history.Entries.Add(entry);
            }
            history.Entries.Sort(Compare);
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(history, true));
            if (File.Exists(_path))
            {
                File.Replace(temporary, _path, _path + ".bak");
            }
            else
            {
                File.Move(temporary, _path);
            }
            return history.Entries;
        }

        internal static int Compare(Entry first, Entry second)
        {
            if (first.IsComplete != second.IsComplete) { return first.IsComplete ? -1 : 1; }
            int comparison = first.IsComplete
                ? first.Seconds.CompareTo(second.Seconds)
                : second.Progress.CompareTo(first.Progress);
            return comparison != 0 ? comparison : string.CompareOrdinal(first.Id, second.Id);
        }
    }
}
