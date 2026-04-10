using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 메타 성장에서 집계된 작업자 핵심 스탯입니다.
    /// </summary>
    public struct WorkerProgressionStats : IComponentData
    {
        public float SessionDurationSeconds;
        public int MaxHandleWeight;
        public float LaneMoveDurationSeconds;
        public float TimingWindowHalfDepth;
    }

    /// <summary>
    /// 이번 세션의 규칙성 메타 값을 담는 싱글턴입니다.
    /// </summary>
    public struct SessionRuleState : IComponentData
    {
        public int ActiveLaneCount;
        public int PreviewCargoCount;
    }

    /// <summary>
    /// 메타 성장에서 집계된 경제 배율입니다.
    /// </summary>
    public struct EconomyModifier : IComponentData
    {
        public float RewardMultiplier;
        public float PenaltyMultiplier;
    }

    /// <summary>
    /// 자동화 해금 결과를 전투 런타임에 전달하는 싱글턴입니다.
    /// </summary>
    public struct AutomationProfile : IComponentData
    {
        public float ReturnBeltChance;
        // 0은 무게 미리보기 비활성, 1은 활성입니다.
        public byte HasWeightPreview;
        // 0은 보조 암 비활성, 1은 활성입니다.
        public byte HasAssistArm;
    }

    /// <summary>
    /// 로봇 해금 상태와 처리 한도를 전투 런타임에 전달하는 싱글턴입니다.
    /// </summary>
    public struct RobotProfile : IComponentData
    {
        // 0은 비활성, 1은 활성입니다.
        public byte HasLaneRobotAccess;
        // 0은 비활성, 1은 활성입니다.
        public byte HasDockRobotAccess;
        public int MaxHandleWeight;
        public int PrecisionTier;
    }

    /// <summary>
    /// 현재 세션이 어떤 메타 로드아웃으로 시작했는지 요약한 싱글턴입니다.
    /// </summary>
    public struct SkillLoadoutState : IComponentData
    {
        public int SchemaVersion;
        public int ResolvedLoadoutVersion;
        public int UnlockedNodeCount;
    }
}
