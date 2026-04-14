using Unity.Entities;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 모든 컨베이어 물류를 이동시키되 판정선에 도달하면 그 자리에서 대기시킵니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoSpawnSystem))]
    public partial struct CargoMoveSystem : ISystem
    {
        /// <summary>
        /// 세션 진행 상태와 전역 전투 설정이 준비된 뒤에만 이동을 수행합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 비포커스 구역도 계속 흐르지만 판정이 필요한 순간에는 자동으로 판정선에 정지시킵니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var deltaTime = SystemAPI.Time.DeltaTime;
            var judgmentLineZ = SystemAPI.GetSingleton<BattleConfig>().JudgmentLineZ;

            foreach (var (transform, moveSpeed, verticalPosition, cargoPhase) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRO<MoveSpeed>, RefRW<VerticalPosition>, RefRO<CargoMiniGamePhase>>()
                         .WithAll<CargoTag>())
            {
                var nextZ = verticalPosition.ValueRO.Value - moveSpeed.ValueRO.Value * deltaTime;
                var area = cargoPhase.ValueRO.Value switch
                {
                    BattleMiniGamePhase.Approval => BattleMiniGameArea.Approval,
                    BattleMiniGamePhase.RouteSelection => BattleMiniGameArea.RouteSelection,
                    _ => BattleMiniGameArea.LoadingDock
                };

                // 사람 입력이 필요한 구역은 판정선에서 멈춘 채 포커스가 돌아오기를 기다립니다.
                if (PrototypeSessionRuntime.ShouldHoldAtJudgment(area) && nextZ <= judgmentLineZ)
                {
                    nextZ = judgmentLineZ;
                }

                verticalPosition.ValueRW.Value = nextZ;
                var position = transform.ValueRO.Position;
                position.z = nextZ;
                transform.ValueRW.Position = position;
            }
        }
    }
}
