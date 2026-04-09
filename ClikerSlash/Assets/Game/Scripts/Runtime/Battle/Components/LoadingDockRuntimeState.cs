namespace ClikerSlash.Battle
{
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
    /// 상하차 라운드 종료 시 남기는 결과 스냅샷입니다.
    /// </summary>
    public struct LoadingDockResultSnapshot
    {
        public int DeliveredCargoCount;
        public int TotalCargoCount;
        public bool CompletedSuccessfully;
    }
}
