// Author: Jorge Dinares
// Copyright © 2025 Jorge Dinares. All rights reserved.
/// <summary>
/// A custom <see cref="VisualElement"/> that acts as a custom mouse cursor for the editor UI.
/// </summary>
/// <remarks>
/// This element handles the visual display of the cursor, including changing its texture
/// and hotspot based on user interaction states (e.g., dragging, resizing, or copying).
/// It can be shown or hidden as needed.
/// </remarks>
using UnityEngine.UIElements;

namespace Jorjouto.AnimComposerSystem.ACSEditor
{
    [UxmlElement]
    public partial class CursorElement : VisualElement
    {
        /// <summary>
        /// Flag indicating if an item (action block) has been selected for dragging or resizing.
        /// </summary>
        private bool bHasItemSelected = false;

        /// <summary>
        /// Flag indicating if an action block has been copied.
        /// </summary>
        private bool bHasBlockCopied = false;

        /// <summary>
        /// Flag indicating if a track has been copied.
        /// </summary>
        private bool bHasTrackCopied = false;

        /// <summary>
        /// The style of the mouse cursor that is currently active.
        /// </summary>
        private string activeMouseStyle = null;

        /// <summary>
        /// Sets whether an item is currently selected.
        /// </summary>
        /// <param name="hasItemSelected">True if an item is selected, otherwise false.</param>
        public void SetHasItemSelected(bool hasItemSelected) => bHasItemSelected = hasItemSelected;

        /// <summary>
        /// Gets whether an item is currently selected.
        /// </summary>
        /// <returns>True if an item is selected, otherwise false.</returns>
        public bool GetHasItemSelected() => bHasItemSelected;

        /// <summary>
        /// Sets whether an action block has been copied.
        /// </summary>
        /// <param name="hasBlockCopied">True if a block has been copied, otherwise false.</param>
        public void SetHasBlockCopied(bool hasBlockCopied) => bHasBlockCopied = hasBlockCopied;

        /// <summary>
        /// Gets whether an action block has been copied.
        /// </summary>
        /// <returns>True if a block has been copied, otherwise false.</returns>
        public bool GetHasBlockCopied() => bHasBlockCopied;

        /// <summary>
        /// Sets whether a track has been copied.
        /// </summary>
        /// <param name="hasTrackCopied">True if a track has been copied, otherwise false.</param>
        public void SetHasTrackCopied(bool hasTrackCopied) => bHasTrackCopied = hasTrackCopied;

        /// <summary>
        /// Gets whether a track has been copied.
        /// </summary>
        /// <returns>True if a track has been copied, otherwise false.</returns>
        public bool GetHasTrackCopied() => bHasTrackCopied;


        /// <summary>
        /// Hides the custom cursor.
        /// </summary>
        private void Hide()
        {
            RemoveFromClassList("DragCursor");
            RemoveFromClassList("StretchCursor");
        }

        /// <summary>
        /// Attempts to change the custom mouse cursor to the specified style.
        /// </summary>
        /// <param name="mouseStyle">The new style for the cursor.</param>
        public void TryChangeMouseCursor(string mouseStyle, bool bMustItemsBeReleased)
        {
            if ((GetHasItemSelected() && bMustItemsBeReleased)|| mouseStyle == activeMouseStyle)
            {
                return;
            }

            if (mouseStyle == null)
            {
                Hide();
            }
            else
            {
                if (activeMouseStyle != null && ClassListContains(activeMouseStyle))
                {
                    RemoveFromClassList(activeMouseStyle);
                }
                if (!ClassListContains(mouseStyle))
                {
                    AddToClassList(mouseStyle);
                }
            }

            activeMouseStyle = mouseStyle;
        }
    }
}