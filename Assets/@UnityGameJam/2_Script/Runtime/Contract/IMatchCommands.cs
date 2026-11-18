namespace UnityGameJam.Contract
{
    /// <summary>
    /// 플레이어 입력(B 레인)을 규칙(A 레인)으로 넘기는 명령.
    /// 위치가 필요한 명령은 MatchSetup.PlayerBody의 현재 위치를 쓴다.
    /// Try로 시작하는 명령은 규칙상 할 수 없으면 false를 돌려주고 아무것도 바꾸지 않는다.
    /// </summary>
    public interface IMatchCommands
    {
        // 졸개 지휘 (GDD §5.1)
        bool BeginTalk(EWorkerType minion);
        void CancelTalk(EWorkerType minion); // TODO: 보류 (미정 U9. 대화 취소)
        bool AssignRoom(EWorkerType minion, EZoneId zone); // TODO: 보류 (미정 U2. 로비 배정)

        // 이동 제안 응답. 가장 오래된 활성 제안에만 적용한다(GDD §6).
        bool ApproveOldestProposal();
        bool HoldOldestProposal();

        // 헬하운드 배출 명령 (GDD §7.2)
        bool DispatchHellhound(); // TODO: 보류 (미정 U16. 헬하운드 예외 명령)

        // 직접 청소 (GDD §3.1, §4)
        bool TryPickUp(int itemId);
        bool TryDrop();
        bool TryPlaceAtHome();
        bool TryStartMop(int itemId);
        void CancelWork();

        // 물걸레 대여·반납 (GDD §7.1)
        bool TryBorrowMop(EFloor floor);
        bool TryReturnMop(); // TODO: 보류 (미정 U5. 걸레 반납)
    }
}
