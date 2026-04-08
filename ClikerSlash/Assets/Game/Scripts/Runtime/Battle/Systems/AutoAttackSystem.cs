using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 쿨다운이 준비되면 플레이어가 현재 선택한 레인 타깃에게 공격 적중 이벤트를 발행합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TargetSelectionSystem))]
    public partial struct AutoAttackSystem : ISystem
    {
        /// <summary>
        /// 전투가 초기화된 뒤에만 자동 공격 갱신이 돌도록 요구 조건을 설정합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 공격 쿨다운을 진행시키고, 플레이어가 정지 상태이며 유효한 타깃이 있을 때 적중 이벤트를 발행합니다.
        /// </summary>
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
                // 유효한 타깃이 없어도 쿨다운은 계속 감소시켜 시스템이 스스로 상태를 완결되게 유지합니다.
                cooldown.ValueRW.Remaining = Unity.Mathematics.math.max(0f, cooldown.ValueRO.Remaining - deltaTime);

                if (moveState.ValueRO.IsMoving != 0)
                {
                    continue;
                }

                var target = targetState.ValueRO.Target;
                // 살아 있는 타깃이 없거나 이전 공격의 회복 시간이 남아 있으면 이번 프레임 공격을 중단합니다.
                if (target == Entity.Null || !entityManager.Exists(target) || cooldown.ValueRO.Remaining > 0f)
                {
                    continue;
                }

                // 실제 적 제거는 짧은 수명의 이벤트 엔티티를 통해 판정 처리 단계로 넘깁니다.
                var hitEvent = ecb.CreateEntity();
                ecb.AddComponent(hitEvent, new AttackHitEvent { Target = target });
                cooldown.ValueRW.Remaining = attackProfile.ValueRO.Interval;
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }
}
