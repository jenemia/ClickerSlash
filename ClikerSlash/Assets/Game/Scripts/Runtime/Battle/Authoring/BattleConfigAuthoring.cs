using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 씬에서 프로토타입 물류 세션의 최상위 밸런스 값을 노출합니다.
    /// </summary>
    public sealed class BattleConfigAuthoring : MonoBehaviour
    {
        [FormerlySerializedAs("BattleDurationSeconds")]
        [Min(5f)] public float BaseWorkDurationSeconds = PrototypeSessionRuntime.DefaultBaseWorkDurationSeconds;
        [Min(0f)] public float HealthDurationBonusSeconds = PrototypeSessionRuntime.DefaultHealthDurationBonusSeconds;
        [Min(0.05f)] public float PlayerMoveDuration = 0.22f;
        [FormerlySerializedAs("AttackInterval")]
        [Min(0.05f)] public float HandleDurationSeconds = 0.4f;
        [Min(0.05f)] public float SpawnInterval = 1.08f;
        [FormerlySerializedAs("EnemySpawnZ")]
        public float CargoSpawnZ = 8.5f;
        public float ApprovalLaneX = 18f;
        public float RouteLaneX = 0f;
        [Min(0.05f)] public float HandleWindowHalfDepth = 0.45f;
        public float JudgmentLineZ = -2.8f;
        [FormerlySerializedAs("DefenseLineZ")]
        public float FailLineZ = -3.8f;
        [Min(1)] public int StartingMaxHandleWeight = 10;
        [Min(1)] public int DeliveryLaneMaxWeight = PrototypeSessionRuntime.DefaultDeliveryLaneMaxWeight;
    }

    /// <summary>
    /// 씬 설정 단계의 물류 세션 설정값을 ECS 싱글턴 데이터로 변환합니다.
    /// </summary>
    public sealed class BattleConfigAuthoringBaker : Baker<BattleConfigAuthoring>
    {
        /// <summary>
        /// 런타임 시스템이 하나의 기준 설정을 읽을 수 있도록 불변 세션 설정을 베이킹 엔티티에 기록합니다.
        /// </summary>
        public override void Bake(BattleConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BattleConfig
            {
                BaseWorkDurationSeconds = authoring.BaseWorkDurationSeconds,
                HealthDurationBonusSeconds = authoring.HealthDurationBonusSeconds,
                PlayerMoveDuration = authoring.PlayerMoveDuration,
                HandleDurationSeconds = authoring.HandleDurationSeconds,
                SpawnInterval = authoring.SpawnInterval,
                CargoSpawnZ = authoring.CargoSpawnZ,
                ApprovalLaneX = authoring.ApprovalLaneX,
                RouteLaneX = authoring.RouteLaneX,
                JudgmentLineZ = authoring.JudgmentLineZ,
                FailLineZ = authoring.FailLineZ,
                HandleWindowHalfDepth = authoring.HandleWindowHalfDepth,
                StartingMaxHandleWeight = authoring.StartingMaxHandleWeight,
                DeliveryLaneMaxWeight = authoring.DeliveryLaneMaxWeight
            });
        }
    }
}
