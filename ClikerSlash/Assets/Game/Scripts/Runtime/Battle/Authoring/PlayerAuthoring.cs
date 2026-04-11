using Unity.Mathematics;
using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 프로토타입 전투 씬에서 플레이어의 시작 레인과 월드 위치를 노출합니다.
    /// </summary>
    public sealed class PlayerAuthoring : MonoBehaviour
    {
        [Range(0, 3)] public int InitialLane = 1;
    }

    /// <summary>
    /// 씬 설정 단계의 플레이어 시작 설정을 ECS 구성 데이터로 변환합니다.
    /// </summary>
    public sealed class PlayerAuthoringBaker : Baker<PlayerAuthoring>
    {
        /// <summary>
        /// 플레이어의 시작 레인과 스폰 좌표를 베이크된 설정 엔티티에 복사합니다.
        /// </summary>
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PlayerConfig
            {
                InitialLane = authoring.InitialLane,
                WorldPosition = (float3)authoring.transform.position
            });
        }
    }
}
