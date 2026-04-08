using Unity.Entities;

namespace ClikerSlash.Battle
{
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

    public struct PlayerConfig : IComponentData
    {
        public int InitialLane;
        public float Y;
        public float Z;
    }

    public struct EnemyConfig : IComponentData
    {
        public int Health;
        public float Y;
        public float MoveSpeed;
    }

    public struct LaneLayout : IComponentData
    {
        public int LaneCount;
    }

    public struct LaneWorldXElement : IBufferElementData
    {
        public float Value;
    }

    public struct BattleRuntimeInitializedTag : IComponentData
    {
    }

    public struct PlayerTag : IComponentData
    {
    }

    public struct EnemyTag : IComponentData
    {
    }

    public struct LaneIndex : IComponentData
    {
        public int Value;
    }

    public struct LaneMoveState : IComponentData
    {
        public int StartLane;
        public int TargetLane;
        public float Progress;
        public byte IsMoving;
    }

    public struct LaneMoveCommandBufferElement : IBufferElementData
    {
        public int Direction;
    }

    public struct MoveSpeed : IComponentData
    {
        public float Value;
    }

    public struct VerticalPosition : IComponentData
    {
        public float Value;
    }

    public struct EnemyHealth : IComponentData
    {
        public int Value;
    }

    public struct AttackCooldown : IComponentData
    {
        public float Remaining;
    }

    public struct AutoAttackProfile : IComponentData
    {
        public float Interval;
    }

    public struct TargetSelectionState : IComponentData
    {
        public Entity Target;
    }

    public struct LifeState : IComponentData
    {
        public int Value;
    }

    public struct StageProgressState : IComponentData
    {
        public float RemainingTime;
        public byte IsFinished;
    }

    public struct SpawnTimerState : IComponentData
    {
        public float Remaining;
        public Unity.Mathematics.Random Random;
    }

    public struct BattleOutcomeState : IComponentData
    {
        public byte HasOutcome;
        public byte IsVictory;
    }

    public struct AttackHitEvent : IComponentData
    {
        public Entity Target;
    }
}
