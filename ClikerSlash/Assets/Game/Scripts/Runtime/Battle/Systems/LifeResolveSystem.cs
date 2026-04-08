using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyHealthResolveSystem))]
    public partial struct LifeResolveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<EnemyTag>();
            state.RequireForUpdate<StageProgressState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var defenseLineZ = SystemAPI.GetSingleton<BattleConfig>().DefenseLineZ;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerLife = SystemAPI.GetComponentRW<LifeState>(playerEntity);
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
                playerLife.ValueRW.Value = Unity.Mathematics.math.max(0, playerLife.ValueRO.Value - lifeLost);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
