using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CommonUX.Tests
{
    public sealed class ScreenStackElementTests
    {
        private TestWindow window;

        [TearDown]
        public void TearDown()
        {
            if (window == null)
                return;

            window.Close();
            UnityEngine.Object.DestroyImmediate(window);
        }

        [Test]
        public void Push_ActivatesNewTopAndCoversPreviousTop()
        {
            var stack = new ScreenStackElement();
            var first = new TestScreen();
            var second = new TestScreen();

            stack.Push(first);
            stack.Push(second);

            Assert.That(first.ActivatedCount, Is.EqualTo(1));
            Assert.That(first.CoveredCount, Is.EqualTo(1));
            Assert.That(first.parent, Is.Null);
            Assert.That(second.ActivatedCount, Is.EqualTo(1));
            Assert.That(second.parent, Is.SameAs(stack));
        }

        [Test]
        public void Pop_ReturnsRemovedTopAndReactivatesPreviousScreen()
        {
            var stack = new ScreenStackElement();
            var first = new TestScreen();
            var second = new TestScreen();
            stack.Push(first);
            stack.Push(second);

            var popped = stack.Pop();

            Assert.That(popped, Is.SameAs(second));
            Assert.That(second.PoppedCount, Is.EqualTo(1));
            Assert.That(second.parent, Is.Null);
            Assert.That(first.ActivatedCount, Is.EqualTo(2));
            Assert.That(first.parent, Is.SameAs(stack));
        }

        [Test]
        public void Pop_WhenEmpty_ReturnsNull()
        {
            var stack = new ScreenStackElement();

            Assert.That(stack.Pop(), Is.Null);
        }

        [Test]
        public void Push_WhenScreenAlreadyBelongsToAStack_Throws()
        {
            var firstStack = new ScreenStackElement();
            var secondStack = new ScreenStackElement();
            var screen = new TestScreen();
            firstStack.Push(screen);

            Assert.Throws<InvalidOperationException>(() => secondStack.Push(screen));
        }

        [Test]
        public void Push_WhenScreenIsNull_Throws()
        {
            var stack = new ScreenStackElement();

            Assert.Throws<ArgumentNullException>(() => stack.Push(null));
        }

        [Test]
        public void Push_WhenScreenAlreadyBelongsToAnotherVisualTree_Throws()
        {
            var container = new VisualElement();
            var stack = new ScreenStackElement();
            var screen = new TestScreen();
            container.Add(screen);

            Assert.Throws<InvalidOperationException>(() => stack.Push(screen));
        }

        [Test]
        public void Push_FocusesDefaultFocus()
        {
            var stack = CreateAttachedStack();
            var screen = new FocusScreen();

            stack.Push(screen);

            Assert.That(FocusedElement, Is.SameAs(screen.DefaultTarget));
        }

        [Test]
        public void Pop_RestoresLastFocusedElement()
        {
            var stack = CreateAttachedStack();
            var first = new FocusScreen();
            var second = new FocusScreen();
            stack.Push(first);
            first.AlternateTarget.Focus();
            Assert.That(FocusedElement, Is.SameAs(first.AlternateTarget));
            stack.Push(second);

            stack.Pop();

            Assert.That(FocusedElement, Is.SameAs(first.AlternateTarget));
        }

        [Test]
        public void Pop_WhenSavedFocusIsNoLongerValid_FocusesDefaultFocus()
        {
            var stack = CreateAttachedStack();
            var first = new FocusScreen();
            var second = new FocusScreen();
            stack.Push(first);
            first.AlternateTarget.Focus();
            stack.Push(second);
            first.AlternateTarget.RemoveFromHierarchy();

            stack.Pop();

            Assert.That(FocusedElement, Is.SameAs(first.DefaultTarget));
        }

        [Test]
        public void Push_FromLifecycleCallback_Throws()
        {
            var stack = new ScreenStackElement();
            var nested = new TestScreen();
            var screen = new ReentrantScreen(() => stack.Push(nested));

            Assert.Throws<InvalidOperationException>(() => stack.Push(screen));
            Assert.That(nested.parent, Is.Null);
        }

        private Focusable FocusedElement =>
            window.rootVisualElement.panel.focusController.focusedElement;

        private ScreenStackElement CreateAttachedStack()
        {
            window = ScriptableObject.CreateInstance<TestWindow>();
            window.Show();
            var stack = new ScreenStackElement();
            window.rootVisualElement.Add(stack);
            return stack;
        }

        private sealed class TestScreen : StackableScreenElement
        {
            public int ActivatedCount { get; private set; }
            public int CoveredCount { get; private set; }
            public int PoppedCount { get; private set; }

            public override VisualElement DefaultFocus => this;

            protected override void OnActivated() => ActivatedCount++;
            protected override void OnCovered() => CoveredCount++;
            protected override void OnPopped() => PoppedCount++;
        }

        private sealed class FocusScreen : StackableScreenElement
        {
            public Button DefaultTarget { get; } = new();
            public Button AlternateTarget { get; } = new();

            public override VisualElement DefaultFocus => DefaultTarget;

            public FocusScreen()
            {
                Add(DefaultTarget);
                Add(AlternateTarget);
            }
        }

        private sealed class ReentrantScreen : StackableScreenElement
        {
            private readonly Action onActivated;

            public ReentrantScreen(Action onActivated)
            {
                this.onActivated = onActivated;
            }

            protected override void OnActivated() => onActivated();
        }

        private sealed class TestWindow : EditorWindow
        {
        }
    }
}
