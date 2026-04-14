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
        [SerializeField] private GameObject frozenPrefab;

        public GameObject GeneralPrefab => standardPrefab;
        public GameObject StandardPrefab => standardPrefab;
        public GameObject FragilePrefab => fragilePrefab;
        public GameObject FrozenPrefab => frozenPrefab;
        public GameObject HeavyPrefab => frozenPrefab;
        public bool IsComplete => standardPrefab != null && fragilePrefab != null && frozenPrefab != null;

        public static CargoVisualPrefabSet Create(GameObject standardPrefab, GameObject fragilePrefab, GameObject frozenPrefab)
        {
            return new CargoVisualPrefabSet
            {
                standardPrefab = standardPrefab,
                fragilePrefab = fragilePrefab,
                frozenPrefab = frozenPrefab
            };
        }

        public GameObject Resolve(LoadingDockCargoKind kind)
        {
            return kind switch
            {
                LoadingDockCargoKind.Fragile => fragilePrefab,
                LoadingDockCargoKind.Frozen => frozenPrefab,
                _ => standardPrefab
            };
        }
    }
}
