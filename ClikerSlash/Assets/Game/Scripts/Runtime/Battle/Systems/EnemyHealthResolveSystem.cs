using Unity.Collections;
using Unity.Entities;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AutoAttackSystem))]
    public partial struct EnemyHealthResolveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AttackHitEvent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (hitEvent, eventEntity) in SystemAPI.Query<RefRO<AttackHitEvent>>().WithEntityAccess())
            {
                if (entityManager.Exists(hitEvent.ValueRO.Target))
                {
                    ecb.DestroyEntity(hitEvent.ValueRO.Target);
                }

                ecb.DestroyEntity(eventEntity);
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }
}
