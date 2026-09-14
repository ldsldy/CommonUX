using System;
using System.Collections.Generic;

namespace CommonUX.Core
{
    /// <summary>등록된 화면의 순서를 관리합니다. Pop은 화면을 비활성화하지만 등록이나 명령을 파괴하지 않습니다.</summary>
    public sealed class ScreenStack : IDisposable
    {
        readonly UiContext m_context;
        readonly List<ScreenHandle> m_entries = new List<ScreenHandle>();
        readonly IReadOnlyList<ScreenHandle> m_readOnlyEntries;

        // Callback 중에는 요청 순서를 예약하고, 공개 목록은 callback이 끝난 뒤 FIFO로 반영합니다.
        internal readonly List<ScreenHandle> PlannedEntries = new List<ScreenHandle>();
        internal List<ScreenHandle> MutableEntries => m_entries;
        internal long LastInteraction;

        public StackHandle Handle { get; }
        public int Priority { get; }
        public bool AllowEmpty { get; }
        public IReadOnlyList<ScreenHandle> Entries => m_readOnlyEntries;
        public bool IsDisposed { get; internal set; }
        public bool CanGoBack => !IsDisposed && (AllowEmpty ? m_entries.Count > 0 : m_entries.Count > 1);

        internal ScreenStack(UiContext context, StackHandle handle, int priority, bool allowEmpty)
        {
            m_context = context;
            Handle = handle;
            Priority = priority;
            AllowEmpty = allowEmpty;
            m_readOnlyEntries = m_entries.AsReadOnly();
        }

        public void Push(ScreenHandle screen) => m_context.Push(this, screen);

        /// <summary>명시적 Pop은 AllowEmpty와 관계없이 마지막 화면도 제거합니다. 반환값은 예약된 요청 결과입니다.</summary>
        public bool Pop() => m_context.Pop(this);

        public void Clear() => m_context.Clear(this);
        public void Dispose() => m_context.DisposeStack(this);
    }
}
