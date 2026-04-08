using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    public sealed class EnemyAuthoring : MonoBehaviour
    {
        [Min(1)] public int Health = 1;
        public float Y = 0.6f;
        [Min(0.1f)] public float MoveSpeed = 2.4f;
    }

    public sealed class EnemyAuthoringBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new EnemyConfig
            {
                Health = authoring.Health,
                Y = authoring.Y,
                MoveSpeed = authoring.MoveSpeed
            });
        }
    }
}
