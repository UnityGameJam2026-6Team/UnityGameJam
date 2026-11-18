using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityGameJam.Contract;
using UnityGameJam.View;
using Object = UnityEngine.Object;

namespace UnityGameJam.Editor.Facility
{
    /// <summary>
    /// Spec FacilityLayout의 치수표(U204)대로 FL_Floor1~3 프리팹을 조립하고 NavMesh를 굽는다.
    /// 처음 화이트박스를 만들 때와 치수표를 고쳤을 때 쓴다. **다시 만들면 층 프리팹을 덮어쓰므로**,
    /// Prefab Mode에서 손으로 고친 내용은 사라진다. 손으로 고친 뒤에는 NavMesh 베이크 메뉴만 쓴다.
    /// </summary>
    public static class WhiteboxFloorBuilder
    {
        private const string MenuRoot = "Tools/UnityGameJam/Facility/";
        private const string PrefabRoot = "Assets/@UnityGameJam/4_Prefab/";
        private const string FloorDir = PrefabRoot + "Floor/";
        private const string NavMeshDir = FloorDir + "NavMesh/";
        private const float Grid = 2f;
        private const float HalfWidth = 18f;
        private const float HalfDepth = 14f;
        private const float CurveRadius = 4f;
        private const int IgnoreRaycastLayer = 2;

        private enum ESide { Bottom, Top, Left, Right }
        private enum ECorner { TopLeft, TopRight, BottomLeft, BottomRight }
        private enum ERoomKind { Room, Toilet, Stairs }

        private sealed class Room
        {
            public ERoomKind Kind;
            public EZoneId Zone;
            public RectInt Rect; // x, y(=z), width, height (m)
            public ESide DoorSide;
            public int DoorFrom;
            public ECorner[] Curves = Array.Empty<ECorner>();
            public (string Prefab, float X, float Z, float RotY)[] Furniture = Array.Empty<(string, float, float, float)>();
        }

        private sealed class FloorPlan
        {
            public EFloor Floor;
            public EZoneId Lobby;
            public List<Room> Rooms = new List<Room>();
        }

        // 구역별 생성 지점 용량 (무거운 잔해, 일반 쓰레기, 작은 물건=제자리 슬롯, 얼룩) — Spec 용량표
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

        private static readonly string[] _clutterKinds = { "Book", "Cup", "Document", "Chair" };

        private static readonly RectInt StairsRect = R(14, 8, 18, 14);
        private static readonly Rect EntranceArea = new Rect(-2f, -14f, 4f, 3f);
        private static readonly Rect DischargeArea = new Rect(4f, -14f, 4f, 3f);

        private static readonly (EWorkerType Worker, Vector2 Position)[] _starts =
        {
            (EWorkerType.Player, new Vector2(0f, -2f)),
            (EWorkerType.Goblin, new Vector2(-2f, -4f)),
            (EWorkerType.Orc, new Vector2(2f, -4f)),
            (EWorkerType.Skeleton, new Vector2(-2f, -6f)),
            (EWorkerType.Slime, new Vector2(2f, -6f)),
            (EWorkerType.Hellhound, new Vector2(-4f, -12f)),
        };

        private static List<FloorPlan> CreatePlans()
        {
            var floor1 = new FloorPlan { Floor = EFloor.Floor1, Lobby = EZoneId.Floor1Lobby };
            floor1.Rooms.Add(new Room
            {
                Kind = ERoomKind.Room, Zone = EZoneId.MultipurposeRoom, Rect = R(-12, 4, 12, 14), DoorSide = ESide.Bottom, DoorFrom = 0,
                Furniture = new[] { ("PR_Table", -7f, 9.5f, 0f), ("PR_Table", -3f, 9.5f, 0f), ("PR_Table", 5f, 9.5f, 0f), ("PR_Table", 9f, 9.5f, 0f) },
            });
            floor1.Rooms.Add(new Room
            {
                Kind = ERoomKind.Room, Zone = EZoneId.Lounge, Rect = R(-18, -14, -8, -4), DoorSide = ESide.Right, DoorFrom = -10,
                Furniture = new[] { ("PR_Sofa", -15f, -9f, 90f), ("PR_Table", -12.5f, -9f, 0f) },
            });
            floor1.Rooms.Add(new Room { Kind = ERoomKind.Toilet, Rect = R(-18, -4, -12, 2), DoorSide = ESide.Right, DoorFrom = -2 });
            floor1.Rooms.Add(new Room
            {
                // 위쪽에 2m 통로(Z 2~4)를 남겨 계단 쪽 로비와 이어지게 한다.
                Kind = ERoomKind.Room, Zone = EZoneId.Storage, Rect = R(10, -10, 18, 2), DoorSide = ESide.Left, DoorFrom = -4,
                Curves = new[] { ECorner.TopRight, ECorner.BottomRight },
                Furniture = new[] { ("PR_Shelf", 14f, -4.5f, 0f), ("PR_Shelf", 14f, -1.5f, 0f) },
            });
            floor1.Rooms.Add(new Room { Kind = ERoomKind.Stairs, Rect = StairsRect });

            var floor2 = new FloorPlan { Floor = EFloor.Floor2, Lobby = EZoneId.Floor2Lobby };
            floor2.Rooms.Add(new Room
            {
                Kind = ERoomKind.Room, Zone = EZoneId.MeetingRoom, Rect = R(-18, -14, -6, -2), DoorSide = ESide.Top, DoorFrom = -10,
                Furniture = new[] { ("PR_Table", -13f, -8f, 0f), ("PR_Table", -11.8f, -8f, 0f) },
            });
            floor2.Rooms.Add(new Room { Kind = ERoomKind.Toilet, Rect = R(-18, -2, -12, 4), DoorSide = ESide.Right, DoorFrom = 0 });
            floor2.Rooms.Add(new Room
            {
                Kind = ERoomKind.Room, Zone = EZoneId.Workshop, Rect = R(6, -14, 18, -2), DoorSide = ESide.Top, DoorFrom = 10,
                Furniture = new[] { ("PR_Desk", 9f, -10f, 0f), ("PR_Desk", 12f, -10f, 0f), ("PR_Desk", 15f, -10f, 0f), ("PR_Desk", 12f, -6.5f, 0f) },
            });
            floor2.Rooms.Add(new Room { Kind = ERoomKind.Stairs, Rect = StairsRect });

            var floor3 = new FloorPlan { Floor = EFloor.Floor3, Lobby = EZoneId.Floor3Lobby };
            floor3.Rooms.Add(new Room
            {
                Kind = ERoomKind.Room, Zone = EZoneId.ExecutiveRoom, Rect = R(-14, 0, 14, 14), DoorSide = ESide.Bottom, DoorFrom = 0,
                Curves = new[] { ECorner.TopLeft, ECorner.TopRight },
                Furniture = new[] { ("PR_Desk", -5f, 9f, 0f), ("PR_Desk", 5f, 9f, 0f), ("PR_Shelf", -8f, 6f, 0f), ("PR_Shelf", 8f, 6f, 0f), ("PR_Sofa", 0f, 11.5f, 0f) },
            });
            floor3.Rooms.Add(new Room { Kind = ERoomKind.Toilet, Rect = R(-18, -14, -12, -8), DoorSide = ESide.Top, DoorFrom = -16 });
            floor3.Rooms.Add(new Room { Kind = ERoomKind.Stairs, Rect = StairsRect });

            return new List<FloorPlan> { floor1, floor2, floor3 };
        }

        // ---------- 메뉴 ----------

        [MenuItem(MenuRoot + "화이트박스 층 다시 만들기 (덮어쓰기)", priority = 0)]
        private static void RebuildMenu()
        {
            if (!EditorUtility.DisplayDialog("화이트박스 층 다시 만들기", "FL_Floor1~3 프리팹을 치수표대로 다시 만들고 NavMesh를 굽습니다.\nPrefab Mode에서 손으로 고친 내용은 사라집니다.", "다시 만들기", "취소")) { return; }
            BuildAll();
            BakeAll();
        }

        [MenuItem(MenuRoot + "층 NavMesh 다시 굽기", priority = 1)]
        private static void BakeMenu()
        {
            BakeAll();
        }

        // ---------- 조립 ----------

        public static void BuildAll()
        {
            int slotId = 0;
            foreach (FloorPlan plan in CreatePlans())
            {
                GameObject root = BuildFloor(plan, ref slotId);
                string path = FloorDir + "FL_" + plan.Floor + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                Debug.Log($"[Facility] 층 프리팹 저장: {path}");
            }
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildFloor(FloorPlan plan, ref int slotId)
        {
            var root = new GameObject("FL_" + plan.Floor);
            FloorAuthoring floorAuthoring = root.AddComponent<FloorAuthoring>();
            SetValue(floorAuthoring, "_floor", (int)plan.Floor);

            NavMeshSurface surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << IgnoreRaycastLayer);
            surface.minRegionArea = 4f; // 곡선 벽 뒤에 갇힌 작은 바닥 조각을 지운다

            Transform tiles = Group(root, "Tiles");
            Transform walls = Group(root, "Walls");
            Transform props = Group(root, "Props");
            Transform markers = Group(root, "Markers");

            // 바닥
            for (int x = -18; x < 18; x += 2)
            {
                for (int z = -14; z < 14; z += 2) { Place("Environment/ENV_FloorTile", tiles, new Vector3(x, 0f, z), 0f); }
            }

            BuildWalls(plan, walls);

            // 방, 화장실, 계단
            var furniture = new List<Rect>();
            foreach (Room room in plan.Rooms)
            {
                RectInt r = room.Rect;
                var center = new Vector3(r.center.x, 0f, r.center.y);
                switch (room.Kind)
                {
                    case ERoomKind.Room:
                        PlaceZone(markers, room.Zone, r);
                        foreach (ECorner corner in room.Curves) { PlaceCurve(walls, r, corner); }
                        foreach ((string prefab, float x, float z, float rot) in room.Furniture)
                        {
                            Place("Prop/" + prefab, props, new Vector3(x, 0f, z), rot);
                            furniture.Add(Footprint(prefab, x, z, rot));
                        }
                        break;
                    case ERoomKind.Toilet:
                        Transform toiletFloor = Place("Environment/ENV_Toilet", props, new Vector3(r.xMin, 0f, r.yMin), 0f);
                        toiletFloor.localScale = new Vector3(r.width, 1f, r.height);
                        Transform toilet = Place("Marker/MK_Toilet", markers, center, 0f);
                        toilet.localScale = new Vector3(r.width, 3f, r.height);
                        Place("Prop/PR_MopRack", props, new Vector3(r.xMin + 0.5f, 0f, center.z), 90f);
                        break;
                    case ERoomKind.Stairs:
                        Place("Environment/ENV_Stairs", props, new Vector3(r.xMin, 0f, r.yMin), 0f);
                        Place("Marker/MK_Stair", markers, new Vector3(r.center.x, 0f, r.yMin + 1.5f), 0f);
                        break;
                }
            }

            // 로비 구역 = 층 범위 − 방 − 화장실 − 계단
            List<RectInt> lobbyRects = ComputeLobbyRects(plan);
            foreach (RectInt rect in lobbyRects) { PlaceZone(markers, plan.Lobby, rect); }

            if (plan.Floor == EFloor.Floor1)
            {
                Transform discharge = Place("Marker/MK_DischargeArea", markers, new Vector3(DischargeArea.center.x, 0f, DischargeArea.center.y), 0f);
                discharge.localScale = new Vector3(DischargeArea.width, 3f, DischargeArea.height);
                Place("Prop/PR_DischargeBin", props, new Vector3(DischargeArea.xMin, 0f, DischargeArea.yMin), 0f);
                foreach ((EWorkerType worker, Vector2 position) in _starts)
                {
                    Transform start = Place("Marker/MK_StartPoint", markers, new Vector3(position.x, 0f, position.y), 0f);
                    SetValue(start.GetComponent<StartPointAuthoring>(), "_worker", (int)worker);
                }
            }

            // 구역별 순찰, 제자리 슬롯, 생성 지점
            foreach (Room room in plan.Rooms.Where(r => r.Kind == ERoomKind.Room))
            {
                var rects = new List<RectInt> { room.Rect };
                PlacePatrol(markers, room.Zone, RoomPatrol(room.Rect));
                List<Vector3> slots = PlaceRoomSlots(markers, props, room, furniture, ref slotId);
                PlaceSpawns(markers, plan, room.Zone, rects, room, furniture, slots);
            }
            PlacePatrol(markers, plan.Lobby, LobbyPatrol(lobbyRects));
            List<Vector3> lobbySlots = PlaceLobbySlots(markers, props, plan.Lobby, lobbyRects, ref slotId);
            PlaceSpawns(markers, plan, plan.Lobby, lobbyRects, null, furniture, lobbySlots);

            return root;
        }

        // ---------- 벽 ----------

        private static void BuildWalls(FloorPlan plan, Transform parent)
        {
            // 2m 단위 벽 조각을 (방향, 시작점) 키로 모아 중복을 없앤다.
            var segments = new HashSet<(bool IsVertical, int X, int Z)>();
            AddRectEdges(segments, R(-18, -14, 18, 14), null);
            foreach (Room room in plan.Rooms)
            {
                // 계단은 아래쪽으로 열려 있다
                AddRectEdges(segments, room.Rect, room.Kind == ERoomKind.Stairs ? ESide.Bottom : (ESide?)null);
            }

            var openings = new List<(bool IsVertical, int X, int Z, float Width, string Prefab)>();
            foreach (Room room in plan.Rooms.Where(r => r.Kind != ERoomKind.Stairs))
            {
                (bool isVertical, int x, int z) = DoorStart(room);
                segments.Remove((isVertical, x, z));
                openings.Add((isVertical, x, z, 2f, "Environment/ENV_Door"));
            }
            if (plan.Floor == EFloor.Floor1)
            {
                segments.Remove((false, -2, -14));
                segments.Remove((false, 0, -14));
                openings.Add((false, -2, -14, 4f, "Environment/ENV_Entrance"));
            }

            foreach ((bool isVertical, int x, int z) in segments)
            {
                Place("Environment/ENV_Wall_Straight", parent, new Vector3(x, 0f, z), isVertical ? -90f : 0f);
            }
            foreach ((bool isVertical, int x, int z, float _, string prefab) in openings)
            {
                Place(prefab, parent, new Vector3(x, 0f, z), isVertical ? -90f : 0f);
            }

            // 벽 끝과 꺾이는 점에 기둥을 세워 틈을 막는다(문틀 끝은 문틀 기둥이 있으므로 제외).
            var openingEnds = new HashSet<(int, int)>();
            foreach ((bool isVertical, int x, int z, float width, string _) in openings)
            {
                openingEnds.Add((x, z));
                openingEnds.Add(isVertical ? (x, z + (int)width) : (x + (int)width, z));
            }
            var joints = new Dictionary<(int, int), List<Vector2Int>>();
            foreach ((bool isVertical, int x, int z) in segments)
            {
                var dir = isVertical ? Vector2Int.up : Vector2Int.right;
                AddJoint(joints, (x, z), dir);
                AddJoint(joints, isVertical ? (x, z + 2) : (x + 2, z), -dir);
            }
            foreach (KeyValuePair<(int, int), List<Vector2Int>> joint in joints)
            {
                if (openingEnds.Contains(joint.Key)) { continue; }
                List<Vector2Int> dirs = joint.Value;
                bool isStraight = dirs.Count == 2 && dirs[0] == -dirs[1];
                if (!isStraight) { Place("Environment/ENV_Wall_Corner", parent, new Vector3(joint.Key.Item1, 0f, joint.Key.Item2), 0f); }
            }
        }

        private static void AddRectEdges(HashSet<(bool, int, int)> segments, RectInt r, ESide? skip)
        {
            for (int x = r.xMin; x < r.xMax; x += 2)
            {
                if (skip != ESide.Bottom) { segments.Add((false, x, r.yMin)); }
                if (skip != ESide.Top) { segments.Add((false, x, r.yMax)); }
            }
            for (int z = r.yMin; z < r.yMax; z += 2)
            {
                if (skip != ESide.Left) { segments.Add((true, r.xMin, z)); }
                if (skip != ESide.Right) { segments.Add((true, r.xMax, z)); }
            }
        }

        private static (bool IsVertical, int X, int Z) DoorStart(Room room)
        {
            RectInt r = room.Rect;
            return room.DoorSide switch
            {
                ESide.Bottom => (false, room.DoorFrom, r.yMin),
                ESide.Top => (false, room.DoorFrom, r.yMax),
                ESide.Left => (true, r.xMin, room.DoorFrom),
                _ => (true, r.xMax, room.DoorFrom),
            };
        }

        private static Vector2 DoorCenter(Room room)
        {
            (bool isVertical, int x, int z) = DoorStart(room);
            return isVertical ? new Vector2(x, z + 1f) : new Vector2(x + 1f, z);
        }

        private static void AddJoint(Dictionary<(int, int), List<Vector2Int>> joints, (int, int) point, Vector2Int dir)
        {
            if (!joints.TryGetValue(point, out List<Vector2Int> list))
            {
                list = new List<Vector2Int>();
                joints[point] = list;
            }
            list.Add(dir);
        }

        private static void PlaceCurve(Transform parent, RectInt r, ECorner corner)
        {
            Vector2 center = CurveCenter(r, corner);
            // 기본 조각은 0~90°(오른쪽 위). Y축 회전으로 사분면을 맞춘다.
            float rot = corner switch
            {
                ECorner.TopRight => 0f,
                ECorner.TopLeft => -90f,
                ECorner.BottomLeft => 180f,
                _ => 90f,
            };
            Place("Environment/ENV_Wall_Curve", parent, new Vector3(center.x, 0f, center.y), rot);
        }

        private static Vector2 CurveCenter(RectInt r, ECorner corner)
        {
            return corner switch
            {
                ECorner.TopRight => new Vector2(r.xMax - CurveRadius, r.yMax - CurveRadius),
                ECorner.TopLeft => new Vector2(r.xMin + CurveRadius, r.yMax - CurveRadius),
                ECorner.BottomLeft => new Vector2(r.xMin + CurveRadius, r.yMin + CurveRadius),
                _ => new Vector2(r.xMax - CurveRadius, r.yMin + CurveRadius),
            };
        }

        /// <summary>곡선 벽 바깥(걸을 수 없는 모서리 주머니)에 있는 점인지.</summary>
        private static bool IsInCurvePocket(Room room, Vector2 point, float margin)
        {
            if (room == null) { return false; }
            foreach (ECorner corner in room.Curves)
            {
                Vector2 center = CurveCenter(room.Rect, corner);
                bool isInCornerSquare = Mathf.Abs(point.x - center.x) <= CurveRadius && Mathf.Abs(point.y - center.y) <= CurveRadius
                    && Mathf.Sign(point.x - center.x) == (corner == ECorner.TopRight || corner == ECorner.BottomRight ? 1f : -1f)
                    && Mathf.Sign(point.y - center.y) == (corner == ECorner.TopRight || corner == ECorner.TopLeft ? 1f : -1f);
                if (isInCornerSquare && Vector2.Distance(point, center) > CurveRadius - margin) { return true; }
            }
            return false;
        }

        // ---------- 구역·마커 ----------

        private static List<RectInt> ComputeLobbyRects(FloorPlan plan)
        {
            const int cols = 18;
            const int rows = 14;
            var isFree = new bool[cols, rows];
            for (int c = 0; c < cols; c++)
            {
                for (int w = 0; w < rows; w++)
                {
                    var cell = new Vector2(-18 + c * 2 + 1, -14 + w * 2 + 1);
                    isFree[c, w] = plan.Rooms.All(room => !room.Rect.Contains(Vector2Int.FloorToInt(cell)));
                }
            }

            var result = new List<RectInt>();
            for (int w = 0; w < rows; w++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (!isFree[c, w]) { continue; }
                    int width = 1;
                    while (c + width < cols && isFree[c + width, w]) { width++; }
                    int height = 1;
                    while (w + height < rows && Enumerable.Range(c, width).All(i => isFree[i, w + height])) { height++; }
                    for (int i = c; i < c + width; i++)
                    {
                        for (int j = w; j < w + height; j++) { isFree[i, j] = false; }
                    }
                    result.Add(new RectInt(-18 + c * 2, -14 + w * 2, width * 2, height * 2));
                }
            }
            return result;
        }

        private static void PlaceZone(Transform parent, EZoneId zone, RectInt r)
        {
            Transform volume = Place("Marker/MK_ZoneVolume", parent, new Vector3(r.center.x, 0f, r.center.y), 0f);
            volume.localScale = new Vector3(r.width, 3f, r.height);
            volume.name = "MK_ZoneVolume_" + zone;
            SetValue(volume.GetComponent<ZoneVolumeAuthoring>(), "_zone", (int)zone);
        }

        private static List<Vector3> RoomPatrol(RectInt r)
        {
            const float inset = 1.5f;
            return new List<Vector3>
            {
                new Vector3(r.xMin + inset, 0f, r.yMin + inset),
                new Vector3(r.xMax - inset, 0f, r.yMin + inset),
                new Vector3(r.xMax - inset, 0f, r.yMax - inset),
                new Vector3(r.xMin + inset, 0f, r.yMax - inset),
            };
        }

        private static List<Vector3> LobbyPatrol(List<RectInt> rects)
        {
            List<Vector2> centers = rects.Where(r => r.width * r.height >= 8).Select(r => r.center).ToList();
            Vector2 mid = centers.Aggregate(Vector2.zero, (a, b) => a + b) / Mathf.Max(1, centers.Count);
            return centers.OrderBy(c => Mathf.Atan2(c.y - mid.y, c.x - mid.x)).Select(c => new Vector3(c.x, 0f, c.y)).ToList();
        }

        private static void PlacePatrol(Transform parent, EZoneId zone, List<Vector3> worldPoints)
        {
            Transform route = Place("Marker/MK_PatrolRoute", parent, Vector3.zero, 0f);
            route.name = "MK_PatrolRoute_" + zone;
            var so = new SerializedObject(route.GetComponent<PatrolRouteAuthoring>());
            so.FindProperty("_zone").enumValueIndex = (int)zone;
            SerializedProperty points = so.FindProperty("_localPoints");
            points.arraySize = worldPoints.Count;
            for (int i = 0; i < worldPoints.Count; i++) { points.GetArrayElementAtIndex(i).vector3Value = worldPoints[i]; }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<Vector3> PlaceRoomSlots(Transform markers, Transform props, Room room, List<Rect> furniture, ref int slotId)
        {
            int count = _capacity[room.Zone].Clutter;
            RectInt r = room.Rect;
            Vector2 door = DoorCenter(room);
            var candidates = new List<(Vector2 Position, float Rot)>();
            const float wallOffset = 0.7f;
            for (float x = r.xMin + 2f; x <= r.xMax - 2f; x += 2f)
            {
                candidates.Add((new Vector2(x, r.yMin + wallOffset), 0f));
                candidates.Add((new Vector2(x, r.yMax - wallOffset), 0f));
            }
            for (float z = r.yMin + 2f; z <= r.yMax - 2f; z += 2f)
            {
                candidates.Add((new Vector2(r.xMin + wallOffset, z), 90f));
                candidates.Add((new Vector2(r.xMax - wallOffset, z), 90f));
            }
            candidates = candidates
                .Where(c => Vector2.Distance(c.Position, door) > 2f)
                .Where(c => !IsInCurvePocket(room, c.Position, 1f))
                .Where(c => furniture.All(f => DistanceToRect(f, c.Position) > 1.2f))
                .ToList();
            return PlaceSlots(markers, props, room.Zone, Spread(candidates, count), ref slotId);
        }

        private static List<Vector3> PlaceLobbySlots(Transform markers, Transform props, EZoneId zone, List<RectInt> rects, ref int slotId)
        {
            int count = _capacity[zone].Clutter;
            var candidates = new List<(Vector2 Position, float Rot)>();
            foreach (RectInt r in rects.Where(r => r.width >= 4 && r.height >= 4).OrderByDescending(r => r.width * r.height))
            {
                candidates.Add((new Vector2(r.xMin + 1.5f, r.yMax - 1.5f), 0f));
                candidates.Add((new Vector2(r.xMax - 1.5f, r.yMax - 1.5f), 0f));
            }
            candidates = candidates.Where(c => !IsReserved(c.Position)).ToList();
            return PlaceSlots(markers, props, zone, Spread(candidates, count), ref slotId);
        }

        private static List<Vector3> PlaceSlots(Transform markers, Transform props, EZoneId zone, List<(Vector2 Position, float Rot)> chosen, ref int slotId)
        {
            var positions = new List<Vector3>();
            for (int i = 0; i < chosen.Count; i++)
            {
                string kind = _clutterKinds[i % _clutterKinds.Length];
                var position = new Vector3(chosen[i].Position.x, 0f, chosen[i].Position.y);
                Place("Prop/PR_HomeSlot_" + kind, props, position, chosen[i].Rot);
                Transform marker = Place("Marker/MK_HomeSlot", markers, position, 0f);
                var so = new SerializedObject(marker.GetComponent<HomeSlotAuthoring>());
                so.FindProperty("_slotId").intValue = slotId++;
                so.FindProperty("_itemKind").stringValue = kind;
                so.ApplyModifiedPropertiesWithoutUndo();
                positions.Add(position);
            }
            if (chosen.Count < _capacity[zone].Clutter) { Debug.LogWarning($"[Facility] {zone} 제자리 슬롯 자리가 모자랍니다: {chosen.Count}/{_capacity[zone].Clutter}"); }
            return positions;
        }

        private static void PlaceSpawns(Transform markers, FloorPlan plan, EZoneId zone, List<RectInt> rects, Room room, List<Rect> furniture, List<Vector3> slots)
        {
            var doors = plan.Rooms.Where(r => r.Kind != ERoomKind.Stairs).Select(DoorCenter).ToList();
            var candidates = new List<Vector2>();
            foreach (RectInt r in rects)
            {
                for (float x = r.xMin + 0.5f; x < r.xMax; x += 1f)
                {
                    for (float z = r.yMin + 0.5f; z < r.yMax; z += 1f)
                    {
                        var p = new Vector2(x, z);
                        bool isNearRoomWall = room != null && (x < r.xMin + 1f || x > r.xMax - 1f || z < r.yMin + 1f || z > r.yMax - 1f);
                        bool isNearAnyWall = IsNearWall(plan, p, 0.8f);
                        if (isNearRoomWall || isNearAnyWall) { continue; }
                        if (IsInCurvePocket(room, p, 0.8f)) { continue; }
                        if (doors.Any(d => Vector2.Distance(d, p) < 1.5f)) { continue; }
                        if (furniture.Any(f => DistanceToRect(f, p) < 0.8f)) { continue; }
                        if (slots.Any(s => Vector2.Distance(new Vector2(s.x, s.z), p) < 1.2f)) { continue; }
                        if (plan.Floor == EFloor.Floor1 && IsReserved(p)) { continue; }
                        candidates.Add(p);
                    }
                }
            }

            (int heavy, int trash, int clutter, int stain) = _capacity[zone];
            int needed = heavy + trash + clutter + stain;
            var random = new System.Random((int)zone * 97 + 13);
            List<Vector2> shuffled = candidates.OrderBy(_ => random.Next()).ToList();
            if (shuffled.Count < needed) { Debug.LogWarning($"[Facility] {zone} 생성 지점 자리가 모자랍니다: {shuffled.Count}/{needed}"); }

            int index = 0;
            void Add(EContaminationType type, int count)
            {
                for (int i = 0; i < count && index < shuffled.Count; i++, index++)
                {
                    Transform spawn = Place("Marker/MK_SpawnPoint", markers, new Vector3(shuffled[index].x, 0f, shuffled[index].y), 0f);
                    SetValue(spawn.GetComponent<SpawnPointAuthoring>(), "_type", (int)type);
                }
            }
            Add(EContaminationType.HeavyDebris, heavy);
            Add(EContaminationType.Trash, trash);
            Add(EContaminationType.ClutterItem, clutter);
            Add(EContaminationType.FloorStain, stain);
        }

        /// <summary>1층 현관, 배출 구역, 시작점 주변처럼 오염과 소품을 두지 않는 곳.</summary>
        private static bool IsReserved(Vector2 p)
        {
            if (Expand(EntranceArea, 0.5f).Contains(p) || Expand(DischargeArea, 0.5f).Contains(p)) { return true; }
            return _starts.Any(s => Vector2.Distance(s.Position, p) < 1.2f);
        }

        /// <summary>층의 어느 벽선(바깥 벽, 방·화장실·계단 테두리)에서든 distance 이내인지.</summary>
        private static bool IsNearWall(FloorPlan plan, Vector2 p, float distance)
        {
            if (p.x < -HalfWidth + distance || p.x > HalfWidth - distance || p.y < -HalfDepth + distance || p.y > HalfDepth - distance) { return true; }
            foreach (Room room in plan.Rooms)
            {
                RectInt r = room.Rect;
                bool isInsideX = p.x >= r.xMin - distance && p.x <= r.xMax + distance;
                bool isInsideZ = p.y >= r.yMin - distance && p.y <= r.yMax + distance;
                if (!isInsideX || !isInsideZ) { continue; }
                if (Mathf.Abs(p.x - r.xMin) < distance || Mathf.Abs(p.x - r.xMax) < distance || Mathf.Abs(p.y - r.yMin) < distance || Mathf.Abs(p.y - r.yMax) < distance) { return true; }
            }
            return false;
        }

        // ---------- NavMesh ----------

        public static void BakeAll()
        {
            if (!AssetDatabase.IsValidFolder(NavMeshDir.TrimEnd('/'))) { AssetDatabase.CreateFolder(FloorDir.TrimEnd('/'), "NavMesh"); }
            foreach (EFloor floor in Enum.GetValues(typeof(EFloor)))
            {
                string prefabPath = FloorDir + "FL_" + floor + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var surface = root.GetComponent<NavMeshSurface>();
                    surface.BuildNavMesh();
                    string dataPath = NavMeshDir + "NavMesh_FL_" + floor + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(dataPath);
                    if (existing != null) { AssetDatabase.DeleteAsset(dataPath); }
                    AssetDatabase.CreateAsset(surface.navMeshData, dataPath);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log($"[Facility] NavMesh 베이크: {dataPath}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- 도우미 ----------

        private static RectInt R(int minX, int minZ, int maxX, int maxZ)
        {
            return new RectInt(minX, minZ, maxX - minX, maxZ - minZ);
        }

        private static Transform Group(GameObject root, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root.transform, false);
            return group;
        }

        private static Transform Place(string prefabPath, Transform parent, Vector3 localPosition, float rotY)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + prefabPath + ".prefab");
            if (prefab == null) { throw new InvalidOperationException("프리팹이 없습니다: " + prefabPath); }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            return instance.transform;
        }

        private static void SetValue(Component component, string property, int enumOrInt)
        {
            var so = new SerializedObject(component);
            SerializedProperty prop = so.FindProperty(property);
            if (prop.propertyType == SerializedPropertyType.Enum) { prop.enumValueIndex = enumOrInt; }
            else { prop.intValue = enumOrInt; }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Rect Footprint(string prefab, float x, float z, float rotY)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "Prop/" + prefab + ".prefab");
            var box = asset.GetComponent<BoxCollider>();
            Vector2 size = box != null ? new Vector2(box.size.x, box.size.z) : Vector2.one;
            if (Mathf.Abs(Mathf.DeltaAngle(rotY, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(rotY, -90f)) < 1f) { size = new Vector2(size.y, size.x); }
            return new Rect(x - size.x * 0.5f, z - size.y * 0.5f, size.x, size.y);
        }

        private static float DistanceToRect(Rect rect, Vector2 p)
        {
            float dx = Mathf.Max(rect.xMin - p.x, 0f, p.x - rect.xMax);
            float dz = Mathf.Max(rect.yMin - p.y, 0f, p.y - rect.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static Rect Expand(Rect rect, float amount)
        {
            return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }

        private static List<(Vector2, float)> Spread(List<(Vector2 Position, float Rot)> candidates, int count)
        {
            if (candidates.Count <= count) { return candidates.Select(c => (c.Position, c.Rot)).ToList(); }
            var result = new List<(Vector2, float)>(count);
            float stride = candidates.Count / (float)count;
            for (int i = 0; i < count; i++)
            {
                (Vector2 position, float rot) = candidates[Mathf.FloorToInt(i * stride)];
                result.Add((position, rot));
            }
            return result;
        }
    }
}
