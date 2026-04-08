using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 씬에서 프로토타입 전투의 최상위 밸런스 값을 노출합니다.
    /// </summary>
    public sealed class BattleConfigAuthoring : MonoBehaviour
    {
        [Min(1f)] public float BattleDurationSeconds = 60f;
        [Min(1)] public int StartingLives = 3;
        [Min(0.05f)] public float PlayerMoveDuration = 0.22f;
        [Min(0.05f)] public float AttackInterval = 0.4f;
        [Min(0.05f)] public float SpawnInterval = 0.9f;
        public float EnemySpawnZ = 8.5f;
        public float DefenseLineZ = -3.5f;
    }

    /// <summary>
    /// 씬 설정 단계의 전투 설정값을 ECS 싱글턴 데이터로 변환합니다.
    /// </summary>
    public sealed class BattleConfigAuthoringBaker : Baker<BattleConfigAuthoring>
    {
        /// <summary>
        /// 런타임 시스템이 하나의 기준 설정을 읽을 수 있도록 불변 전투 설정을 베이킹 엔티티에 기록합니다.
        /// </summary>
        public override void Bake(BattleConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BattleConfig
            {
                BattleDurationSeconds = authoring.BattleDurationSeconds,
                StartingLives = authoring.StartingLives,
                PlayerMoveDuration = authoring.PlayerMoveDuration,
                AttackInterval = authoring.AttackInterval,
                SpawnInterval = authoring.SpawnInterval,
                EnemySpawnZ = authoring.EnemySpawnZ,
                DefenseLineZ = authoring.DefenseLineZ
            });
        }
    }
}
