using System.Collections.Generic;
using UnityEngine;

namespace ClikerSlash.Battle
{
    public sealed class BattleViewAuthoring : MonoBehaviour
    {
        public Vector3 CameraPosition = new(0f, 10.6f, -16.8f);
        public Vector3 CameraRotation = new(31f, 0f, 0f);
        [Min(10f)] public float CameraFieldOfView = 34f;
        public List<float> LaneWorldXs = new() { -6f, -2f, 2f, 6f };
        [Min(0.1f)] public float LaneWidth = 3f;
        [Min(0.1f)] public float LaneLength = 15f;
        public float LaneCenterZ = 3f;
        [Min(0.1f)] public float LineVisualWidth = 16f;
        public float SpawnLineZ = 10.5f;
        public float DefenseLineZ = -4.5f;
        public float PlayerZ = -3f;
    }
}
