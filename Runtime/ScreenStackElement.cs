using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace CommonUX
{
    /// <summary>
    /// Owns one independent stack of <see cref="StackableScreenElement"/> instances.
    /// Only the active top screen is attached to the visual tree; covered screens remain retained
    /// by the stack and are restored when the top screen is popped.
    /// </summary>
    [UxmlElement]
    public partial class ScreenStackElement : VisualElement
    {
        private readonly List<StackableScreenElement> screens = new();
        private bool isTransitioning;

        public ScreenStackElement()
        {
            AddToClassList("commonux-stack");
        }

        /// <summary>Pushes a detached screen and makes it active.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="screen"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// The screen already belongs to a stack or visual tree, or a lifecycle callback attempted
        /// to mutate this stack before the current transition completed.
        /// </exception>
        public void Push(StackableScreenElement screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            if (screen.Owner != null || screen.parent != null)
                throw new InvalidOperationException("A screen must be detached and unowned before it can be pushed.");

            BeginTransition();
            try
            {
                var previous = GetTop();
                CaptureFocus(previous);

                previous?.RemoveFromHierarchy();
                screens.Add(screen);
                screen.Owner = this;
                hierarchy.Add(screen);

                previous?.NotifyCovered();
                screen.NotifyActivated();
                RestoreFocus(screen);
            }
            finally
            {
                isTransitioning = false;
            }
        }

        /// <summary>
        /// Removes and returns the active screen, or returns null when the stack is empty.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// A lifecycle callback attempted to mutate this stack before the current transition completed.
        /// </exception>
        public StackableScreenElement Pop()
        {
            BeginTransition();
            try
            {
                var popped = GetTop();
                if (popped == null)
                    return null;

                popped.RemoveFromHierarchy();
                screens.RemoveAt(screens.Count - 1);
                popped.Owner = null;
                popped.LastFocused = null;

                var previous = GetTop();
                if (previous != null)
                    hierarchy.Add(previous);

                popped.NotifyPopped();
                previous?.NotifyActivated();
                RestoreFocus(previous);
                return popped;
            }
            finally
            {
                isTransitioning = false;
            }
        }

        private void BeginTransition()
        {
            if (isTransitioning)
                throw new InvalidOperationException("A screen stack cannot be mutated from a lifecycle callback.");

            isTransitioning = true;
        }

        private StackableScreenElement GetTop()
        {
            return screens.Count == 0 ? null : screens[screens.Count - 1];
        }

        private static void CaptureFocus(StackableScreenElement screen)
        {
            if (screen?.panel?.focusController.focusedElement is VisualElement focused &&
                (focused == screen || screen.Contains(focused)))
            {
                screen.LastFocused = focused;
            }
        }

        private static void RestoreFocus(StackableScreenElement screen)
        {
            if (screen?.panel == null)
                return;

            var target = CanFocus(screen.LastFocused, screen)
                ? screen.LastFocused
                : screen.DefaultFocus;

            if (CanFocus(target, screen))
                target.Focus();
        }

        private static bool CanFocus(VisualElement element, VisualElement screen)
        {
            if (element == null || element.panel == null || !element.focusable ||
                !element.enabledInHierarchy || (element != screen && !screen.Contains(element)))
            {
                return false;
            }

            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None ||
                    current.resolvedStyle.visibility == Visibility.Hidden)
                {
                    return false;
                }

                if (current == screen)
                    return true;
            }

            return false;
        }
    }
}
