using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    public sealed class BattlePresentationBridge : MonoBehaviour
    {
        [SerializeField] private GameObject playerViewPrefab;
        [SerializeField] private GameObject enemyViewPrefab;

        private World _cachedWorld;
        private EntityQuery _playerQuery;
        private EntityQuery _enemyQuery;
        private GameObject _playerInstance;
        private readonly Dictionary<Entity, GameObject> _enemyInstances = new();

        private void Update()
        {
            if (!TryPrepareQueries(out var entityManager))
            {
                CleanupAllViews();
                return;
            }

            SyncPlayer(entityManager);
            SyncEnemies(entityManager);
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
            _enemyQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyTag>(),
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
                _playerInstance.name = "PlayerView";
            }

            _playerInstance.transform.position = transforms[0].Position;
        }

        private void SyncEnemies(EntityManager entityManager)
        {
            if (enemyViewPrefab == null)
            {
                return;
            }

            using var entities = _enemyQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var alive = new HashSet<Entity>();

            for (var i = 0; i < entities.Length; i++)
            {
                var enemyEntity = entities[i];
                alive.Add(enemyEntity);

                if (!_enemyInstances.TryGetValue(enemyEntity, out var enemyView) || enemyView == null)
                {
                    enemyView = Instantiate(enemyViewPrefab, transform);
                    enemyView.name = $"EnemyView_{enemyEntity.Index}";
                    _enemyInstances[enemyEntity] = enemyView;
                }

                enemyView.transform.position = transforms[i].Position;
            }

            if (_enemyInstances.Count == 0)
            {
                return;
            }

            var staleEntities = ListPool<Entity>.Get();
            foreach (var pair in _enemyInstances)
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
                _enemyInstances.Remove(staleEntity);
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

            foreach (var pair in _enemyInstances)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }
            _enemyInstances.Clear();
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
