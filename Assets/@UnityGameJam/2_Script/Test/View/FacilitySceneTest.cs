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
            { EZoneId.Floor1Lobby, (2, 10, 4, 10) },
            { EZoneId.Storage, (6, 6, 2, 5) },
            { EZoneId.MultipurposeRoom, (2, 10, 2, 7) },
            { EZoneId.Lounge, (2, 8, 6, 9) },
            { EZoneId.Floor2Lobby, (2, 10, 2, 9) },
            { EZoneId.Workshop, (3, 6, 8, 5) },
            { EZoneId.MeetingRoom, (5, 6, 5, 4) },
            { EZoneId.Floor3Lobby, (2, 10, 2, 10) },
            { EZoneId.ExecutiveRoom, (3, 11, 8, 8) },
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
        public void 각_층_바닥은_한_평면에_판석을_2미터마다_반복한다()
        {
            foreach (FloorAuthoring floor in _floors)
            {
                Transform surface = floor.transform.Find("FL_FloorSurface");
                Assert.That(surface, Is.Not.Null, floor.name);
                Assert.That(floor.transform.Find("Tiles"), Is.Null, floor.name);
                MeshRenderer renderer = surface.GetComponentsInChildren<MeshRenderer>().Single();
                Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0), Is.EqualTo(6));
                Material material = renderer.sharedMaterial;
                Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(18f, 14f)));
                Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null);
                Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null);
                Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
                BoxCollider ground = surface.GetComponentsInChildren<BoxCollider>().Single();
                Assert.That(ground.size, Is.EqualTo(new Vector3(36f, 0.1f, 28f)));
                Vector3 top = floor.transform.InverseTransformPoint(ground.bounds.center + Vector3.up * ground.bounds.extents.y);
                Assert.That(top.magnitude, Is.LessThan(0.001f), floor.name);
            }
        }

        [Test]
        public void 각_층의_걸레_대여와_반납_지점은_실제_걸레걸이와_일치한다()
        {
            foreach (FloorAuthoring floor in _floors)
            {
                Transform rack = floor.transform.Find("Props/PR_MopRack");
                Assert.IsNotNull(rack, floor.Floor.ToString());
                ToiletStation station = _layout.Toilets.Single(t => t.Floor == floor.Floor);
                Assert.That(Vector3.Distance(station.MopPickupPosition, rack.position), Is.LessThan(0.001f));
            }
        }

        [Test]
        public void Facility_씬의_배치_데이터는_규칙을_모두_만족한다()
        {
            CollectionAssert.IsEmpty(_warnings);
            CollectionAssert.IsEmpty(FacilityLayoutValidator.Validate(_layout));
            var bootstrap = _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MatchBootstrap>(true)).Single();
            var factory = (UnityGameJam.Match.MatchFactory)new UnityEditor.SerializedObject(bootstrap).FindProperty("_factory").objectReferenceValue;
            Assert.DoesNotThrow(() => factory.ValidateInitialContamination(_layout, 42));
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

        [TestCase(0f, 5)]
        [TestCase(0.99f, 5)]
        [TestCase(1f, 4)]
        [TestCase(2f, 3)]
        [TestCase(3f, 2)]
        [TestCase(4f, 1)]
        [TestCase(4.99f, 1)]
        [TestCase(5f, 0)]
        public void 시작_카운트다운은_초마다_바뀌고_오초에_끝난다(float elapsed, int number)
        {
            Assert.AreEqual(number, MatchBootstrap.OpeningNumberAt(elapsed));
        }

        [Test]
        public void 일반_졸개는_로비_NavMesh_위에서_대칭인_한_줄을_만든다()
        {
            MatchBootstrap bootstrap = _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MatchBootstrap>()).Single();
            ViewSettingsSO settings = bootstrap.ViewSettings;
            Vector3 center = _layout.StartPositions[EWorkerType.Player] - Vector3.forward * settings.OpeningLineDistance;
            IReadOnlyList<Bounds> lobby = _layout.Zones.Single(zone => zone.Zone == EZoneId.Floor1Lobby).Areas;
            EWorkerType[] workers = { EWorkerType.Goblin, EWorkerType.Orc, EWorkerType.Skeleton, EWorkerType.Slime };
            for (int index = 0; index < workers.Length; index++)
            {
                Assert.IsTrue(MatchBootstrap.TryGetOpeningMinionPosition(workers[index], center, settings.OpeningMinionSpacing, out Vector3 position));
                Assert.AreEqual(center.x + (index - 1.5f) * settings.OpeningMinionSpacing, position.x, 0.001f);
                Assert.AreEqual(center.z, position.z, 0.001f);
                Assert.IsTrue(lobby.Any(area => position.x >= area.min.x && position.x <= area.max.x
                    && position.z >= area.min.z && position.z <= area.max.z), workers[index].ToString());
                Assert.IsTrue(NavMesh.SamplePosition(position, out _, 0.4f, NavMesh.AllAreas), workers[index].ToString());
            }
            Assert.IsFalse(MatchBootstrap.TryGetOpeningMinionPosition(EWorkerType.Hellhound, center, settings.OpeningMinionSpacing, out _));
        }

        [Test]
        public void 현관_연출에는_회전할_두_문과_입장_배우의_참조가_연결되어_있다()
        {
            FloorAuthoring floor = _floors.Single(candidate => candidate.Floor == EFloor.Floor1);
            Assert.IsNotNull(floor.ArrivalStage);
            Assert.IsNotNull(floor.ArrivalLeftDoor);
            Assert.IsNotNull(floor.ArrivalRightDoor);
            Assert.AreEqual(floor.ArrivalStage, floor.ArrivalLeftDoor.parent);
            Assert.AreEqual(floor.ArrivalStage, floor.ArrivalRightDoor.parent);
            Assert.IsTrue(floor.ArrivalLeftDoor.GetComponent<BoxCollider>().enabled);
            Assert.IsTrue(floor.ArrivalRightDoor.GetComponent<BoxCollider>().enabled);

            MatchBootstrap bootstrap = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MatchBootstrap>()).Single();
            ViewSettingsSO.EndingSettings ending = bootstrap.ViewSettings.Ending;
            Vector3 stop = floor.ArrivalStage.TransformPoint(ending.KingStop);
            Assert.IsTrue(NavMesh.SamplePosition(stop, out _, 0.4f, NavMesh.AllAreas));
            Assert.AreEqual("dog_idle1", ending.DogGreeting.name);
            Assert.IsNotNull(ending.ThrowTrash.GetComponentInChildren<ArtSlot>(true));
            Assert.That(ending.ThrowGripAt, Is.LessThan(ending.ThrowReleaseAt));
        }

        [Test]
        public void 실패_연출은_멀리_선_플레이어와_네_졸개를_배치하고_적중까지_카메라를_잇는다()
        {
            FloorAuthoring floor = _floors.Single(candidate => candidate.Floor == EFloor.Floor1);
            MatchBootstrap bootstrap = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MatchBootstrap>()).Single();
            ViewSettingsSO.EndingSettings ending = bootstrap.ViewSettings.Ending;
            Vector3 player = ending.FailurePlayerStart;
            float successDistance = Vector3.Distance(ending.KingStop, ending.PlayerStart);
            Assert.That(Vector3.Distance(ending.KingStop, player), Is.GreaterThan(successDistance * 2f));
            Assert.That(ending.ThrowLookSeconds, Is.GreaterThan(0f));
            Assert.AreEqual(4, ending.FailureMinionOffsets.Length);
            Assert.AreEqual(2, ending.FailureMinionOffsets.Count(offset => offset.x < 0f));
            Assert.AreEqual(2, ending.FailureMinionOffsets.Count(offset => offset.x > 0f));
            foreach (Vector3 actor in ending.FailureMinionOffsets.Select(offset => player + offset).Append(player))
            {
                Vector3 position = floor.ArrivalStage.TransformPoint(actor);
                Assert.IsTrue(NavMesh.SamplePosition(position, out _, 0.4f, NavMesh.AllAreas), position.ToString());
            }

            Assert.AreEqual(ending.Throw.EndPosition, ending.Impact.StartPosition);
            Assert.AreEqual(ending.Throw.EndFocus, ending.Impact.StartFocus);
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
