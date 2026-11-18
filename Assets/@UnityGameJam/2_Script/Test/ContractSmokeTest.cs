using System;
using NUnit.Framework;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.Test
{
    /// <summary>
    /// 계약이 GDD 구조와 맞는지, A 레인이 씬 없이 배치 데이터를 만들 수 있는지 확인한다.
    /// </summary>
    public class ContractSmokeTest
    {
        [Test]
        public void 판정_구역은_일반_방_6개와_로비_3개로_9개다()
        {
            Assert.AreEqual(9, Enum.GetValues(typeof(EZoneId)).Length);
        }

        [Test]
        public void 오염_유형은_4가지다()
        {
            Assert.AreEqual(4, Enum.GetValues(typeof(EContaminationType)).Length);
        }

        [Test]
        public void 시설_배치_데이터는_씬_없이_코드로_만들_수_있다()
        {
            var layout = new FacilityLayout { DischargeArea = new Bounds(Vector3.zero, Vector3.one) };
            var zone = new ZoneArea { Zone = EZoneId.Storage, Floor = EFloor.Floor1 };
            zone.Areas.Add(new Bounds(new Vector3(5f, 0f, 0f), new Vector3(4f, 1f, 4f)));
            layout.Zones.Add(zone);
            layout.StartPositions[EWorkerType.Player] = Vector3.zero;

            Assert.AreEqual(1, layout.Zones.Count);
            Assert.AreEqual(EZoneId.Storage, layout.Zones[0].Zone);
            Assert.IsTrue(layout.Zones[0].Areas[0].Contains(new Vector3(5f, 0f, 1f)));
        }

        [Test]
        public void 모든_구역에_표시_이름이_있다()
        {
            foreach (EZoneId zone in Enum.GetValues(typeof(EZoneId)))
            {
                Assert.IsFalse(string.IsNullOrEmpty(ZoneDisplayName.GetName(zone)), zone.ToString());
            }
        }

        [Test]
        public void 일반_방은_층과_이름을_함께_표시한다()
        {
            Assert.AreEqual("1층 다목적실", ZoneDisplayName.GetFullName(EZoneId.MultipurposeRoom));
            Assert.AreEqual("3층 간부실", ZoneDisplayName.GetFullName(EZoneId.ExecutiveRoom));
        }

        [Test]
        public void 개체_스냅샷은_제자리_슬롯을_담을_수_있고_기본값은_없음이다()
        {
            var clutter = new ItemSnapshot(1, EContaminationType.ClutterItem, EItemState.OnFloor, Vector3.zero, null, 7);
            var trash = new ItemSnapshot(2, EContaminationType.Trash, EItemState.OnFloor, Vector3.zero, null);

            Assert.AreEqual(7, clutter.HomeSlotId);
            Assert.IsNull(trash.HomeSlotId);
        }

        [Test]
        public void 로비는_층을_중복해서_붙이지_않는다()
        {
            Assert.AreEqual("2층 로비", ZoneDisplayName.GetFullName(EZoneId.Floor2Lobby));
        }
    }
}
