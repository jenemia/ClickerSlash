using System.Collections.Generic;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 상하차 구역 컨베이어 렌더러의 UV 오프셋만 스크롤해 벨트가 움직이는 것처럼 보이게 합니다.
    /// </summary>
    public sealed class LoadingDockConveyorPresenter : MonoBehaviour
    {
        [SerializeField] private LoadingDockEnvironmentAuthoring environment;

        private readonly List<ConveyorRendererState> _rendererStates = new();
        private MaterialPropertyBlock _propertyBlock;
        private string _cachedTexturePropertyName;
        private int _cachedTextureStPropertyId;
        private float _lastAppliedOffsetY = float.NaN;

        /// <summary>
        /// 씬 빌더나 테스트가 상하차 환경 참조를 명시적으로 연결할 때 사용합니다.
        /// </summary>
        public void BindSceneReferences(LoadingDockEnvironmentAuthoring targetEnvironment)
        {
            environment = targetEnvironment;
            InvalidateCachedRenderers();
        }

        /// <summary>
        /// 테스트가 현재 벨트 UV 이동량을 확인할 수 있게 마지막 적용 Y 오프셋을 노출합니다.
        /// </summary>
        public float LastAppliedOffsetY => _lastAppliedOffsetY;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// 게임이 진행되는 동안에만 현재 시각에 맞는 UV 오프셋을 컨베이어 렌더러들에 반영합니다.
        /// </summary>
        private void Update()
        {
            environment ??= FindFirstObjectByType<LoadingDockEnvironmentAuthoring>();
            if (environment == null)
            {
                return;
            }

            if (!TryPrepareRendererStates())
            {
                return;
            }

            ApplyOffset(Time.time * environment.conveyorUvSpeedY);
        }

        /// <summary>
        /// 플레이 모드 종료나 컴포넌트 비활성화 시 벨트 렌더러를 원래 UV 상태로 되돌립니다.
        /// </summary>
        private void OnDisable()
        {
            ResetOffsets();
        }

        /// <summary>
        /// 환경 참조나 프로퍼티 이름이 바뀌었을 때 다음 프레임에 캐시를 다시 만들도록 초기화합니다.
        /// </summary>
        private void OnValidate()
        {
            InvalidateCachedRenderers();
        }

        /// <summary>
        /// 현재 환경 설정과 벨트 렌더러 목록을 바탕으로 UV 애니메이션 캐시를 준비합니다.
        /// </summary>
        private bool TryPrepareRendererStates()
        {
            var texturePropertyName = string.IsNullOrWhiteSpace(environment.conveyorTexturePropertyName)
                ? "_BaseMap"
                : environment.conveyorTexturePropertyName;
            if (_rendererStates.Count > 0 && _cachedTexturePropertyName == texturePropertyName)
            {
                return true;
            }

            _rendererStates.Clear();
            _cachedTexturePropertyName = texturePropertyName;
            _cachedTextureStPropertyId = Shader.PropertyToID($"{texturePropertyName}_ST");

            foreach (var renderer in environment.GetConveyorBeltRenderers())
            {
                if (renderer == null)
                {
                    continue;
                }

                var sharedMaterial = renderer.sharedMaterial;
                if (sharedMaterial == null || !sharedMaterial.HasProperty(texturePropertyName))
                {
                    continue;
                }

                var scale = sharedMaterial.GetTextureScale(texturePropertyName);
                var offset = sharedMaterial.GetTextureOffset(texturePropertyName);
                _rendererStates.Add(new ConveyorRendererState(
                    renderer,
                    new Vector4(scale.x, scale.y, offset.x, offset.y)));
            }

            return _rendererStates.Count > 0;
        }

        /// <summary>
        /// 기존 property block을 유지한 채 texture ST 벡터의 Y 오프셋만 시간값으로 갱신합니다.
        /// </summary>
        private void ApplyOffset(float offsetY)
        {
            _lastAppliedOffsetY = offsetY;
            foreach (var rendererState in _rendererStates)
            {
                // 이미 다른 property block이 붙어 있어도 UV 오프셋 값만 덮어쓰도록 기존 블록을 읽어옵니다.
                rendererState.Renderer.GetPropertyBlock(_propertyBlock);
                var baseTextureSt = rendererState.BaseTextureSt;
                _propertyBlock.SetVector(
                    _cachedTextureStPropertyId,
                    new Vector4(baseTextureSt.x, baseTextureSt.y, baseTextureSt.z, baseTextureSt.w + offsetY));
                rendererState.Renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>
        /// 비활성화 시점에는 캐시된 기본 UV 오프셋으로 복구해 에디터 씬 값이 오염되지 않도록 합니다.
        /// </summary>
        private void ResetOffsets()
        {
            if (_rendererStates.Count == 0 || _propertyBlock == null)
            {
                return;
            }

            foreach (var rendererState in _rendererStates)
            {
                rendererState.Renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetVector(_cachedTextureStPropertyId, rendererState.BaseTextureSt);
                rendererState.Renderer.SetPropertyBlock(_propertyBlock);
            }

            _lastAppliedOffsetY = float.NaN;
        }

        /// <summary>
        /// 다음 Update에서 렌더러/프로퍼티 캐시를 다시 구성하도록 내부 상태를 초기화합니다.
        /// </summary>
        private void InvalidateCachedRenderers()
        {
            _rendererStates.Clear();
            _cachedTexturePropertyName = string.Empty;
            _cachedTextureStPropertyId = 0;
            _lastAppliedOffsetY = float.NaN;
        }

        private readonly struct ConveyorRendererState
        {
            public ConveyorRendererState(Renderer renderer, Vector4 baseTextureSt)
            {
                Renderer = renderer;
                BaseTextureSt = baseTextureSt;
            }

            public Renderer Renderer { get; }
            public Vector4 BaseTextureSt { get; }
        }
    }
}
