using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 전투 무대와 3구역 카메라 기본 시점을 씬에서 조정할 수 있게 노출합니다.
    /// </summary>
    public sealed class BattleViewAuthoring : MonoBehaviour
    {
        [Header("레인 카메라")]
        [Tooltip("레인선택 구역을 비추는 카메라 위치입니다.")]
        public Vector3 CameraPosition = new(0f, 10.6f, -16.8f);
        [Tooltip("레인선택 구역 카메라 회전값입니다.")]
        public Vector3 CameraRotation = new(31f, 0f, 0f);
        [Tooltip("세 virtual camera가 공통으로 사용할 기본 시야각입니다.")]
        [Min(10f)] public float CameraFieldOfView = 34f;

        [Header("승인 카메라")]
        [Tooltip("승인 구역을 비추는 카메라 위치입니다.")]
        public Vector3 ApprovalCameraPosition = new(13.6f, 10.4f, -12.2f);
        [Tooltip("승인 구역 카메라 회전값입니다.")]
        public Vector3 ApprovalCameraRotation = new(35f, 14f, 0f);

        [Header("상하차 카메라")]
        [Tooltip("상하차 구역을 비추는 카메라 위치입니다.")]
        public Vector3 LoadingDockCameraPosition = new(13.64f, 11.22f, -9.6f);
        [Tooltip("상하차 구역 카메라 회전값입니다.")]
        public Vector3 LoadingDockCameraRotation = new(39.583f, 18f, 0f);

        [Header("레인 배치")]
        [Tooltip("레인 중심 X 좌표 목록입니다.")]
        public List<float> LaneWorldXs = new() { -8f, -4f, 0f, 4f, 8f };
        [Tooltip("한 레인의 시각 폭입니다.")]
        [Min(0.1f)] public float LaneWidth = 2.4f;
        [Tooltip("한 레인의 시각 길이입니다.")]
        [Min(0.1f)] public float LaneLength = 15f;
        [Tooltip("레인 바닥이 놓일 중심 Z 좌표입니다.")]
        public float LaneCenterZ = 3f;
        [Tooltip("스폰/판정/실패 라인 시각 폭입니다.")]
        [Min(0.1f)] public float LineVisualWidth = 22f;

        [Header("판정 라인")]
        [Tooltip("물류가 처음 스폰되는 Z 좌표입니다.")]
        [FormerlySerializedAs("SpawnLineZ")]
        public float CargoSpawnZ = 10.5f;
        [Tooltip("승인/레인선택 물류가 멈춰 서는 판정선 Z 좌표입니다.")]
        public float JudgmentLineZ = -2.8f;
        [Tooltip("레거시 실패선 Z 좌표입니다.")]
        [FormerlySerializedAs("DefenseLineZ")]
        public float FailLineZ = -3.8f;
        [Tooltip("플레이어 뷰 오브젝트의 기본 Z 위치입니다.")]
        public float PlayerZ = -3f;
    }
}
