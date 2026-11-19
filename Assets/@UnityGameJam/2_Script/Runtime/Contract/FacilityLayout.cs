using System.Collections.Generic;
using UnityEngine;

namespace UnityGameJam.Contract
{
    /// <summary>
    /// 씬(B 레인)에서 뽑아 규칙(A 레인)에 넘기는 시설 배치 데이터.
    /// MonoBehaviour를 참조하지 않으므로 A 레인은 테스트 코드에서 직접 만들어 쓸 수 있다.
    /// 층은 X축으로 떨어뜨려 배치하므로 모든 좌표는 월드 좌표 하나로 구분된다.
    /// </summary>
    public sealed class FacilityLayout
    {
        public List<FloorArea> Floors { get; } = new List<FloorArea>();
        public List<ZoneArea> Zones { get; } = new List<ZoneArea>();
        public List<ToiletStation> Toilets { get; } = new List<ToiletStation>();
        public List<PatrolRoute> PatrolRoutes { get; } = new List<PatrolRoute>();
        public List<SpawnPoint> SpawnPoints { get; } = new List<SpawnPoint>();
        public List<HomeSlot> HomeSlots { get; } = new List<HomeSlot>(); // TODO: 보류 (미정 U3. 작은 물건 제자리)
        public List<WasteBin> WasteBins { get; } = new List<WasteBin>();
        public Dictionary<EWorkerType, Vector3> StartPositions { get; } = new Dictionary<EWorkerType, Vector3>();

        /// <summary>1층 현관 배출 구역. 이 안의 폐기물만 헬하운드가 가져간다(GDD §7.2).</summary>
        public Bounds DischargeArea { get; set; }

        public WasteBin FindNearestWasteBin(EFloor floor, Vector3 position, float reach = float.PositiveInfinity)
        {
            WasteBin nearest = null;
            float bestDistance = reach * reach;
            foreach (WasteBin bin in WasteBins)
            {
                if (bin.Floor != floor) { continue; }
                Vector3 offset = bin.Position - position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance > bestDistance) { continue; }
                bestDistance = distance;
                nearest = bin;
            }
            return nearest;
        }
    }

    public sealed class WasteBin
    {
        public EFloor Floor { get; set; }
        public Vector3 Position { get; set; }
    }

    /// <summary>층 전체 범위. 판정 구역 밖(화장실·계단)에 떨어진 물건의 층을 찾는 데 쓴다.</summary>
    public sealed class FloorArea
    {
        public EFloor Floor { get; set; }
        public Bounds Area { get; set; }
    }

    /// <summary>판정 구역. 모양이 복잡한 방은 사각형 여러 개로 나타낸다.</summary>
    public sealed class ZoneArea
    {
        public EZoneId Zone { get; set; }
        public EFloor Floor { get; set; }
        public List<Bounds> Areas { get; } = new List<Bounds>();
    }

    /// <summary>층마다 1개인 화장실. 청결도 판정에서 제외한다(GDD §2).</summary>
    public sealed class ToiletStation
    {
        public EFloor Floor { get; set; }
        public Vector3 MopPickupPosition { get; set; }
        public Bounds Area { get; set; }
    }

    /// <summary>졸개가 방에 도착해 작업이 보이지 않을 때 한 바퀴 도는 경로(GDD §5.1-2).</summary>
    public sealed class PatrolRoute
    {
        public EZoneId Zone { get; set; }
        public List<Vector3> Points { get; } = new List<Vector3>();
    }

    /// <summary>오염 생성 지점. 유형마다 놓일 수 있는 자리가 다르다.</summary>
    public sealed class SpawnPoint
    {
        public EZoneId Zone { get; set; }
        public EContaminationType Type { get; set; }
        public Vector3 Position { get; set; }
    }

    /// <summary>어질러진 작은 물건을 가져다 놓는 지정 자리(GDD §3.1).</summary>
    public sealed class HomeSlot
    {
        public int SlotId { get; set; }
        public EZoneId Zone { get; set; }
        public Vector3 Position { get; set; }
    }
}
