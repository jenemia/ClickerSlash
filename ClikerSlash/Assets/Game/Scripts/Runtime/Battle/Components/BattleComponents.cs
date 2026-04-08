using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 프로토타입 전투 시스템 전반이 공유하는 전역 밸런스 값입니다.
    /// </summary>
    public struct BattleConfig : IComponentData
    {
        public float BattleDurationSeconds;
        public int StartingLives;
        public float PlayerMoveDuration;
        public float AttackInterval;
        public float SpawnInterval;
        public float EnemySpawnZ;
        public float DefenseLineZ;
    }

    /// <summary>
    /// 현재 전투 씬에서 설정된 플레이어 시작 배치 정보입니다.
    /// </summary>
    public struct PlayerConfig : IComponentData
    {
        public int InitialLane;
        public float Y;
        public float Z;
    }

    /// <summary>
    /// 스폰 시스템이 사용하는 적 기본 스탯입니다.
    /// </summary>
    public struct EnemyConfig : IComponentData
    {
        public int Health;
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
    /// 스폰된 적 런타임 엔티티임을 나타냅니다.
    /// </summary>
    public struct EnemyTag : IComponentData
    {
    }

    /// <summary>
    /// 플레이어 또는 적 엔티티의 현재 레인 인덱스를 저장합니다.
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
    /// 적처럼 이동하는 엔티티가 공통으로 사용하는 속도 값입니다.
    /// </summary>
    public struct MoveSpeed : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// 적이 방어선 쪽으로 전진할 때 사용하는 논리 Z축 진행도입니다.
    /// </summary>
    public struct VerticalPosition : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// 적 엔티티의 남은 체력을 저장합니다.
    /// </summary>
    public struct EnemyHealth : IComponentData
    {
        public int Value;
    }

    /// <summary>
    /// 다음 자동 공격이 가능해질 때까지 플레이어가 기다려야 하는 시간을 추적합니다.
    /// </summary>
    public struct AttackCooldown : IComponentData
    {
        public float Remaining;
    }

    /// <summary>
    /// 플레이어의 기본 자동 공격 주기를 정의합니다.
    /// </summary>
    public struct AutoAttackProfile : IComponentData
    {
        public float Interval;
    }

    /// <summary>
    /// 플레이어가 현재 레인에서 선택한 타깃을 저장합니다.
    /// </summary>
    public struct TargetSelectionState : IComponentData
    {
        public Entity Target;
    }

    /// <summary>
    /// 플레이어의 남은 라이프를 저장합니다.
    /// </summary>
    public struct LifeState : IComponentData
    {
        public int Value;
    }

    /// <summary>
    /// 현재 콤보와 이번 전투에서 기록한 최고 콤보를 함께 저장합니다.
    /// </summary>
    public struct ComboState : IComponentData
    {
        public int Current;
        public int Max;
    }

    /// <summary>
    /// 남은 전투 시간과 런타임 시스템이 전투 진행을 멈춰야 하는지 여부를 추적합니다.
    /// </summary>
    public struct StageProgressState : IComponentData
    {
        public float RemainingTime;
        // 0은 전투가 계속 진행 중인 상태, 1은 시스템이 더 이상 전투 상태를 변경하면 안 되는 상태를 뜻합니다.
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
    /// 전투 결과가 확정되었는지와 어느 쪽이 승리했는지를 저장합니다.
    /// </summary>
    public struct BattleOutcomeState : IComponentData
    {
        // 0은 결과가 아직 확정되지 않은 상태, 1은 승패 결과를 읽을 수 있는 상태를 뜻합니다.
        public byte HasOutcome;
        // 0은 패배, 1은 생존 성공에 의한 승리를 뜻합니다.
        public byte IsVictory;
    }

    /// <summary>
    /// 전투 종료 후 HUD와 허브 씬이 표시할 요약 통계를 모아 둡니다.
    /// </summary>
    public struct BattleSessionStatsState : IComponentData
    {
        public int KillCount;
        public int CurrentCombo;
        public int MaxCombo;
        public float SurvivalTimeSeconds;
        public int RemainingLives;
        // 0은 결과가 아직 핸드오프 스냅샷으로 복사되지 않은 상태, 1은 이미 복사된 상태를 뜻합니다.
        public byte HasSnapshot;
        // 0은 캡처된 스냅샷이 패배 결과임을, 1은 승리 결과임을 뜻합니다.
        public byte IsVictory;
    }

    /// <summary>
    /// 플레이어의 자동 공격이 적중했을 때 발행되는 이벤트 컴포넌트입니다.
    /// </summary>
    public struct AttackHitEvent : IComponentData
    {
        public Entity Target;
    }
}
