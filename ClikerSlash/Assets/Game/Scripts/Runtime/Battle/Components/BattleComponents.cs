using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 프로토타입 물류 세션이 공유하는 전역 밸런스 값입니다.
    /// </summary>
    public struct BattleConfig : IComponentData
    {
        public float BaseWorkDurationSeconds;
        public float HealthDurationBonusSeconds;
        public float PlayerMoveDuration;
        public float HandleDurationSeconds;
        public float SpawnInterval;
        public float CargoSpawnZ;
        public float JudgmentLineZ;
        public float FailLineZ;
        public float HandleWindowHalfDepth;
        public int StartingMaxHandleWeight;
    }

    /// <summary>
    /// 현재 세션에서 설정된 플레이어 시작 배치 정보입니다.
    /// </summary>
    public struct PlayerConfig : IComponentData
    {
        public int InitialLane;
        public float Y;
        public float Z;
    }

    /// <summary>
    /// 스폰 시스템이 사용하는 기본 물류 원형 값입니다.
    /// </summary>
    public struct CargoConfig : IComponentData
    {
        public int StandardWeight;
        public int FragileWeight;
        public int HeavyWeight;
        public int Reward;
        public int Penalty;
        public float Y;
        public float MoveSpeed;
    }

    /// <summary>
    /// 이동 범위 보정과 스폰 레인 선택에 쓰이는 레인 개수 메타데이터입니다.
    /// </summary>
    public struct LaneLayout : IComponentData
    {
        public int LaneCount;
    }

    /// <summary>
    /// 레인 번호를 월드 공간 X 위치로 매핑하는 인덱스 기반 좌표 버퍼입니다.
    /// </summary>
    public struct LaneWorldXElement : IBufferElementData
    {
        public float Value;
    }

    /// <summary>
    /// 전투 부트스트랩 시스템이 이 설정 엔티티에 대한 런타임 엔티티 생성을 이미 마쳤음을 표시합니다.
    /// </summary>
    public struct BattleRuntimeInitializedTag : IComponentData
    {
    }

    /// <summary>
    /// 플레이어가 제어하는 런타임 엔티티임을 나타냅니다.
    /// </summary>
    public struct PlayerTag : IComponentData
    {
    }

    /// <summary>
    /// 스폰된 물류 런타임 엔티티임을 나타냅니다.
    /// </summary>
    public struct CargoTag : IComponentData
    {
    }

    /// <summary>
    /// 레인 물류가 상하차 큐에 전달할 기본 분류를 저장합니다.
    /// </summary>
    public struct CargoKind : IComponentData
    {
        public LoadingDockCargoKind Value;
    }

    /// <summary>
    /// 플레이어 또는 물류 엔티티의 현재 레인 인덱스를 저장합니다.
    /// </summary>
    public struct LaneIndex : IComponentData
    {
        public int Value;
    }

    /// <summary>
    /// 플레이어의 현재 레인 전환 상태를 추적합니다.
    /// </summary>
    public struct LaneMoveState : IComponentData
    {
        public int StartLane;
        public int TargetLane;
        public float Progress;
        // 0은 현재 레인에 정착한 상태, 1은 레인 보간 이동이 아직 진행 중인 상태를 뜻합니다.
        public byte IsMoving;
    }

    /// <summary>
    /// 플레이어 입력에서 수집한 좌우 레인 변경 요청 버퍼입니다.
    /// </summary>
    public struct LaneMoveCommandBufferElement : IBufferElementData
    {
        public int Direction;
    }

    /// <summary>
    /// 물류 이동 속도 값입니다.
    /// </summary>
    public struct MoveSpeed : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// 물류가 세션 화면 안에서 얼마나 내려왔는지 추적합니다.
    /// </summary>
    public struct VerticalPosition : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// 플레이어가 처리할 수 있는 최대 물류 무게를 저장합니다.
    /// </summary>
    public struct MaxHandleWeight : IComponentData
    {
        public int Value;
    }

    /// <summary>
    /// 플레이어가 다음 물류를 다시 처리할 수 있는 시점을 저장합니다.
    /// </summary>
    public struct HandleState : IComponentData
    {
        public double BusyUntilTime;
    }

    /// <summary>
    /// 물류의 무게를 저장합니다.
    /// </summary>
    public struct CargoWeight : IComponentData
    {
        public int Value;
    }

    /// <summary>
    /// 물류 처리 성공 시 획득할 돈을 저장합니다.
    /// </summary>
    public struct CargoReward : IComponentData
    {
        public int Value;
    }

    /// <summary>
    /// 물류 처리 실패 시 잃는 돈을 저장합니다.
    /// </summary>
    public struct CargoPenalty : IComponentData
    {
        public int Value;
    }

    /// <summary>
    /// 현재 콤보와 이번 세션에서 기록한 최고 콤보를 함께 저장합니다.
    /// </summary>
    public struct ComboState : IComponentData
    {
        public int Current;
        public int Max;
    }

    /// <summary>
    /// 남은 작업 시간, 경과 시간, 종료 여부를 추적합니다.
    /// </summary>
    public struct StageProgressState : IComponentData
    {
        public float RemainingWorkTime;
        public float ElapsedWorkTime;
        // 0은 세션이 계속 진행 중인 상태, 1은 시스템이 더 이상 세션 상태를 변경하면 안 되는 상태를 뜻합니다.
        public byte IsFinished;
    }

    /// <summary>
    /// 다음 스폰 시각과 레인 선택용 난수 상태를 추적합니다.
    /// </summary>
    public struct SpawnTimerState : IComponentData
    {
        public float Remaining;
        public Unity.Mathematics.Random Random;
    }

    /// <summary>
    /// 세션 결과가 확정되었는지 여부를 저장합니다.
    /// </summary>
    public struct BattleOutcomeState : IComponentData
    {
        // 0은 결과가 아직 확정되지 않은 상태, 1은 세션 종료 결과를 읽을 수 있는 상태를 뜻합니다.
        public byte HasOutcome;
    }

    /// <summary>
    /// 세션 종료 후 HUD와 허브 씬이 표시할 요약 통계를 모아 둡니다.
    /// </summary>
    public struct BattleSessionStatsState : IComponentData
    {
        public int TotalMoney;
        public int ProcessedCargoCount;
        public int MissedCargoCount;
        public int CurrentCombo;
        public int MaxCombo;
        public float WorkedTimeSeconds;
        public float ResolvedWorkDurationSeconds;
        // 0은 결과가 아직 핸드오프 스냅샷으로 복사되지 않은 상태, 1은 이미 복사된 상태를 뜻합니다.
        public byte HasSnapshot;
    }

    /// <summary>
    /// 물류 처리 성공 시 발행되는 이벤트 컴포넌트입니다.
    /// </summary>
    public struct CargoHandledEvent : IComponentData
    {
        public int Reward;
        public LoadingDockCargoKind Kind;
        public int Weight;
    }

    /// <summary>
    /// 물류 처리 실패 시 발행되는 이벤트 컴포넌트입니다.
    /// </summary>
    public struct CargoMissedEvent : IComponentData
    {
        public int Penalty;
    }

    /// <summary>
    /// 레인에 배치되는 보조 로봇 엔티티임을 나타냅니다.
    /// </summary>
    public struct LaneRobotTag : IComponentData
    {
    }

    /// <summary>
    /// 레인 로봇의 배치 상태를 저장합니다.
    /// </summary>
    public struct LaneRobotState : IComponentData
    {
        public int AssignedLane;
        // 0은 아직 미배치, 1은 세션 중 배치가 확정된 상태를 뜻합니다.
        public byte IsAssigned;
    }

    /// <summary>
    /// 레인 로봇과 Dock 로봇이 공통으로 사용하는 처리 가능 여부 판정 유틸리티입니다.
    /// </summary>
    public static class RobotHandlingRules
    {
        /// <summary>
        /// 현재 로봇 스펙으로 지정한 물류를 처리할 수 있는지 계산합니다.
        /// </summary>
        public static bool CanHandle(int maxHandleWeight, int precisionTier, LoadingDockCargoKind kind, int weight)
        {
            if (weight > maxHandleWeight)
            {
                return false;
            }

            return kind switch
            {
                LoadingDockCargoKind.Fragile => precisionTier >= 1,
                _ => true
            };
        }
    }
}
