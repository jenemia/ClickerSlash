namespace ClikerSlash.Battle
{
    /// <summary>
    /// 상하차 큐와 레인 물류가 공유하는 기본 물류 분류입니다.
    /// </summary>
    public enum LoadingDockCargoKind
    {
        Standard = 0,
        Fragile = 1,
        Heavy = 2
    }

    /// <summary>
    /// 플레이어가 현재 점유 중인 작업 구역입니다.
    /// </summary>
    public enum WorkAreaType
    {
        Lane = 0,
        LoadingDock = 1
    }

    /// <summary>
    /// 상하차 구역 전환 단계입니다.
    /// </summary>
    public enum WorkAreaTransitionPhase
    {
        None = 0,
        EnteringLoadingDock = 1,
        ActiveInLoadingDock = 2,
        ReturningToLane = 3
    }

    /// <summary>
    /// 상하차 구역 진입/복귀 계약을 표현하는 런타임 스냅샷입니다.
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
