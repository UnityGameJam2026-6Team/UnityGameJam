using System;
using NUnit.Framework;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Test.View
{
    /// <summary>Spec FacilityLayout 규칙 1~9: 씬에서 뽑은 배치 데이터 검사.</summary>
    public class FacilityLayoutValidatorTest
    {
        [Test]
        public void 규칙을_모두_만족하는_배치는_위반이_없다()
        {
            CollectionAssert.IsEmpty(FacilityLayoutValidator.Validate(CreateValidLayout()));
        }

        [Test]
        public void 판정_구역이_하나라도_빠지면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            layout.Zones.RemoveAll(z => z.Zone == EZoneId.Workshop);

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 구역의_층이_GDD와_다르면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            layout.Zones.Find(z => z.Zone == EZoneId.Storage).Floor = EFloor.Floor2;

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 구역_사각형이_겹치면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            ZoneArea lounge = layout.Zones.Find(z => z.Zone == EZoneId.Lounge);
            ZoneArea storage = layout.Zones.Find(z => z.Zone == EZoneId.Storage);
            storage.Areas[0] = lounge.Areas[0];

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 맞닿기만_한_구역은_겹친_것이_아니다()
        {
            FacilityLayout layout = CreateValidLayout();
            ZoneArea lounge = layout.Zones.Find(z => z.Zone == EZoneId.Lounge);
            Bounds box = lounge.Areas[0];
            lounge.Areas.Add(Box(box.min.x, box.max.x, box.max.z, box.max.z + 2f)); // 위쪽에 맞닿게 추가

            CollectionAssert.IsEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 화장실이_판정_구역과_겹치면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            layout.Toilets[0].Area = layout.Zones.Find(z => z.Zone == EZoneId.Floor1Lobby).Areas[0];

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 화장실이_층마다_하나가_아니면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            layout.Toilets.RemoveAt(2);

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 배출_구역이_1층_로비_밖이면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            layout.DischargeArea = layout.Zones.Find(z => z.Zone == EZoneId.Storage).Areas[0];

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 작업자_시작점이_빠지면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            layout.StartPositions.Remove(EWorkerType.Hellhound);

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 순찰점이_구역_밖이면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            layout.PatrolRoutes[0].Points[0] = new Vector3(999f, 0f, 999f);

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 순찰점이_3개보다_적으면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            layout.PatrolRoutes[0].Points.RemoveAt(0);

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 생성_지점이_구역_밖이면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            layout.SpawnPoints[0].Position = new Vector3(999f, 0f, 999f);

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 제자리_슬롯_ID가_겹치면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            layout.HomeSlots.Add(new HomeSlot { SlotId = layout.HomeSlots[0].SlotId, Zone = layout.HomeSlots[0].Zone, Position = layout.HomeSlots[0].Position });

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        [Test]
        public void 층_범위가_3개가_아니면_위반이다()
        {
            FacilityLayout layout = CreateValidLayout();
            layout.Floors.RemoveAt(0);

            CollectionAssert.IsNotEmpty(FacilityLayoutValidator.Validate(layout));
        }

        // 구역마다 10x10 칸을 하나씩 갖는 단순한 배치. 층은 X축으로 100씩 떨어진다.
        private static FacilityLayout CreateValidLayout()
        {
            var layout = new FacilityLayout();
            foreach (EFloor floor in Enum.GetValues(typeof(EFloor)))
            {
                float offset = (int)floor * 100f;
                layout.Floors.Add(new FloorArea { Floor = floor, Area = Box(offset - 50f, offset + 50f, -50f, 50f) });
                layout.Toilets.Add(new ToiletStation { Floor = floor, Area = Box(offset + 40f, offset + 44f, 40f, 44f), MopPickupPosition = new Vector3(offset + 42f, 0f, 42f) });
            }

            int slotId = 0;
            int index = 0;
            foreach (EZoneId zone in Enum.GetValues(typeof(EZoneId)))
            {
                float offset = (int)ZoneDisplayName.GetFloor(zone) * 100f;
                float minX = offset - 40f + index * 10f;
                Bounds box = Box(minX, minX + 10f, -5f, 5f);
                var area = new ZoneArea { Zone = zone, Floor = ZoneDisplayName.GetFloor(zone) };
                area.Areas.Add(box);
                layout.Zones.Add(area);

                var route = new PatrolRoute { Zone = zone };
                route.Points.Add(new Vector3(minX + 2f, 0f, -2f));
                route.Points.Add(new Vector3(minX + 8f, 0f, -2f));
                route.Points.Add(new Vector3(minX + 8f, 0f, 2f));
                layout.PatrolRoutes.Add(route);

                layout.SpawnPoints.Add(new SpawnPoint { Zone = zone, Type = EContaminationType.Trash, Position = new Vector3(minX + 5f, 0f, 0f) });
                layout.HomeSlots.Add(new HomeSlot { SlotId = slotId++, Zone = zone, Position = new Vector3(minX + 5f, 0f, 3f) });
                index++;
            }

            ZoneArea lobby = layout.Zones.Find(z => z.Zone == EZoneId.Floor1Lobby);
            Bounds lobbyBox = lobby.Areas[0];
            layout.DischargeArea = Box(lobbyBox.min.x + 1f, lobbyBox.min.x + 3f, -4f, -2f);
            foreach (EWorkerType worker in Enum.GetValues(typeof(EWorkerType)))
            {
                layout.StartPositions[worker] = lobbyBox.center;
            }
            return layout;
        }

        private static Bounds Box(float minX, float maxX, float minZ, float maxZ)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(minX, -0.5f, minZ), new Vector3(maxX, 3f, maxZ));
            return bounds;
        }
    }
}
