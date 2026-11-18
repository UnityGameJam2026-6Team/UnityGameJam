using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Test.View
{
    /// <summary>
    /// Spec FacilityLayout 규칙 1~10을 실제 Facility.unity로 확인한다.
    /// 씬이 열려 있지 않으면 추가로 열었다가 테스트가 끝나면 닫는다.
    /// </summary>
    public class FacilitySceneTest
    {
        private const string ScenePath = "Assets/@UnityGameJam/1_Scene/Facility.unity";

        // Spec 생성 지점 용량표 (무거운 잔해, 일반 쓰레기, 작은 물건=제자리 슬롯, 얼룩)
        private static readonly Dictionary<EZoneId, (int Heavy, int Trash, int Clutter, int Stain)> _capacity = new Dictionary<EZoneId, (int, int, int, int)>
        {
            { EZoneId.Floor1Lobby, (2, 8, 2, 8) },
            { EZoneId.Storage, (6, 5, 2, 3) },
            { EZoneId.MultipurposeRoom, (2, 8, 2, 6) },
            { EZoneId.Lounge, (2, 6, 6, 8) },
            { EZoneId.Floor2Lobby, (2, 8, 2, 8) },
            { EZoneId.Workshop, (3, 5, 8, 3) },
            { EZoneId.MeetingRoom, (5, 5, 5, 3) },
            { EZoneId.Floor3Lobby, (2, 8, 2, 8) },
            { EZoneId.ExecutiveRoom, (3, 10, 8, 8) },
        };

        private Scene _scene;
        private bool _wasLoaded;
        private List<FloorAuthoring> _floors;
        private FacilityLayout _layout;
        private List<string> _warnings;

        [OneTimeSetUp]
        public void OpenScene()
        {
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _wasLoaded = _scene.isLoaded;
            if (!_wasLoaded) { _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); }

            _floors = _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FloorAuthoring>(true)).OrderBy(f => f.Floor).ToList();
            _warnings = new List<string>();
            _layout = FacilityLayoutBuilder.Build(_floors, _warnings);
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            if (!_wasLoaded && _scene.IsValid()) { EditorSceneManager.CloseScene(_scene, true); }
        }

        [Test]
        public void Facility_씬에는_층이_3개_있다()
        {
            Assert.AreEqual(3, _floors.Count);
        }

        [Test]
        public void Facility_씬의_배치_데이터는_규칙을_모두_만족한다()
        {
            CollectionAssert.IsEmpty(_warnings);
            CollectionAssert.IsEmpty(FacilityLayoutValidator.Validate(_layout));
        }

        [Test]
        public void 구역별_생성_지점과_제자리_슬롯_수가_용량표와_같다()
        {
            foreach (KeyValuePair<EZoneId, (int Heavy, int Trash, int Clutter, int Stain)> pair in _capacity)
            {
                List<SpawnPoint> spawns = _layout.SpawnPoints.Where(s => s.Zone == pair.Key).ToList();
                Assert.AreEqual(pair.Value.Heavy, spawns.Count(s => s.Type == EContaminationType.HeavyDebris), pair.Key + " 무거운 잔해");
                Assert.AreEqual(pair.Value.Trash, spawns.Count(s => s.Type == EContaminationType.Trash), pair.Key + " 일반 쓰레기");
                Assert.AreEqual(pair.Value.Clutter, spawns.Count(s => s.Type == EContaminationType.ClutterItem), pair.Key + " 작은 물건");
                Assert.AreEqual(pair.Value.Stain, spawns.Count(s => s.Type == EContaminationType.FloorStain), pair.Key + " 얼룩");
                Assert.AreEqual(pair.Value.Clutter, _layout.HomeSlots.Count(s => s.Zone == pair.Key), pair.Key + " 제자리 슬롯");
            }
        }

        [Test]
        public void 모든_저작_지점이_NavMesh_위에_있고_계단에서_갈_수_있다()
        {
            var failures = new List<string>();
            foreach (StairAuthoring stair in _floors.SelectMany(f => f.GetComponentsInChildren<StairAuthoring>(true)))
            {
                EFloor floor = stair.GetComponentInParent<FloorAuthoring>().Floor;
                Assert.IsTrue(NavMesh.SamplePosition(stair.EntryPosition, out NavMeshHit start, 1f, NavMesh.AllAreas), floor + " 계단이 NavMesh 밖");

                foreach ((string label, Vector3 point) in PointsOn(floor))
                {
                    if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1.2f, NavMesh.AllAreas))
                    {
                        failures.Add($"NavMesh 밖: {label} {point}");
                        continue;
                    }
                    var path = new NavMeshPath();
                    NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path);
                    if (path.status != NavMeshPathStatus.PathComplete) { failures.Add($"{floor} 계단에서 갈 수 없음: {label} {point}"); }
                }
            }
            CollectionAssert.IsEmpty(failures);
        }

        private IEnumerable<(string, Vector3)> PointsOn(EFloor floor)
        {
            foreach (PatrolRoute route in _layout.PatrolRoutes.Where(r => ZoneDisplayName.GetFloor(r.Zone) == floor))
            {
                foreach (Vector3 point in route.Points) { yield return ("순찰 " + route.Zone, point); }
            }
            foreach (SpawnPoint spawn in _layout.SpawnPoints.Where(s => ZoneDisplayName.GetFloor(s.Zone) == floor)) { yield return ("생성 " + spawn.Zone, spawn.Position); }
            foreach (HomeSlot slot in _layout.HomeSlots.Where(s => ZoneDisplayName.GetFloor(s.Zone) == floor)) { yield return ("슬롯 " + slot.Zone, slot.Position); }
            foreach (ToiletStation toilet in _layout.Toilets.Where(t => t.Floor == floor)) { yield return ("걸레", toilet.MopPickupPosition); }
            if (floor == EFloor.Floor1)
            {
                foreach (KeyValuePair<EWorkerType, Vector3> start in _layout.StartPositions) { yield return ("시작 " + start.Key, start.Value); }
            }
        }
    }
}
