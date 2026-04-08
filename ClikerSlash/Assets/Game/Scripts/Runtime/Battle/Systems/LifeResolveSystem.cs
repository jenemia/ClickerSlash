using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 방어선을 넘은 적을 감지해 제거하고 라이프 및 콤보 패널티를 적용합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyHealthResolveSystem))]
    public partial struct LifeResolveSystem : ISystem
    {
        /// <summary>
        /// 돌파 처리 전에 활성 전투 상태, 적 존재 여부, 세션 통계가 모두 준비되어야 합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<EnemyTag>();
            state.RequireForUpdate<StageProgressState>();
            state.RequireForUpdate<BattleSessionStatsState>();
        }

        /// <summary>
        /// 이번 프레임에 감지된 모든 방어선 돌파를 적용하고, 돌파가 있었다면 현재 콤보를 초기화합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var defenseLineZ = SystemAPI.GetSingleton<BattleConfig>().DefenseLineZ;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerLife = SystemAPI.GetComponentRW<LifeState>(playerEntity);
            var comboState = SystemAPI.GetComponentRW<ComboState>(playerEntity);
            var sessionStats = SystemAPI.GetSingletonRW<BattleSessionStatsState>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var lifeLost = 0;

            foreach (var (enemyTransform, enemyEntity) in SystemAPI
                         .Query<RefRO<LocalTransform>>()
                         .WithAll<EnemyTag>()
                         .WithEntityAccess())
            {
                if (enemyTransform.ValueRO.Position.z > defenseLineZ)
                {
                    continue;
                }

                lifeLost += 1;
                ecb.DestroyEntity(enemyEntity);
            }

            if (lifeLost > 0)
            {
                // 한 번이라도 돌파가 발생하면 라이프를 깎고 현재 콤보 흐름도 끊습니다.
                playerLife.ValueRW.Value = Unity.Mathematics.math.max(0, playerLife.ValueRO.Value - lifeLost);
                comboState.ValueRW.Current = 0;
                sessionStats.ValueRW.CurrentCombo = 0;
            }

            // 공용 세션 요약 상태가 실제 플레이어 라이프 값과 항상 같도록 맞춰 둡니다.
            sessionStats.ValueRW.RemainingLives = playerLife.ValueRO.Value;

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
