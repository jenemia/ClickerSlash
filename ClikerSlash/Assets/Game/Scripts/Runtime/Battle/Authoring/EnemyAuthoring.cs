using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 모든 스폰 적이 시작할 때 기준으로 삼는 프로토타입 적 원형 값을 노출합니다.
    /// </summary>
    public sealed class EnemyAuthoring : MonoBehaviour
    {
        [Min(1)] public int Health = 1;
        public float Y = 0.6f;
        [Min(0.1f)] public float MoveSpeed = 2.4f;
    }

    /// <summary>
    /// 씬에 놓인 적 프로토타입 값을 ECS 데이터로 베이크합니다.
    /// </summary>
    public sealed class EnemyAuthoringBaker : Baker<EnemyAuthoring>
    {
        /// <summary>
        /// 스폰 시스템이 일관된 기준값을 읽을 수 있도록 적 기본값을 베이크된 엔티티에 복사합니다.
        /// </summary>
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
