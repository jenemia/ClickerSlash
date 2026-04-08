using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
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

    public sealed class BattleConfigAuthoringBaker : Baker<BattleConfigAuthoring>
    {
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
