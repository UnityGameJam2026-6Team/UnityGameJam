using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.View;
using UnityGameJam.Contract;
using NUnit.Framework;
using UnityGameJam.UI;

namespace UnityGameJam.Test.View
{
    public sealed class CompletionRankingTest
    {
        [Test]
        public void 완주_기록은_빠른_순서이고_미완주_기록은_진행도가_높은_순서다()
        {
            var records = new[]
            {
                new LocalLeaderboard.Entry { Id = "a", Progress = 70f, Seconds = 180f },
                new LocalLeaderboard.Entry { Id = "b", Progress = 100f, Seconds = 80f, IsComplete = true },
                new LocalLeaderboard.Entry { Id = "c", Progress = 95f, Seconds = 180f },
                new LocalLeaderboard.Entry { Id = "d", Progress = 100f, Seconds = 60f, IsComplete = true }
            };
            Array.Sort(records, LocalLeaderboard.Compare);
            Assert.That(Array.ConvertAll(records, entry => entry.Id), Is.EqualTo(new[] { "d", "b", "c", "a" }));
        }

        [TestCase(EItemState.Carried, false)]
        [TestCase(EItemState.OnFloor, false)]
        [TestCase(EItemState.InDischargeArea, false)]
        [TestCase(EItemState.Resolved, true)]
        [TestCase(EItemState.Dispatched, true)]
        [TestCase(EItemState.Digesting, true)]
        public void 운반물은_남기지_않고_최종_판정에_포함되지_않는_오염만_완료로_본다(EItemState state, bool isComplete)
        {
            var match = new ReadModel
            {
                Items = new[] { new ItemSnapshot(1, EContaminationType.Trash, state, Vector3.zero, null) }
            };
            Assert.That(CompletionRules.IsFullyClean(match), Is.EqualTo(isComplete));
        }

        private sealed class ReadModel : IMatchReadModel
        {
            public float FacilityCleanliness => 100f;
            public IReadOnlyList<ItemSnapshot> Items { get; set; }
            public EMatchState MatchState => EMatchState.Running;
            public float RemainingTime => 100f;
            public float ZoneThreshold => 60f;
            public float FacilityThreshold => 85f;
            public EZoneId? PlayerZone => null;
            public int? PlayerCarriedItemId => null;
            public bool IsPlayerHoldingMop => false;
            public float PlayerMoveSpeed => 1f;
            public bool IsPlayerWorking => false;
            public float PlayerWorkProgress => 0f;
            public IReadOnlyList<MinionStatus> Minions => Array.Empty<MinionStatus>();
            public IReadOnlyList<MessageEntry> ActiveMessages => Array.Empty<MessageEntry>();
            public MatchResult Result => null;
            public float GetZoneCleanliness(EZoneId zone) => 100f;
            public event Action<ItemSnapshot> ItemChanged { add { } remove { } }
            public event Action MessagesChanged { add { } remove { } }
            public event Action<EMatchState> MatchStateChanged { add { } remove { } }
        }

        [Test]
        public void 저장한_기록은_다시_읽히고_같은_판은_한번만_등록된다()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "ranking.json");
            try
            {
                var first = new LocalLeaderboard.Entry { Id = "first", Progress = 90f, Seconds = 180f };
                new LocalLeaderboard(path).Add(first);
                var entries = new LocalLeaderboard(path).Add(first);
                Assert.That(entries.Count, Is.EqualTo(1));
                entries = new LocalLeaderboard(path).Add(new LocalLeaderboard.Entry
                {
                    Id = "second", Progress = 100f, Seconds = 50f, IsComplete = true
                });
                Assert.That(entries.Count, Is.EqualTo(2));
                Assert.That(entries[0].Id, Is.EqualTo("second"));
            }
            finally
            {
                foreach (string file in new[] { path, path + ".bak", path + ".tmp" })
                {
                    if (File.Exists(file)) { File.Delete(file); }
                }
                if (Directory.Exists(directory)) { Directory.Delete(directory); }
            }
        }
    }
}
