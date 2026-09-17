using UnityEngine.UIElements;

namespace CommonUX
{
    /// <summary>
    /// A retained UI Toolkit screen that can be owned by one <see cref="ScreenStackElement"/>.
    /// Derive from this type to provide a default focus target and react to stack lifecycle changes.
    /// </summary>
    [UxmlElement]
    public partial class StackableScreenElement : VisualElement
    {
        internal ScreenStackElement Owner { get; set; }
        internal VisualElement LastFocused { get; set; }

        /// <summary>
        /// Gets the element focused when this screen has no valid saved focus.
        /// </summary>
        public virtual VisualElement DefaultFocus => this;

        public StackableScreenElement()
        {
            AddToClassList("commonux-screen");
            focusable = true;
            tabIndex = -1;
        }

        /// <summary>Called whenever this screen becomes the top screen.</summary>
        protected virtual void OnActivated()
        {
        }

        /// <summary>Called when another screen is pushed above this screen.</summary>
        protected virtual void OnCovered()
        {
        }

        /// <summary>Called after this screen is removed from its stack.</summary>
        protected virtual void OnPopped()
        {
        }

        internal void NotifyActivated() => OnActivated();
        internal void NotifyCovered() => OnCovered();
        internal void NotifyPopped() => OnPopped();
    }
}
