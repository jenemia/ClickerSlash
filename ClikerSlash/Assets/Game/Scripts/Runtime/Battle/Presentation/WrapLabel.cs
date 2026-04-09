using System;
using TMPro;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 프로젝트 기본 TMP 폰트를 자동 적용하는 텍스트 래퍼입니다.
    /// </summary>
    public sealed class WrapLabel : TextMeshProUGUI
    {
        private const string DefaultFontResourcePath = "Font/NanumGothicEco SDF";
        private const string BuiltInTmpFontName = "LiberationSans SDF";
        private static TMP_FontAsset s_defaultFont;

        /// <summary>
        /// 기본 폰트를 아직 지정하지 않았다면 Resources에서 불러와 적용합니다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            ApplyDefaultFontIfNeeded();
        }

        /// <summary>
        /// 에디터에서 새 컴포넌트를 붙였을 때도 동일한 기본 폰트를 사용합니다.
        /// </summary>
        protected override void Reset()
        {
            base.Reset();
            ApplyDefaultFontIfNeeded();
        }

        /// <summary>
        /// 직렬화 복원 후에도 폰트가 비어 있으면 기본 폰트를 채워 넣습니다.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyDefaultFontIfNeeded();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 직렬화 갱신 시에도 기본 폰트 규칙을 유지합니다.
        /// </summary>
        protected override void OnValidate()
        {
            base.OnValidate();
            ApplyDefaultFontIfNeeded();
        }
#endif

        private void ApplyDefaultFontIfNeeded()
        {
            if (!ShouldApplyDefaultFont())
            {
                return;
            }

            s_defaultFont ??= Resources.Load<TMP_FontAsset>(DefaultFontResourcePath);
            if (s_defaultFont != null)
            {
                font = s_defaultFont;
            }
        }

        private bool ShouldApplyDefaultFont()
        {
            if (font == null)
            {
                return true;
            }

            return string.Equals(font.name, BuiltInTmpFontName, StringComparison.Ordinal);
        }
    }
}
