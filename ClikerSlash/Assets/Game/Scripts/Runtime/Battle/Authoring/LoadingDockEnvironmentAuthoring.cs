using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 상하차 프로토타입 구역의 블록아웃 루트와 핵심 기준점을 보관합니다.
    /// </summary>
    public sealed class LoadingDockEnvironmentAuthoring : MonoBehaviour
    {
        public Transform cargoBayRoot;
        public Transform truckBayRoot;
        public Transform cargoThrowOrigin;
        public Transform truckDropZone;
    }
}
