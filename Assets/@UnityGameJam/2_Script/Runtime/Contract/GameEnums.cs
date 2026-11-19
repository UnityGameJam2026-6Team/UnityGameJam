namespace UnityGameJam.Contract
{
    // A·B 레인이 함께 쓰는 enum. 바꿀 때는 docs/rules/collaboration.md 2절 절차를 따른다.

    /// <summary>판정 구역 9개. GDD §2 (로비는 해당 층 복도 포함, 1층 로비는 현관 포함).</summary>
    public enum EZoneId
    {
        Floor1Lobby,      // 1층 로비
        MultipurposeRoom, // 다목적실
        Lounge,           // 휴게실
        Storage,          // 창고
        Floor2Lobby,      // 2층 로비
        MeetingRoom,      // 회의실
        Workshop,         // 작업실
        Floor3Lobby,      // 3층 로비
        ExecutiveRoom,    // 간부실
    }

    public enum EFloor
    {
        Floor1,
        Floor2,
        Floor3,
    }

    /// <summary>오염 유형. GDD §3.1</summary>
    public enum EContaminationType
    {
        HeavyDebris, // 무거운 잔해
        Trash,       // 일반 쓰레기
        ClutterItem, // 어질러진 작은 물건
        FloorStain,  // 바닥 얼룩
    }

    /// <summary>작업자. 졸개는 종마다 1마리라서 졸개 ID로도 쓴다. GDD §4</summary>
    public enum EWorkerType
    {
        Player,
        Goblin,
        Orc,
        Skeleton,
        Slime,
        Hellhound,
    }

    /// <summary>오염 개체 상태. 집계 포함 여부는 GDD §7.2 표를 따른다.</summary>
    public enum EItemState
    {
        OnFloor,         // 바닥에 놓임 (집계 포함)
        Carried,         // 운반 중 (집계 제외)
        InDischargeArea, // 1층 현관 배출 구역에 적치 (1층 로비로 집계 포함)
        Dispatched,      // 헬하운드가 가져감 (제거)
        Digesting,       // 슬라임 소화 중 (집계 제외)
        Resolved,        // 처리 완료 (제자리 정리, 닦기, 소화 완료)
    }

    /// <summary>졸개 현황 패널에 보여 줄 현재 행동. GDD §5.2</summary>
    public enum EMinionActivity
    {
        LobbyIdle,        // 1층 로비 대기
        Talking,          // 대면 배정 중 (정지)
        MovingToRoom,     // 지정 방으로 이동
        Surveying,        // 현장 확인·탐색
        Working,          // 작업 중
        CarryingWaste,    // 폐기물 운반 중
        HandlingMop,      // 물걸레 대여·반납 이동
        AwaitingResponse, // 이동 제안 응답 대기 (정지)
        ReturningToLobby, // 1층 로비 복귀
        Dispatching,      // 헬하운드 배출 이동
        Recalling,        // 호통 집결지로 이동
    }

    public enum EMatchState
    {
        Ready,
        Running,
        DemonKingArrived, // 180초 도달. 강제로 내려놓고 판정하는 중
        Resolved,         // 판정 완료
    }

    /// <summary>우측 하단 메시지 종류. GDD §6</summary>
    public enum EMessageKind
    {
        Proposal, // 승인/보류가 필요한 이동 제안
        Notice,   // 응답이 필요 없는 복귀 통보
    }
}
