using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Test.View
{
    public class RoomVisibilityTest
    {
        [Test]
        public void 실제_부트스트랩의_방_가림은_충돌과_그림자를_만들지_않는다()
        {
            const string bootstrapPath = "Assets/@UnityGameJam/4_Prefab/System/SYS_Bootstrap.prefab";
            var bootstrap = AssetDatabase.LoadAssetAtPath<GameObject>(bootstrapPath).GetComponent<MatchBootstrap>();
            var cover = new SerializedObject(bootstrap).FindProperty("_roomCoverPrefab").objectReferenceValue as GameObject;
            Assert.IsNotNull(cover);
            Assert.IsEmpty(cover.GetComponentsInChildren<Collider>(true));
            var renderer = cover.GetComponent<MeshRenderer>();
            Assert.IsNotNull(renderer);
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode);
        }

        [Test]
        public void 플레이어가_들어간_방만_전부_공개하고_새_판에서_다시_가린다()
        {
            var layout = new FacilityLayout();
            var room = new ZoneArea { Zone = EZoneId.Storage, Floor = EFloor.Floor1 };
            room.Areas.Add(new Bounds(Vector3.zero, new Vector3(4f, 3f, 4f)));
            room.Areas.Add(new Bounds(Vector3.right * 4f, new Vector3(4f, 3f, 4f)));
            layout.Zones.Add(room);
            var lobby = new ZoneArea { Zone = EZoneId.Floor1Lobby, Floor = EFloor.Floor1 };
            lobby.Areas.Add(new Bounds(Vector3.back * 5f, Vector3.one * 4f));
            layout.Zones.Add(lobby);
            var workshop = new ZoneArea { Zone = EZoneId.Workshop, Floor = EFloor.Floor2 };
            workshop.Areas.Add(new Bounds(room.Areas[1].center + Vector3.up * 10f, Vector3.one * 3f));
            layout.Zones.Add(workshop);

            var cover = new GameObject("Cover");
            var match = new GameObject("Match");
            var restart = new GameObject("Restart");
            try
            {
                var visibility = new RoomVisibility(layout, cover, match.transform);
                visibility.RevealAt(EFloor.Floor1, lobby.Areas[0].center);
                Assert.AreEqual(3, match.transform.childCount);
                foreach (Transform child in match.transform) { Assert.IsTrue(child.gameObject.activeSelf); }

                visibility.RevealAt(EFloor.Floor1, room.Areas[1].center);
                visibility.RevealAt(EFloor.Floor1, lobby.Areas[0].center);
                foreach (Transform child in match.transform)
                {
                    Assert.AreEqual(child.position == workshop.Areas[0].center, child.gameObject.activeSelf);
                }

                new RoomVisibility(layout, cover, restart.transform);
                Assert.AreEqual(3, restart.transform.childCount);
                foreach (Transform child in restart.transform) { Assert.IsTrue(child.gameObject.activeSelf); }
            }
            finally
            {
                Object.DestroyImmediate(match);
                Object.DestroyImmediate(restart);
                Object.DestroyImmediate(cover);
            }
        }
    }
}
