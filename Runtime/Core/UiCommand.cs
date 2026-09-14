using System;

namespace CommonUX.Core
{
    /// <summary>입력 장치나 외부 ID에 종속되지 않는 UI 업무 동작입니다. 객체 자체가 명령의 identity입니다.</summary>
    public sealed class UiCommand
    {
        readonly Action m_execute;
        readonly Func<bool> m_canExecute;

        public string Label { get; }
        public bool ShowInActionBar { get; }

        /// <summary>현재 실행 조건입니다. predicate의 예외는 숨기지 않고 호출자에게 전달합니다.</summary>
        public bool CanExecute => m_canExecute == null || m_canExecute();

        public UiCommand(string label, Action execute, Func<bool> canExecute = null, bool showInActionBar = true)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            m_execute = execute ?? throw new ArgumentNullException(nameof(execute));
            m_canExecute = canExecute;
            ShowInActionBar = showInActionBar;
        }

        internal void Execute() => m_execute();
    }
}
