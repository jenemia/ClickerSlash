using Unity.Entities;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TargetSelectionSystem))]
    public partial struct AutoAttackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<StageProgressState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var deltaTime = SystemAPI.Time.DeltaTime;
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (cooldown, attackProfile, moveState, targetState) in SystemAPI
                         .Query<RefRW<AttackCooldown>, RefRO<AutoAttackProfile>, RefRO<LaneMoveState>, RefRW<TargetSelectionState>>()
                         .WithAll<PlayerTag>())
            {
                cooldown.ValueRW.Remaining = Unity.Mathematics.math.max(0f, cooldown.ValueRO.Remaining - deltaTime);

                if (moveState.ValueRO.IsMoving != 0)
                {
                    continue;
                }

                var target = targetState.ValueRO.Target;
                if (target == Entity.Null || !entityManager.Exists(target) || cooldown.ValueRO.Remaining > 0f)
                {
                    continue;
                }

                var hitEvent = ecb.CreateEntity();
                ecb.AddComponent(hitEvent, new AttackHitEvent { Target = target });
                cooldown.ValueRW.Remaining = attackProfile.ValueRO.Interval;
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }
}
