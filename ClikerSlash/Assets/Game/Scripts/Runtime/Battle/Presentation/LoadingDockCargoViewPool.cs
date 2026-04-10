using System.Collections.Generic;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 종류별 cube 뷰를 재사용하는 상하차 전용 오브젝트 풀입니다.
    /// </summary>
    public sealed class LoadingDockCargoViewPool
    {
        private readonly Dictionary<LoadingDockCargoKind, Stack<LoadingDockCargoView>> _poolByKind = new();
        private CargoVisualPrefabSet _cargoVisualPrefabs;
        private Transform _poolRoot;

        public LoadingDockCargoViewPool(CargoVisualPrefabSet cargoVisualPrefabs = null)
        {
            _cargoVisualPrefabs = cargoVisualPrefabs;
        }

        public void Configure(CargoVisualPrefabSet cargoVisualPrefabs)
        {
            _cargoVisualPrefabs = cargoVisualPrefabs;
        }

        public LoadingDockCargoView Acquire(
            int entryId,
            LoadingDockCargoKind kind,
            Transform parent,
            Vector3 position)
        {
            var view = TryPop(kind);
            if (view == null)
            {
                view = CreateView(kind);
            }

            if (view == null)
            {
                return null;
            }

            var viewTransform = view.transform;
            viewTransform.SetParent(parent, false);
            viewTransform.position = position;
            view.gameObject.name = $"LoadingDockCargo_{entryId}";
            view.gameObject.SetActive(true);
            view.Bind(entryId, kind);

            return view;
        }

        public void Release(LoadingDockCargoView view)
        {
            if (view == null)
            {
                return;
            }

            view.gameObject.SetActive(false);
            view.transform.SetParent(GetOrCreatePoolRoot(), false);
            var kind = view.Kind;
            if (!_poolByKind.TryGetValue(kind, out var pool))
            {
                pool = new Stack<LoadingDockCargoView>();
                _poolByKind[kind] = pool;
            }

            pool.Push(view);
        }

        private LoadingDockCargoView TryPop(LoadingDockCargoKind kind)
        {
            return _poolByKind.TryGetValue(kind, out var pool) && pool.Count > 0
                ? pool.Pop()
                : null;
        }

        private Transform GetOrCreatePoolRoot()
        {
            if (_poolRoot != null)
            {
                return _poolRoot;
            }

            var rootObject = new GameObject("LoadingDockCargoPoolRoot");
            rootObject.SetActive(false);
            _poolRoot = rootObject.transform;
            return _poolRoot;
        }

        private LoadingDockCargoView CreateView(LoadingDockCargoKind kind)
        {
            var prefab = _cargoVisualPrefabs?.Resolve(kind);
            if (prefab == null)
            {
                Debug.LogWarning($"No cargo prefab configured for kind {kind}.");
                return null;
            }

            var cargoObject = Object.Instantiate(prefab);
            cargoObject.name = $"LoadingDockCargo_{kind}";
            cargoObject.SetActive(false);
            return cargoObject.GetComponent<LoadingDockCargoView>() ?? cargoObject.AddComponent<LoadingDockCargoView>();
        }
    }
}
