using System;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 레인 전용 상자 타입별 프리팹 묶음입니다.
    /// 명시 연결이 비어 있으면 Resources의 기본 프리팹을 사용합니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CargoTypePrefabProfile",
        menuName = "ClikerSlash/Battle/Cargo Type Prefab Profile")]
    public sealed class CargoTypePrefabProfile : ScriptableObject
    {
        private const string SmallBrownResourcePath = "Battle/BoxSmallBrown";
        private const string SmallWhiteResourcePath = "Battle/BoxSmallWhite";
        private const string LargeBrownResourcePath = "Battle/BoxLargeBrown";
        private const string LargeWhiteResourcePath = "Battle/BoxLargeWhite";

        [SerializeField] private GameObject[] standardPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] fragilePrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] heavyPrefabs = Array.Empty<GameObject>();

        /// <summary>
        /// 지정한 물류 타입이 현재 사용할 수 있는 prefab variant 개수를 반환합니다.
        /// </summary>
        public int GetVariantCount(LoadingDockCargoKind kind)
        {
            var prefabs = GetConfiguredPrefabs(kind);
            return prefabs.Length > 0 ? prefabs.Length : GetDefaultVariantCount(kind);
        }

        /// <summary>
        /// 타입과 variant id에 맞는 prefab을 반환하고, 설정이 비어 있으면 기본 리소스로 폴백합니다.
        /// </summary>
        public GameObject ResolvePrefab(LoadingDockCargoKind kind, int variantId)
        {
            var prefabs = GetConfiguredPrefabs(kind);
            if (prefabs.Length > 0)
            {
                // 런타임에서 잘못된 variant id가 들어와도 가장 가까운 유효 범위로 보정합니다.
                var clampedIndex = Mathf.Clamp(variantId, 0, prefabs.Length - 1);
                if (prefabs[clampedIndex] != null)
                {
                    return prefabs[clampedIndex];
                }
            }

            return ResolveDefaultPrefab(kind, variantId);
        }

        /// <summary>
        /// 인스펙터 연결이 전혀 없을 때 타입별 기본 variant 개수를 반환합니다.
        /// </summary>
        public static int GetDefaultVariantCount(LoadingDockCargoKind kind)
        {
            return kind == LoadingDockCargoKind.Heavy ? 2 : 1;
        }

        /// <summary>
        /// 인스펙터 연결이 비어 있을 때 사용할 Resources 기본 prefab을 반환합니다.
        /// </summary>
        public static GameObject ResolveDefaultPrefab(LoadingDockCargoKind kind, int variantId)
        {
            var resourcePath = kind switch
            {
                LoadingDockCargoKind.Fragile => SmallWhiteResourcePath,
                LoadingDockCargoKind.Heavy when Mathf.Abs(variantId) % 2 == 1 => LargeWhiteResourcePath,
                LoadingDockCargoKind.Heavy => LargeBrownResourcePath,
                _ => SmallBrownResourcePath
            };

            return Resources.Load<GameObject>(resourcePath);
        }

        /// <summary>
        /// 타입에 대응하는 인스펙터 연결 배열을 반환합니다.
        /// </summary>
        private GameObject[] GetConfiguredPrefabs(LoadingDockCargoKind kind)
        {
            return kind switch
            {
                LoadingDockCargoKind.Fragile => fragilePrefabs,
                LoadingDockCargoKind.Heavy => heavyPrefabs,
                _ => standardPrefabs
            };
        }
    }
}
