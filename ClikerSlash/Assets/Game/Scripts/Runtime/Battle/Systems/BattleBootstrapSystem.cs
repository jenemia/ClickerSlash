using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct BattleBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<PlayerConfig>();
            state.RequireForUpdate<EnemyConfig>();
            state.RequireForUpdate<LaneLayout>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var configEntity = SystemAPI.GetSingletonEntity<BattleConfig>();
            if (state.EntityManager.HasComponent<BattleRuntimeInitializedTag>(configEntity))
            {
                return;
            }

            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var playerConfig = SystemAPI.GetSingleton<PlayerConfig>();
            var laneEntity = SystemAPI.GetSingletonEntity<LaneLayout>();
            var laneXs = state.EntityManager.GetBuffer<LaneWorldXElement>(laneEntity);
            var initialLane = BattleLaneUtility.ClampLane(playerConfig.InitialLane, laneXs.Length);
            var playerX = BattleLaneUtility.GetLaneX(laneXs, initialLane);

            state.EntityManager.AddComponentData(configEntity, new StageProgressState
            {
                RemainingTime = battleConfig.BattleDurationSeconds,
                IsFinished = 0
            });
            state.EntityManager.AddComponentData(configEntity, new SpawnTimerState
            {
                Remaining = battleConfig.SpawnInterval,
                Random = Unity.Mathematics.Random.CreateFromIndex(0x00C0FFEEu)
            });
            state.EntityManager.AddComponentData(configEntity, new BattleOutcomeState
            {
                HasOutcome = 0,
                IsVictory = 0
            });
            state.EntityManager.AddComponent<BattleRuntimeInitializedTag>(configEntity);

            var playerEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(playerEntity, new PlayerTag());
            state.EntityManager.AddComponentData(playerEntity, new LaneIndex { Value = initialLane });
            state.EntityManager.AddComponentData(playerEntity, new LaneMoveState
            {
                StartLane = initialLane,
                TargetLane = initialLane,
                Progress = 0f,
                IsMoving = 0
            });
            state.EntityManager.AddComponentData(playerEntity, new AttackCooldown { Remaining = 0f });
            state.EntityManager.AddComponentData(playerEntity, new AutoAttackProfile { Interval = battleConfig.AttackInterval });
            state.EntityManager.AddComponentData(playerEntity, new TargetSelectionState { Target = Entity.Null });
            state.EntityManager.AddComponentData(playerEntity, new LifeState { Value = battleConfig.StartingLives });
            state.EntityManager.AddComponentData(playerEntity, LocalTransform.FromPositionRotationScale(
                new float3(playerX, playerConfig.Y, playerConfig.Z),
                quaternion.identity,
                1f));
            state.EntityManager.AddBuffer<LaneMoveCommandBufferElement>(playerEntity);
        }
    }
}
