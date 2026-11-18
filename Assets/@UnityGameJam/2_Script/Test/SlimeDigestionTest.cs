using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;
using UnityGameJam.Facility;
using UnityGameJam.Minion;

namespace UnityGameJam.Test
{
    public class SlimeDigestionTest
    {
        private const string StatsPath = "Assets/@UnityGameJam/3_Data/ScriptableObject/WorkerStats_Slime.asset";

        [Test]
        public void 소화가_끝나기_전에는_새_폐기물을_먹을_수_없다()
        {
            var trash = new ContaminationItem(1, EContaminationType.Trash, 3, EZoneId.Workshop, Vector3.zero);
            var debris = new ContaminationItem(2, EContaminationType.HeavyDebris, 4, EZoneId.Workshop, Vector3.right);
            var layout = new FacilityLayout();
            var registry = new ContaminationRegistry(new ZoneMap(layout), new[] { trash, debris });
            WorkerStatsSO stats = AssetDatabase.LoadAssetAtPath<WorkerStatsSO>(StatsPath);
            var digestion = new SlimeDigestion(registry, stats);
            bool wasDigestingWhenItemChanged = false;
            registry.ItemChanged += item =>
            {
                if (item.State == EItemState.Digesting) { wasDigestingWhenItemChanged = digestion.IsDigesting; }
            };

            Assert.That(digestion.TryEat(1), Is.True);
            Assert.That(wasDigestingWhenItemChanged, Is.True);
            Assert.That(registry.FindItem(1).IsCounted, Is.False);
            digestion.Tick(stats.DigestSeconds / 2f);
            Assert.That(digestion.TryEat(2), Is.False);
            Assert.That(registry.FindItem(2).State, Is.EqualTo(EItemState.OnFloor));

            digestion.Tick(stats.DigestSeconds / 2f);
            Assert.That(digestion.IsDigesting, Is.False);
            Assert.That(registry.FindItem(1).State, Is.EqualTo(EItemState.Resolved));
            Assert.That(registry.FindItem(1).IsCounted, Is.False);
            Assert.That(digestion.TryEat(2), Is.True);
        }

        [Test]
        public void 선점된_폐기물은_먹지_못하고_그동안_소화_시간도_시작하지_않는다()
        {
            var trash = new ContaminationItem(1, EContaminationType.Trash, 3, EZoneId.Workshop, Vector3.zero);
            var registry = new ContaminationRegistry(new ZoneMap(new FacilityLayout()), new[] { trash });
            WorkerStatsSO stats = AssetDatabase.LoadAssetAtPath<WorkerStatsSO>(StatsPath);
            var digestion = new SlimeDigestion(registry, stats);

            Assert.That(registry.TryClaim(1, EWorkerType.Goblin), Is.True);
            Assert.That(digestion.TryEat(1), Is.False);
            Assert.That(digestion.IsDigesting, Is.False);
            Assert.That(registry.FindItem(1).State, Is.EqualTo(EItemState.OnFloor));

            Assert.That(registry.TryRelease(1, EWorkerType.Goblin), Is.True);
            Assert.That(digestion.TryEat(1), Is.True);
        }
    }
}
