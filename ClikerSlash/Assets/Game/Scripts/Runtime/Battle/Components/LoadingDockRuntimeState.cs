namespace ClikerSlash.Battle
{
    /// <summary>
    /// 전투 루프 전체가 공유하는 기본 물류 분류입니다.
    /// </summary>
    public enum LoadingDockCargoKind
    {
        General = 0,
        Standard = 0,
        Fragile = 1,
        Frozen = 2,
        Heavy = 2
    }

    /// <summary>
    /// 현재 진행 중인 메인 미니게임 phase입니다.
    /// </summary>
    public enum BattleMiniGamePhase
    {
        Approval = 0,
        RouteSelection = 1,
        Completed = 2
    }

    /// <summary>
    /// 승인 미니게임의 불/가 판정 결과입니다.
    /// </summary>
    public enum ApprovalDecision
    {
        None = 0,
        Reject = 1,
        Approve = 2
    }

    /// <summary>
    /// 레인선택 미니게임의 출력 라인입니다.
    /// </summary>
    public enum CargoRouteLane
    {
        Air = 0,
        Sea = 1,
        Rail = 2,
        Truck = 3,
        Return = 4
    }

    /// <summary>
    /// 현재 세션의 phase 진행 요약입니다.
    /// </summary>
    public struct BattleMiniGamePhaseSnapshot
    {
        public BattleMiniGamePhase CurrentPhase;
        public bool HasActiveCargo;
        public int PendingApprovalCount;
        public int PendingRouteCount;
        public int DeliveryLaneMaxWeight;
    }

    /// <summary>
    /// 승인 큐에서 사용되는 단일 물류 스냅샷입니다.
    /// </summary>
    public struct ApprovalCargoSnapshot
    {
        public int EntryId;
        public LoadingDockCargoKind Kind;
        public int Weight;
        public int Reward;
        public int Penalty;
    }

    /// <summary>
    /// 승인 결과가 반영되어 레인선택 phase로 전달된 물류 스냅샷입니다.
    /// </summary>
    public struct RouteSelectionCargoSnapshot
    {
        public int EntryId;
        public LoadingDockCargoKind Kind;
        public int Weight;
        public int Reward;
        public int Penalty;
        public ApprovalDecision ApprovalDecision;
        public bool IsDeliverable;
    }

    /// <summary>
    /// 레거시 프레젠터 호환용 작업 구역 표시입니다.
    /// </summary>
    public enum WorkAreaType
    {
        Lane = 0,
        LoadingDock = 1
    }

    /// <summary>
    /// 레거시 프레젠터 호환용 작업 구역 전환 단계입니다.
    /// </summary>
    public enum WorkAreaTransitionPhase
    {
        None = 0,
        EnteringLoadingDock = 1,
        ActiveInLoadingDock = 2,
        ReturningToLane = 3
    }

    /// <summary>
    /// 레거시 상하차 프레젠터 호환용 런타임 스냅샷입니다.
    /// </summary>
    public struct LoadingDockRuntimeState
    {
        public bool HasLoadingDockAccess;
        public WorkAreaType CurrentArea;
        public WorkAreaTransitionPhase TransitionPhase;
        public bool HasPendingEntryRequest;
        public bool HasPendingReturnRequest;
    }

    /// <summary>
    /// 상하차 세션 큐에 쌓인 단일 물류 엔트리 스냅샷입니다.
    /// </summary>
    public struct LoadingDockCargoQueueEntry
    {
        public int EntryId;
        public LoadingDockCargoKind Kind;
        public int Weight;
    }

    /// <summary>
    /// 상하차 적재장의 고정 slot과 엔트리 매핑을 노출하는 런타임 스냅샷입니다.
    /// </summary>
    public struct LoadingDockActiveCargoSlotSnapshot
    {
        public int SlotIndex;
        public int EntryId;
        public LoadingDockCargoKind Kind;
        public int Weight;
    }

    /// <summary>
    /// 현재 상하차 큐의 backlog/활성 슬롯 상태를 요약합니다.
    /// </summary>
    public struct LoadingDockQueueSnapshot
    {
        public int BacklogCount;
        public int ActiveSlotCount;
        public int MaxActiveSlotCount;
        public int TotalCount;
    }
}
