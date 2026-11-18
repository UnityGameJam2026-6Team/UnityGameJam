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
            // 닉네임 등록 전에 저장한 기록은 기존 ID로 읽는다.
            public string Id;
            public string Nickname;
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

        internal static LocalLeaderboard ForThisPc() =>
            new LocalLeaderboard(Path.Combine(Application.persistentDataPath, "leaderboard.json"));

        internal List<Entry> Read()
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
                if (saved == null || (string.IsNullOrWhiteSpace(saved.Nickname) && string.IsNullOrEmpty(saved.Id)) ||
                    !float.IsFinite(saved.Progress) || saved.Progress < 0f || saved.Progress > 100f ||
                    !float.IsFinite(saved.Seconds) || saved.Seconds < 0f)
                {
                    throw new InvalidDataException("순위표 기록에 올바르지 않은 값이 있습니다.");
                }
            }
            history.Entries.Sort(Compare);
            return history.Entries;
        }

        internal List<Entry> Add(Entry entry)
        {
            List<Entry> entries = Read();
            bool hasNickname = !string.IsNullOrWhiteSpace(entry.Nickname);
            int index = hasNickname
                ? entries.FindIndex(saved => saved.Nickname == entry.Nickname)
                : entries.FindIndex(saved => string.IsNullOrWhiteSpace(saved.Nickname) && saved.Id == entry.Id);
            if (index >= 0)
            {
                if (!hasNickname || CompareScores(entry, entries[index]) >= 0) { return entries; }
                entries[index] = entry;
            }
            else
            {
                entries.Add(entry);
            }
            entries.Sort(Compare);

            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(new History { Entries = entries }, true));
            if (File.Exists(_path))
            {
                File.Replace(temporary, _path, _path + ".bak");
            }
            else
            {
                File.Move(temporary, _path);
            }
            return entries;
        }

        internal static int Compare(Entry first, Entry second)
        {
            int comparison = CompareScores(first, second);
            if (comparison != 0) { return comparison; }

            string firstKey = string.IsNullOrWhiteSpace(first.Nickname) ? first.Id : first.Nickname;
            string secondKey = string.IsNullOrWhiteSpace(second.Nickname) ? second.Id : second.Nickname;
            int keyComparison = string.CompareOrdinal(firstKey, secondKey);
            return keyComparison != 0 ? keyComparison : string.CompareOrdinal(first.Id, second.Id);
        }

        private static int CompareScores(Entry first, Entry second)
        {
            if (first.IsComplete != second.IsComplete) { return first.IsComplete ? -1 : 1; }
            return first.IsComplete
                ? first.Seconds.CompareTo(second.Seconds)
                : second.Progress.CompareTo(first.Progress);
        }
    }
}
