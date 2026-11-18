using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;

namespace UnityGameJam.Test
{
    public class ContaminationSpawnerTest
    {
        private const string DataPath = "Assets/@UnityGameJam/3_Data/ScriptableObject";
        private static readonly EZoneId[] _zones = (EZoneId[])Enum.GetValues(typeof(EZoneId));
        private static readonly EContaminationType[] _types = (EContaminationType[])Enum.GetValues(typeof(EContaminationType));
        private readonly List<ScriptableObject> _settings = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject settings in _settings)
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
            _settings.Clear();
        }

        [Test]
        public void 판을_시작하면_모든_구역의_오염이_표의_범위_안에서_생성된다()
        {
            ZoneSpawnTableSO table = CreateTable(9, 17);
            var spawner = new ContaminationSpawner(CreateLayout(), table, CreateTypes(), new SeededRandom(30));

            IReadOnlyList<ContaminationItem> items = spawner.Spawn();

            Assert.That(items.Select(item => item.Zone).Distinct(), Is.EquivalentTo(_zones));
            Assert.That(items.Count(item => item.Type == EContaminationType.HeavyDebris), Is.InRange(0, 2));
            Assert.That(items.Count(item => item.Type == EContaminationType.FloorStain), Is.EqualTo(9));
            Assert.That(items.Select(item => item.Id).Distinct().Count(), Is.EqualTo(items.Count));
        }

        [Test]
        public void 시설의_시작_오염량이_허용_범위를_벗어나면_다시_뽑는다()
        {
            var random = new SequenceRandom(0, 1);
            var spawner = new ContaminationSpawner(CreateLayout(), CreateTable(13, 13), CreateTypes(), random);

            IReadOnlyList<ContaminationItem> items = spawner.Spawn();

            Assert.That(items.Sum(item => item.Weight), Is.EqualTo(13));
            Assert.That(items.Count(item => item.Type == EContaminationType.HeavyDebris), Is.EqualTo(1));
            Assert.That(random.Consumed, Is.EqualTo(2));
        }

        [Test]
        public void 시설_오염량_범위를_맞추지_못하면_추첨_한도에서_실패한다()
        {
            var random = new SequenceRandom(0, 0, 0, 1);
            var spawner = new ContaminationSpawner(CreateLayout(), CreateTable(13, 13), CreateTypes(), random);

            Assert.Throws<InvalidOperationException>(() => spawner.Spawn());
            Assert.That(random.Consumed, Is.EqualTo(3));
        }

        [Test]
        public void 오염은_생성_지점을_중복_사용하지_않고_작은_물건은_구역_안의_서로_다른_슬롯을_가진다()
        {
            FacilityLayout layout = CreateLayout();
            var spawner = new ContaminationSpawner(layout, CreateTable(13, 21, 2), CreateTypes(), new SeededRandom(30));

            IReadOnlyList<ContaminationItem> items = spawner.Spawn();
            ContaminationItem[] clutter = items.Where(item => item.Type == EContaminationType.ClutterItem).ToArray();

            Assert.That(clutter.Length, Is.EqualTo(2));
            Assert.That(items.Select(item => item.Position).Distinct().Count(), Is.EqualTo(items.Count));
            Assert.That(clutter.Select(item => item.HomeSlotId), Is.EquivalentTo(layout.HomeSlots
                .Where(slot => slot.Zone == EZoneId.Workshop).Select(slot => (int?)slot.SlotId)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 생성_지점이나_슬롯이_부족하면_설정을_거부한다(bool removeSlot)
        {
            FacilityLayout layout = CreateLayout();
            if (removeSlot)
            {
                layout.HomeSlots.RemoveAll(slot => slot.Zone == EZoneId.Workshop);
            }
            else
            {
                layout.SpawnPoints.RemoveAll(point => point.Zone == EZoneId.Floor1Lobby && point.Type == EContaminationType.HeavyDebris);
            }

            Assert.Throws<ArgumentException>(() => new ContaminationSpawner(layout, CreateTable(13, 21, 2),
                CreateTypes(), new SeededRandom(30)));
        }

        [Test]
        public void 같은_시드와_설정이면_개체_목록이_같다()
        {
            FacilityLayout layout = CreateLayout();
            ZoneSpawnTableSO table = CreateTable(13, 21, 2);
            ContaminationTypeSO types = CreateTypes();

            IReadOnlyList<ContaminationItem> first = new ContaminationSpawner(layout, table, types, new SeededRandom(31)).Spawn();
            IReadOnlyList<ContaminationItem> second = new ContaminationSpawner(layout, table, types, new SeededRandom(31)).Spawn();

            Assert.That(second.Select(item => (item.Id, item.Type, item.Zone, item.Position, item.HomeSlotId)),
                Is.EqualTo(first.Select(item => (item.Id, item.Type, item.Zone, item.Position, item.HomeSlotId))));
        }

        [Test]
        public void 기본_생성_표는_여러_판에서_시설_오염량_범위를_만족한다()
        {
            ZoneSpawnTableSO table = AssetDatabase.LoadAssetAtPath<ZoneSpawnTableSO>(DataPath + "/ZoneSpawnTable.asset");
            ContaminationTypeSO types = AssetDatabase.LoadAssetAtPath<ContaminationTypeSO>(DataPath + "/ContaminationType.asset");
            Assert.That(table, Is.Not.Null);
            Assert.That(types, Is.Not.Null);
            Assert.That(table.MinimumFacilityWeight, Is.EqualTo(170));
            Assert.That(table.MaximumFacilityWeight, Is.EqualTo(200));

            FacilityLayout layout = CreateLayout(10);
            for (int seed = 0; seed < 40; seed++)
            {
                IReadOnlyList<ContaminationItem> items = new ContaminationSpawner(layout, table, types, new SeededRandom(seed)).Spawn();
                Assert.That(items.Sum(item => item.Weight), Is.InRange(170, 200), $"시드 {seed}");
                Assert.That(items.Select(item => item.Zone).Distinct(), Is.EquivalentTo(_zones));
            }
        }

        private ZoneSpawnTableSO CreateTable(int minimum, int maximum, int clutterCount = 0)
        {
            ZoneSpawnTableSO table = CreateSettings<ZoneSpawnTableSO>();
            using var data = new SerializedObject(table);
            data.FindProperty("_minimumFacilityWeight").intValue = minimum;
            data.FindProperty("_maximumFacilityWeight").intValue = maximum;
            data.FindProperty("_maximumAttempts").intValue = 3;
            SerializedProperty ranges = data.FindProperty("_ranges");
            ranges.arraySize = _zones.Length * _types.Length;
            int index = 0;
            foreach (EZoneId zone in _zones)
            {
                foreach (EContaminationType type in _types)
                {
                    SerializedProperty range = ranges.GetArrayElementAtIndex(index++);
                    range.FindPropertyRelative("_zone").intValue = (int)zone;
                    range.FindPropertyRelative("_type").intValue = (int)type;
                    int min = type == EContaminationType.FloorStain ? 1 : 0;
                    if (zone == EZoneId.Workshop && type == EContaminationType.ClutterItem) { min = clutterCount; }
                    int max = zone == EZoneId.Floor1Lobby && type == EContaminationType.HeavyDebris ? 2 : min;
                    range.FindPropertyRelative("_minimum").intValue = min;
                    range.FindPropertyRelative("_maximum").intValue = max;
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        private ContaminationTypeSO CreateTypes()
        {
            ContaminationTypeSO types = CreateSettings<ContaminationTypeSO>();
            using var data = new SerializedObject(types);
            SerializedProperty entries = data.FindProperty("_entries");
            entries.arraySize = 4;
            int[] weights = { 4, 3, 2, 1 };
            for (int index = 0; index < weights.Length; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_type").intValue = (int)_types[index];
                entry.FindPropertyRelative("_weight").intValue = weights[index];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return types;
        }

        private T CreateSettings<T>() where T : ScriptableObject
        {
            T settings = ScriptableObject.CreateInstance<T>();
            _settings.Add(settings);
            return settings;
        }

        private static FacilityLayout CreateLayout(int capacity = 2)
        {
            var layout = new FacilityLayout();
            foreach (EZoneId zone in _zones)
            {
                layout.Zones.Add(new ZoneArea { Zone = zone, Floor = ZoneDisplayName.GetFloor(zone) });
                foreach (EContaminationType type in _types)
                {
                    for (int index = 0; index < capacity; index++)
                    {
                        var position = new Vector3((int)zone * 100f + (int)type * 20f + index, 0f, 0f);
                        layout.SpawnPoints.Add(new SpawnPoint { Zone = zone, Type = type, Position = position });
                    }
                }

                for (int index = 0; index < capacity; index++)
                {
                    layout.HomeSlots.Add(new HomeSlot { SlotId = (int)zone * 100 + index, Zone = zone });
                }
            }
            return layout;
        }

        private sealed class SequenceRandom : IRandom
        {
            private readonly int[] _values;
            public int Consumed { get; private set; }

            public SequenceRandom(params int[] values)
            {
                _values = values;
            }

            public int Next(int minInclusive, int maxExclusive)
            {
                return Consumed < _values.Length ? _values[Consumed++] : minInclusive;
            }
        }
    }
}
