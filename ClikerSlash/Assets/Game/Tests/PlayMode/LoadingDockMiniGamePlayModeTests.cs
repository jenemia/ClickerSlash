using System.Collections;
using ClikerSlash.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClikerSlash.Tests.PlayMode
{
    /// <summary>
    /// 상하차 큐 기반 뷰 유틸리티와 오브젝트 풀 동작을 검증합니다.
    /// </summary>
    public class LoadingDockMiniGamePlayModeTests
    {
        [UnityTest]
        public IEnumerator CargoArcMotionPeaksAboveEndpointsAndEndsAtTarget()
        {
            var start = new UnityEngine.Vector3(0f, 1f, 0f);
            var end = new UnityEngine.Vector3(4f, 1f, 0f);

            var startPoint = LoadingDockCargoArcMotion.Evaluate(start, end, 2f, 0f);
            var midPoint = LoadingDockCargoArcMotion.Evaluate(start, end, 2f, 0.5f);
            var endPoint = LoadingDockCargoArcMotion.Evaluate(start, end, 2f, 1f);

            Assert.That(startPoint.x, Is.EqualTo(start.x).Within(0.001f));
            Assert.That(startPoint.y, Is.EqualTo(start.y).Within(0.001f));
            Assert.That(startPoint.z, Is.EqualTo(start.z).Within(0.001f));
            Assert.That(endPoint.x, Is.EqualTo(end.x).Within(0.001f));
            Assert.That(endPoint.y, Is.EqualTo(end.y).Within(0.001f));
            Assert.That(endPoint.z, Is.EqualTo(end.z).Within(0.001f));
            Assert.That(midPoint.y, Is.GreaterThan(start.y));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CargoViewPoolReusesReleasedViewsByKind()
        {
            var root = new GameObject("CargoViewPoolRoot").transform;
            var prefabSet = CargoVisualPrefabSet.Create(
                CreateCargoPrefab("GeneralCargoPrefab", Color.yellow, new Vector3(0.9f, 0.9f, 0.9f)),
                CreateCargoPrefab("FragileCargoPrefab", Color.cyan, new Vector3(0.82f, 0.82f, 0.82f)),
                CreateCargoPrefab("FrozenCargoPrefab", Color.gray, new Vector3(1.08f, 1.08f, 1.08f)));
            var pool = new LoadingDockCargoViewPool(prefabSet);

            var firstGeneral = pool.Acquire(1, LoadingDockCargoKind.General, root, Vector3.zero);
            var firstFragile = pool.Acquire(2, LoadingDockCargoKind.Fragile, root, Vector3.one);

            pool.Release(firstGeneral);
            pool.Release(firstFragile);

            var reusedGeneral = pool.Acquire(3, LoadingDockCargoKind.General, root, Vector3.right);
            var reusedFragile = pool.Acquire(4, LoadingDockCargoKind.Fragile, root, Vector3.left);

            Assert.That(reusedGeneral.gameObject, Is.SameAs(firstGeneral.gameObject));
            Assert.That(reusedFragile.gameObject, Is.SameAs(firstFragile.gameObject));
            Assert.That(reusedGeneral.EntryId, Is.EqualTo(3));
            Assert.That(reusedFragile.EntryId, Is.EqualTo(4));
            Assert.That(reusedGeneral.Kind, Is.EqualTo(LoadingDockCargoKind.General));
            Assert.That(reusedFragile.Kind, Is.EqualTo(LoadingDockCargoKind.Fragile));
            Assert.That(reusedGeneral.gameObject.activeSelf, Is.True);
            Assert.That(reusedFragile.gameObject.activeSelf, Is.True);
            Assert.That(reusedGeneral.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(reusedFragile.GetComponent<Collider>(), Is.Not.Null);

            Object.Destroy(root.gameObject);
            Object.Destroy(prefabSet.GeneralPrefab);
            Object.Destroy(prefabSet.FragilePrefab);
            Object.Destroy(prefabSet.FrozenPrefab);
            yield return null;
        }

        private static GameObject CreateCargoPrefab(string name, Color color, Vector3 scale)
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = name;
            prefab.transform.localScale = scale;
            prefab.AddComponent<LoadingDockCargoView>();
            var renderer = prefab.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                color = color
            };
            return prefab;
        }
    }
}
