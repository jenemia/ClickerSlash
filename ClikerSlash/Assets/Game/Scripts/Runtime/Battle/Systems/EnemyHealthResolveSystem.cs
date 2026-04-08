using Unity.Collections;
using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 공격 적중 이벤트를 처리해 적을 제거하고 콤보 중심 세션 통계를 갱신합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AutoAttackSystem))]
    public partial struct EnemyHealthResolveSystem : ISystem
    {
        /// <summary>
        /// 적중 이벤트와 플레이어/세션 상태가 준비된 뒤 전투 결과 처리를 시작합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AttackHitEvent>();
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BattleSessionStatsState>();
        }

        /// <summary>
        /// 한 프레임 동안 적중한 적을 정리하고 콤보 및 처치 수를 갱신합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var resolvedTargets = new NativeParallelHashSet<Entity>(16, Allocator.Temp);
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var comboState = SystemAPI.GetComponentRW<ComboState>(playerEntity);
            var sessionStats = SystemAPI.GetSingletonRW<BattleSessionStatsState>();

            foreach (var (hitEvent, eventEntity) in SystemAPI.Query<RefRO<AttackHitEvent>>().WithEntityAccess())
            {
                // 같은 프레임에 여러 적중 이벤트가 같은 적을 가리킬 수 있으므로 중복 집계를 막습니다.
                if (entityManager.Exists(hitEvent.ValueRO.Target) && resolvedTargets.Add(hitEvent.ValueRO.Target))
                {
                    ecb.DestroyEntity(hitEvent.ValueRO.Target);
                    var nextCombo = comboState.ValueRO.Current + 1;
                    var nextMaxCombo = Unity.Mathematics.math.max(comboState.ValueRO.Max, nextCombo);
                    comboState.ValueRW.Current = nextCombo;
                    comboState.ValueRW.Max = nextMaxCombo;
                    sessionStats.ValueRW.KillCount += 1;
                    sessionStats.ValueRW.CurrentCombo = nextCombo;
                    sessionStats.ValueRW.MaxCombo = nextMaxCombo;
                }

                // 적중 이벤트는 단발성 메시지이므로 다음 업데이트까지 남기지 않고 즉시 제거합니다.
                ecb.DestroyEntity(eventEntity);
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
            resolvedTargets.Dispose();
        }
    }
}
