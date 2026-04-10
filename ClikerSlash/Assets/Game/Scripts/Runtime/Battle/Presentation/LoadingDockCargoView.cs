using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 상하차 큐 엔트리를 화면 오브젝트와 연결하는 경량 뷰 컴포넌트입니다.
    /// </summary>
    public sealed class LoadingDockCargoView : MonoBehaviour
    {
        public int EntryId { get; private set; }
        public LoadingDockCargoKind Kind { get; private set; }

        public void Bind(int entryId, LoadingDockCargoKind kind)
        {
            EntryId = entryId;
            Kind = kind;
        }
    }
}
