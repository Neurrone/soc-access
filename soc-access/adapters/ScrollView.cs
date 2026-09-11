using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>Moving a scrolling list to what the reader is on, the way the game's own navigation
    /// does it: the view moves only as far as it must, so a row already inside it does not jump.</summary>
    public static class ScrollView
    {
        public static void Reveal(ScrollRect scrollRect, RectTransform source)
        {
            if (scrollRect == null || scrollRect.content == null || source == null)
            {
                return;
            }

            RectTransform viewport = scrollRect.viewport != null
                ? scrollRect.viewport
                : ((Component)scrollRect).GetComponent<RectTransform>();
            if (viewport == null)
            {
                return;
            }

            // The layout the bounds are measured against is the one the game has drawn so far; a row
            // the menu created this frame has no rect until the canvas is brought up to date.
            Canvas.ForceUpdateCanvases();

            Bounds itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, source);
            Rect viewportRect = viewport.rect;
            float scrollableHeight = scrollRect.content.rect.height - viewportRect.height;
            if (scrollableHeight <= 0f)
            {
                return;
            }

            float normalized = scrollRect.verticalNormalizedPosition;
            if (itemBounds.max.y > viewportRect.max.y)
            {
                normalized += (itemBounds.max.y - viewportRect.max.y) / scrollableHeight;
            }
            else if (itemBounds.min.y < viewportRect.min.y)
            {
                normalized -= (viewportRect.min.y - itemBounds.min.y) / scrollableHeight;
            }
            else
            {
                return;
            }

            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
        }
    }
}
