using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 씬 설정 기반 싱글턴이 준비되면 런타임 전투 엔티티를 최초로 구성합니다.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct BattleBootstrapSystem : ISystem
    {
        /// <summary>
        /// 베이크된 설정 싱글턴이 모두 존재할 때까지 전투 상태 초기화를 보류합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<PlayerConfig>();
            state.RequireForUpdate<EnemyConfig>();
            state.RequireForUpdate<LaneLayout>();
        }

        /// <summary>
        /// 씬 부트스트랩당 한 번만 전투 싱글턴 상태와 플레이어 런타임 엔티티를 생성합니다.
        /// </summary>
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

            // 후속 시스템이 한 기준 엔티티에서 읽을 수 있도록 전투 싱글턴 상태를 설정 엔티티에 붙입니다.
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
            state.EntityManager.AddComponentData(configEntity, new BattleSessionStatsState
            {
                KillCount = 0,
                CurrentCombo = 0,
                MaxCombo = 0,
                SurvivalTimeSeconds = 0f,
                RemainingLives = battleConfig.StartingLives,
                HasSnapshot = 0,
                IsVictory = 0
            });
            state.EntityManager.AddComponent<BattleRuntimeInitializedTag>(configEntity);

            // 기본 이동, 공격, 세션 추적 상태를 가진 단일 플레이어 런타임 엔티티를 생성합니다.
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
            state.EntityManager.AddComponentData(playerEntity, new ComboState { Current = 0, Max = 0 });
            state.EntityManager.AddComponentData(playerEntity, LocalTransform.FromPositionRotationScale(
                new float3(playerX, playerConfig.Y, playerConfig.Z),
                quaternion.identity,
                1f));
            state.EntityManager.AddBuffer<LaneMoveCommandBufferElement>(playerEntity);
        }
    }
}
