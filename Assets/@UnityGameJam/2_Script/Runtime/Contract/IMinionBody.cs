using UnityEngine;

namespace UnityGameJam.Contract
{
    /// <summary>규칙이 읽는 작업자의 위치. 플레이어 몸(B 레인)이 구현한다.</summary>
    public interface IWorkerBody
    {
        Vector3 Position { get; }
        EFloor Floor { get; }
    }

    /// <summary>
    /// 졸개 두뇌(A 레인)가 몸(B 레인, NavMeshAgent)에 내리는 명령.
    /// 층이 다르면 몸이 계단을 거쳐 이동한다. 두뇌는 경로를 몰라도 된다.
    /// </summary>
    public interface IMinionBody : IWorkerBody
    {
        bool HasArrived { get; }

        void MoveTo(EFloor floor, Vector3 destination);
        void Stop();

        /// <summary>종별 속도와 무게 감속은 규칙이 계산해서 넘긴다.</summary>
        void SetMoveSpeed(float speed);
    }
}
