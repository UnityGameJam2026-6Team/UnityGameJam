using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Player
{
    /// <summary>규칙(A 레인)이 읽는 플레이어 위치와 층(Spec Player 규칙 8).</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerBody : MonoBehaviour, IWorkerBody
    {
        private IReadOnlyList<FloorArea> _floors = new List<FloorArea>();

        public Vector3 Position => transform.position;

        public EFloor Floor
        {
            get
            {
                foreach (FloorArea floor in _floors)
                {
                    if (FacilityLayoutValidator.ContainsXZ(floor.Area, transform.position)) { return floor.Floor; }
                }
                return EFloor.Floor1;
            }
        }

        public void Initialize(IReadOnlyList<FloorArea> floors)
        {
            _floors = floors;
        }
    }
}
