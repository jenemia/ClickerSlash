using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// ECS의 플레이어와 물류 위치를 프로토타입 씬의 단순 게임 오브젝트 뷰에 반영합니다.
    /// </summary>
    public sealed class BattlePresentationBridge : MonoBehaviour
    {
        [SerializeField] private GameObject playerViewPrefab;
        [SerializeField] private GameObject cargoViewPrefab;

        private World _cachedWorld;
        private EntityQuery _playerQuery;
        private EntityQuery _cargoQuery;
        private GameObject _playerInstance;
        private readonly Dictionary<Entity, GameObject> _cargoInstances = new();

        private void Update()
        {
            if (!TryPrepareQueries(out var entityManager))
            {
                CleanupAllViews();
                return;
            }

            SyncPlayer(entityManager);
            SyncCargo(entityManager);
        }

        private bool TryPrepareQueries(out EntityManager entityManager)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                return false;
            }

            entityManager = world.EntityManager;
            if (_cachedWorld == world)
            {
                return true;
            }

            _cachedWorld = world;
            _playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            _cargoQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CargoTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            return true;
        }

        private void SyncPlayer(EntityManager entityManager)
        {
            if (playerViewPrefab == null || _playerQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var transforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (_playerInstance == null)
            {
                _playerInstance = Instantiate(playerViewPrefab, transform);
                _playerInstance.name = "WorkerView";
            }

            _playerInstance.transform.position = transforms[0].Position;
        }

        private void SyncCargo(EntityManager entityManager)
        {
            if (cargoViewPrefab == null)
            {
                return;
            }

            using var entities = _cargoQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _cargoQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var alive = new HashSet<Entity>();

            for (var i = 0; i < entities.Length; i++)
            {
                var cargoEntity = entities[i];
                alive.Add(cargoEntity);

                if (!_cargoInstances.TryGetValue(cargoEntity, out var cargoView) || cargoView == null)
                {
                    cargoView = Instantiate(cargoViewPrefab, transform);
                    cargoView.name = $"CargoView_{cargoEntity.Index}";
                    _cargoInstances[cargoEntity] = cargoView;
                }

                cargoView.transform.position = transforms[i].Position;
            }

            if (_cargoInstances.Count == 0)
            {
                return;
            }

            var staleEntities = ListPool<Entity>.Get();
            foreach (var pair in _cargoInstances)
            {
                if (alive.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }

                staleEntities.Add(pair.Key);
            }

            foreach (var staleEntity in staleEntities)
            {
                _cargoInstances.Remove(staleEntity);
            }
            ListPool<Entity>.Release(staleEntities);
        }

        private void CleanupAllViews()
        {
            if (_playerInstance != null)
            {
                Destroy(_playerInstance);
                _playerInstance = null;
            }

            foreach (var pair in _cargoInstances)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }
            _cargoInstances.Clear();
        }

        private void OnDisable()
        {
            CleanupAllViews();
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new();

            public static List<T> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<T>();
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
