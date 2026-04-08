using System;
using UnityEngine;

/// <summary>
/// 샘플 튜토리얼 인스펙터에 표시할 온보딩 텍스트를 저장하는 스크립터블 오브젝트입니다.
/// </summary>
public class Readme : ScriptableObject
{
    public Texture2D icon;
    public string title;
    public Section[] sections;
    // 커스텀 에디터가 이 에셋에 대해 샘플 레이아웃을 한 번 복원한 뒤에는 참입니다.
    public bool loadedLayout;

    /// <summary>
    /// 안내문 인스펙터에 표시되는 제목, 본문, 링크 단위의 단일 섹션입니다.
    /// </summary>
    [Serializable]
    public class Section
    {
        public string heading, text, linkText, url;
    }
}
