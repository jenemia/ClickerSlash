using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 실패선을 지난 물류를 감지해 리듬 미스와 패널티를 반영합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoHandleSystem))]
    public partial struct CargoMissSystem : ISystem
    {
        /// <summary>
        /// 실패 처리 전에 세션 상태와 설정이 준비되어야 합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 실패선을 통과한 모든 물류를 정리하고 돈 손실 이벤트를 남깁니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var failLineZ = SystemAPI.GetSingleton<BattleConfig>().FailLineZ;
            var entityManager = state.EntityManager;
            var stats = SystemAPI.GetSingletonRW<BattleSessionStatsState>();
            var rhythmPhaseState = SystemAPI.GetSingletonRW<RhythmPhaseState>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (cargoPenalty, cargoPhase, cargoTransform, cargoEntity) in SystemAPI
                         .Query<RefRO<CargoPenalty>, RefRO<CargoMiniGamePhase>, RefRO<LocalTransform>>()
                         .WithAll<CargoTag>()
                         .WithEntityAccess())
            {
                if (cargoTransform.ValueRO.Position.z > failLineZ)
                {
                    continue;
                }

                if (cargoPhase.ValueRO.Value == BattleMiniGamePhase.RouteSelection)
                {
                    stats.ValueRW.TotalMoney -= cargoPenalty.ValueRO.Value;
                    stats.ValueRW.MisrouteCount += 1;
                    stats.ValueRW.CurrentCombo = 0;
                }
                else
                {
                    stats.ValueRW.MissedCargoCount += 1;
                }

                ecb.DestroyEntity(cargoEntity);
                PrototypeSessionRuntime.NotifyActiveCargoResolved();
                var runtimeSnapshot = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot();
                rhythmPhaseState.ValueRW.CurrentPhase = runtimeSnapshot.CurrentPhase;
                rhythmPhaseState.ValueRW.PendingApprovalCount = runtimeSnapshot.PendingApprovalCount;
                rhythmPhaseState.ValueRW.PendingRouteCount = runtimeSnapshot.PendingRouteCount;
                rhythmPhaseState.ValueRW.HasActiveCargo = runtimeSnapshot.HasActiveCargo ? (byte)1 : (byte)0;
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }
}
