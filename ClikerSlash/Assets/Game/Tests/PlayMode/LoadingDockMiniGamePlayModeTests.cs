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
            var pool = new LoadingDockCargoViewPool();

            var firstStandard = pool.Acquire(1, LoadingDockCargoKind.Standard, root, Vector3.zero);
            var firstFragile = pool.Acquire(2, LoadingDockCargoKind.Fragile, root, Vector3.one);

            pool.Release(firstStandard);
            pool.Release(firstFragile);

            var reusedStandard = pool.Acquire(3, LoadingDockCargoKind.Standard, root, Vector3.right);
            var reusedFragile = pool.Acquire(4, LoadingDockCargoKind.Fragile, root, Vector3.left);

            Assert.That(reusedStandard.gameObject, Is.SameAs(firstStandard.gameObject));
            Assert.That(reusedFragile.gameObject, Is.SameAs(firstFragile.gameObject));
            Assert.That(reusedStandard.EntryId, Is.EqualTo(3));
            Assert.That(reusedFragile.EntryId, Is.EqualTo(4));
            Assert.That(reusedStandard.Kind, Is.EqualTo(LoadingDockCargoKind.Standard));
            Assert.That(reusedFragile.Kind, Is.EqualTo(LoadingDockCargoKind.Fragile));
            Assert.That(reusedStandard.gameObject.activeSelf, Is.True);
            Assert.That(reusedFragile.gameObject.activeSelf, Is.True);

            Object.Destroy(root.gameObject);
            yield return null;
        }
    }
}
