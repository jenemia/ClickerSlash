using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 레인과 상하차가 함께 사용하는 종류별 물류 프리팹 묶음입니다.
    /// </summary>
    [System.Serializable]
    public sealed class CargoVisualPrefabSet
    {
        [SerializeField] private GameObject standardPrefab;
        [SerializeField] private GameObject fragilePrefab;
        [SerializeField] private GameObject heavyPrefab;

        public GameObject StandardPrefab => standardPrefab;
        public GameObject FragilePrefab => fragilePrefab;
        public GameObject HeavyPrefab => heavyPrefab;
        public bool IsComplete => standardPrefab != null && fragilePrefab != null && heavyPrefab != null;

        public static CargoVisualPrefabSet Create(GameObject standardPrefab, GameObject fragilePrefab, GameObject heavyPrefab)
        {
            return new CargoVisualPrefabSet
            {
                standardPrefab = standardPrefab,
                fragilePrefab = fragilePrefab,
                heavyPrefab = heavyPrefab
            };
        }

        public GameObject Resolve(LoadingDockCargoKind kind)
        {
            return kind switch
            {
                LoadingDockCargoKind.Fragile => fragilePrefab,
                LoadingDockCargoKind.Heavy => heavyPrefab,
                _ => standardPrefab
            };
        }
    }
}
