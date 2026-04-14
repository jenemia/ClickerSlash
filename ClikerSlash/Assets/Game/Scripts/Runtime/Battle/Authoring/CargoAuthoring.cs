using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 모든 스폰 물류가 시작할 때 기준으로 삼는 프로토타입 원형 값을 노출합니다.
    /// </summary>
    public sealed class CargoAuthoring : MonoBehaviour
    {
        [Min(1)] public int StandardWeight = 6;
        [Min(1)] public int FragileWeight = 6;
        [Min(1)] public int HeavyWeight = 12;
        [Min(1)] public int Reward = 60;
        [Min(1)] public int Penalty = 35;
        public float Y = 0.6f;
        [Min(0.1f)] public float MoveSpeed = 1.92f;
    }

    /// <summary>
    /// 씬에 놓인 물류 프로토타입 값을 ECS 데이터로 베이크합니다.
    /// </summary>
    public sealed class CargoAuthoringBaker : Baker<CargoAuthoring>
    {
        /// <summary>
        /// 스폰 시스템이 일관된 기준값을 읽을 수 있도록 물류 기본값을 베이크된 엔티티에 복사합니다.
        /// </summary>
        public override void Bake(CargoAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new CargoConfig
            {
                StandardWeight = authoring.StandardWeight,
                FragileWeight = authoring.FragileWeight,
                HeavyWeight = authoring.HeavyWeight,
                Reward = authoring.Reward,
                Penalty = authoring.Penalty,
                Y = authoring.Y,
                MoveSpeed = authoring.MoveSpeed
            });
        }
    }
}
