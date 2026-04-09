using UnityEngine;
using UnityEngine.EventSystems;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 큰 스킬트리 콘텐츠를 마우스 드래그와 휠 줌으로 탐색하게 해주는 컨트롤러입니다.
    /// </summary>
    public sealed class PrototypeHubPanZoomController : UIBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private float minZoom = 0.42f;
        [SerializeField] private float maxZoom = 1.65f;
        [SerializeField] private float zoomStep = 0.12f;
        [SerializeField] private float clampPadding = 120f;

        /// <summary>
        /// 바깥 빌더가 생성한 뷰포트와 콘텐츠 루트를 연결합니다.
        /// </summary>
        public void Bind(RectTransform viewportTransform, RectTransform contentTransform)
        {
            viewport = viewportTransform;
            content = contentTransform;
        }

        /// <summary>
        /// 첫 진입 시 전체 그래프가 화면 안에 들어오도록 배율과 위치를 초기화합니다.
        /// </summary>
        public void FrameContent()
        {
            if (viewport == null || content == null)
            {
                return;
            }

            var viewportSize = viewport.rect.size;
            var contentSize = content.rect.size;
            if (viewportSize.x <= 0f || viewportSize.y <= 0f || contentSize.x <= 0f || contentSize.y <= 0f)
            {
                return;
            }

            var fitScale = Mathf.Min(
                viewportSize.x / contentSize.x,
                viewportSize.y / contentSize.y);
            fitScale = Mathf.Clamp(fitScale * 0.92f, minZoom, maxZoom);
            content.localScale = new Vector3(fitScale, fitScale, 1f);
            content.anchoredPosition = Vector2.zero;
            ClampContentIntoView();
        }

        /// <summary>
        /// 드래그 시작 시 현재 설정 상태만 정리합니다.
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            ClampContentIntoView();
        }

        /// <summary>
        /// 포인터 이동량만큼 콘텐츠를 평행이동합니다.
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (content == null)
            {
                return;
            }

            content.anchoredPosition += eventData.delta;
            ClampContentIntoView();
        }

        /// <summary>
        /// 휠 위치를 기준으로 줌 중심을 유지하며 확대/축소합니다.
        /// </summary>
        public void OnScroll(PointerEventData eventData)
        {
            if (viewport == null || content == null || Mathf.Approximately(eventData.scrollDelta.y, 0f))
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewport,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var pointerLocalPosition))
            {
                return;
            }

            var previousScale = content.localScale.x;
            var targetScale = previousScale * (1f + eventData.scrollDelta.y * zoomStep);
            targetScale = Mathf.Clamp(targetScale, minZoom, maxZoom);
            if (Mathf.Approximately(previousScale, targetScale))
            {
                return;
            }

            var contentPointBeforeZoom = (pointerLocalPosition - content.anchoredPosition) / previousScale;
            content.localScale = new Vector3(targetScale, targetScale, 1f);
            content.anchoredPosition = pointerLocalPosition - contentPointBeforeZoom * targetScale;
            ClampContentIntoView();
        }

        /// <summary>
        /// 콘텐츠가 화면 밖으로 완전히 사라지지 않도록 이동 범위를 제한합니다.
        /// </summary>
        private void ClampContentIntoView()
        {
            if (viewport == null || content == null)
            {
                return;
            }

            var scaledWidth = content.rect.width * content.localScale.x;
            var scaledHeight = content.rect.height * content.localScale.y;
            var viewportWidth = viewport.rect.width;
            var viewportHeight = viewport.rect.height;

            var maxOffsetX = Mathf.Max(0f, (scaledWidth - viewportWidth) * 0.5f + clampPadding);
            var maxOffsetY = Mathf.Max(0f, (scaledHeight - viewportHeight) * 0.5f + clampPadding);

            content.anchoredPosition = new Vector2(
                Mathf.Clamp(content.anchoredPosition.x, -maxOffsetX, maxOffsetX),
                Mathf.Clamp(content.anchoredPosition.y, -maxOffsetY, maxOffsetY));
        }
    }
}
