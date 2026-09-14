using System;
using System.Collections.Generic;
using UnityEngine;
using InputApi = UnityEngine.InputSystem.InputSystem;

namespace CommonUX.InputSystem
{
    /// <summary>게임에서 소유한 입력 아이콘과 대체 문구를 장치 레이아웃·컨트롤 경로별로 보관합니다.</summary>
    [CreateAssetMenu(fileName = "GlyphSet", menuName = "CommonUX/Glyph Set")]
    public sealed class GlyphSet : ScriptableObject
    {
        /// <summary>예: Gamepad + buttonSouth. 경로는 장치 접두사 없이 지정합니다.</summary>
        [Serializable]
        public sealed class Entry
        {
            public string deviceLayout = "Gamepad";
            public string controlPath = "buttonSouth";
            public Texture2D texture;
            public string fallbackText;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>정확한 레이아웃을 우선하고, 그다음 목록 순서로 부모 레이아웃을 찾습니다.</summary>
        public bool TryResolve(string deviceLayout, string controlPath, out Texture2D texture, out string fallbackText)
        {
            texture = null;
            fallbackText = null;
            if (string.IsNullOrEmpty(deviceLayout) || string.IsNullOrEmpty(controlPath)) return false;
            Entry inherited = null;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.deviceLayout) ||
                    !string.Equals(entry.controlPath, controlPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(entry.deviceLayout, deviceLayout, StringComparison.OrdinalIgnoreCase))
                {
                    texture = entry.texture;
                    fallbackText = entry.fallbackText;
                    return true;
                }
                if (inherited == null && InputApi.IsFirstLayoutBasedOnSecond(deviceLayout, entry.deviceLayout))
                    inherited = entry;
            }
            if (inherited == null) return false;
            texture = inherited.texture;
            fallbackText = inherited.fallbackText;
            return true;
        }
    }
}
