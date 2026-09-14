using System;
using System.Collections.Generic;

namespace CommonUX.Core
{
    /// <summary>화면 등록 수명과 구분되는 현재 표시/입력 상태입니다.</summary>
    public enum ScreenState
    {
        Inactive,
        Active,
        Covered
    }

    /// <summary>화면의 표시 정책과 해당 화면이 입력을 소유할 때의 게임 입력 정책입니다.</summary>
    public sealed class ScreenOptions
    {
        /// <summary>같은 stack의 바로 아래 화면을 보이게 합니다. 입력 소유권은 최상단 화면에만 있습니다.</summary>
        public bool IsModal { get; }
        public bool BlocksGameplayInput { get; }

        public ScreenOptions(bool isModal = false, bool blocksGameplayInput = true)
        {
            IsModal = isModal;
            BlocksGameplayInput = blocksGameplayInput;
        }
    }

    /// <summary>하나의 확정된 변경 이후 입력 소유권과 명령 목록입니다. 뒤 변경이 이 객체를 수정하지 않습니다.</summary>
    public sealed class UiStateSnapshot
    {
        public ScreenHandle ActiveScreen { get; }
        public bool BlocksGameplayInput { get; }
        public IReadOnlyList<UiCommand> Commands { get; }
        public long Version { get; }

        internal UiStateSnapshot(ScreenHandle activeScreen, bool blocksGameplayInput, UiCommand[] commands, long version)
        {
            ActiveScreen = activeScreen;
            BlocksGameplayInput = blocksGameplayInput;
            Commands = Array.AsReadOnly(commands);
            Version = version;
        }
    }
}
