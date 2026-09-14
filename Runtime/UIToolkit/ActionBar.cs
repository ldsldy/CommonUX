using System;
using CommonUX.Core;
using UnityEngine.UIElements;

namespace CommonUX.UIToolkit
{
    /// <summary>
    /// 현재 입력 소유 화면에 등록된 명령을 표시합니다.
    /// 버튼과 같은 UiCommand의 CanExecute를 사용하며 입력 힌트는 외부 공급자로 확장합니다.
    /// </summary>
    [UxmlElement]
    public partial class ActionBar : VisualElement
    {
        private UiContext context;
        private Func<UiCommand, string> hintProvider;
        private bool subscribed;

        public ActionBar()
        {
            AddToClassList("commonux-action-bar");
            style.flexDirection = FlexDirection.Row;
            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
        }

        public void Bind(UiContext value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            Unsubscribe();
            context = value;
            Subscribe();
            Refresh();
        }

        public void SetHintProvider(Func<UiCommand, string> provider)
        {
            hintProvider = provider;
            Refresh();
        }

        public void Refresh()
        {
            Clear();
            if (context == null) return;
            foreach (var command in context.GetActiveCommands())
            {
                if (!command.ShowInActionBar) continue;
                var hint = hintProvider?.Invoke(command);
                var item = new Button(() => context?.TryExecute(command))
                {
                    text = string.IsNullOrEmpty(hint) ? command.Label : $"{hint}  {command.Label}",
                    // 안내용 버튼이 화면의 기본 Tab 이동 순서를 늘리지 않도록 합니다.
                    focusable = false
                };
                item.AddToClassList("commonux-action-bar__action");
                item.SetEnabled(command.CanExecute);
                Add(item);
            }
        }

        public void Unbind()
        {
            Unsubscribe();
            context = null;
            hintProvider = null;
            Clear();
        }

        private void Subscribe()
        {
            if (context == null || subscribed || panel == null) return;
            context.Changed += Refresh;
            subscribed = true;
            Refresh();
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            context.Changed -= Refresh;
            subscribed = false;
        }
    }
}
