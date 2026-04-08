using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    public sealed class PlayerAuthoring : MonoBehaviour
    {
        [Range(0, 3)] public int InitialLane = 1;
        public float Y = 0.6f;
        public float Z = -2.4f;
    }

    public sealed class PlayerAuthoringBaker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PlayerConfig
            {
                InitialLane = authoring.InitialLane,
                Y = authoring.Y,
                Z = authoring.Z
            });
        }
    }
}
