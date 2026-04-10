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

            var viewTransform = view.transform;
            viewTransform.SetParent(parent, false);
            viewTransform.position = position;
            viewTransform.localScale = ResolveCargoScale(kind);
            view.gameObject.name = $"LoadingDockCargo_{entryId}";
            view.gameObject.SetActive(true);
            view.Bind(entryId, kind);

            var renderer = view.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = ResolveCargoColor(kind);
            }

            return view;
        }

        public void Release(LoadingDockCargoView view)
        {
            if (view == null)
            {
                return;
            }

            view.gameObject.SetActive(false);
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

        private static LoadingDockCargoView CreateView(LoadingDockCargoKind kind)
        {
            var cargoObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cargoObject.name = $"LoadingDockCargo_{kind}";
            cargoObject.SetActive(false);

            var view = cargoObject.AddComponent<LoadingDockCargoView>();
            var renderer = cargoObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = ResolveCargoColor(kind);
            }

            cargoObject.transform.localScale = ResolveCargoScale(kind);
            return view;
        }

        private static Vector3 ResolveCargoScale(LoadingDockCargoKind kind)
        {
            return kind switch
            {
                LoadingDockCargoKind.Heavy => new Vector3(1.35f, 1.35f, 1.35f),
                LoadingDockCargoKind.Fragile => new Vector3(1.15f, 1.15f, 1.15f),
                _ => new Vector3(1.2f, 1.2f, 1.2f)
            };
        }

        private static Color ResolveCargoColor(LoadingDockCargoKind kind)
        {
            return kind switch
            {
                LoadingDockCargoKind.Fragile => new Color(0.4f, 0.85f, 1f),
                LoadingDockCargoKind.Heavy => new Color(0.72f, 0.72f, 0.72f),
                _ => new Color(1f, 0.6f, 0.2f)
            };
        }
    }
}
