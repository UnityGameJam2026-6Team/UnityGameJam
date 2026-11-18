using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>오염 개체 표시 규칙(Spec ContaminationView 규칙 2·4·5). MonoBehaviour와 분리해 테스트한다.</summary>
    public static class ContaminationViewRules
    {
        public static bool IsVisible(EItemState state)
        {
            return state == EItemState.OnFloor || state == EItemState.InDischargeArea || state == EItemState.Carried;
        }

        public static Vector3 CarryPosition(Vector3 carrierPosition, float carrierHeight, float offset)
        {
            return carrierPosition + Vector3.up * (carrierHeight + offset);
        }

        /// <summary>목록이 비어 있으면 -1.</summary>
        public static int ChoosePrefabIndex(int itemId, int count)
        {
            if (count <= 0) { return -1; }
            int index = itemId % count;
            return index < 0 ? index + count : index;
        }

        /// <summary>슬롯이 없거나 종류를 모르면 null(작은 물건 목록의 첫 번째를 쓴다).</summary>
        public static string ResolveClutterKind(int? homeSlotId, IReadOnlyDictionary<int, string> slotKinds)
        {
            if (!homeSlotId.HasValue || slotKinds == null) { return null; }
            return slotKinds.TryGetValue(homeSlotId.Value, out string kind) ? kind : null;
        }
    }
}
