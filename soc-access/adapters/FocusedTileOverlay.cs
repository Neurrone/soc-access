using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The yellow square drawn around the focused tile on a map or board. Owns its own
    /// screen-space canvas and the four segments that make up the outline; the adapter that
    /// holds one destroys it when its menu goes away.
    /// </summary>
    public sealed class FocusedTileOverlay
    {
        private const float Size = 42f;
        private const float Thickness = 4f;

        private readonly string _objectName;
        private GameObject _overlay;
        private RectTransform[] _segments;

        public FocusedTileOverlay(string objectName)
        {
            _objectName = objectName;
        }

        public bool IsCreated
        {
            get { return _overlay != null; }
        }

        /// <summary>Create the overlay if it is not there yet; false if it could not be made.</summary>
        public bool Ensure()
        {
            if (_overlay != null && _segments != null)
            {
                return true;
            }

            _overlay = new GameObject(_objectName);
            Canvas canvas = _overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Keep the cursor above map visuals and the native overlay canvas
            // (29998), but below native tooltip canvases (30001) and windows.
            canvas.sortingOrder = 29999;
            CanvasScaler scaler = _overlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            CanvasGroup canvasGroup = _overlay.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            _segments = new[]
            {
                CreateSegment("Top"),
                CreateSegment("Right"),
                CreateSegment("Bottom"),
                CreateSegment("Left")
            };

            return _overlay != null && _segments != null;
        }

        /// <summary>Put the outline around a screen point and show it.</summary>
        public void MoveTo(Vector2 point)
        {
            if (_segments != null && _segments.Length == 4)
            {
                SetSegment(_segments[0], point + new Vector2(0f, Size * 0.5f), new Vector2(Size, Thickness));
                SetSegment(_segments[1], point + new Vector2(Size * 0.5f, 0f), new Vector2(Thickness, Size));
                SetSegment(_segments[2], point + new Vector2(0f, -Size * 0.5f), new Vector2(Size, Thickness));
                SetSegment(_segments[3], point + new Vector2(-Size * 0.5f, 0f), new Vector2(Thickness, Size));
            }

            _overlay?.SetActive(true);
        }

        /// <summary>Destroy the game object; the next <see cref="Ensure"/> builds a new one.</summary>
        public void Destroy()
        {
            if (_overlay == null)
            {
                _segments = null;
                return;
            }

            UnityEngine.Object.Destroy(_overlay);
            _overlay = null;
            _segments = null;
        }

        private RectTransform CreateSegment(string name)
        {
            GameObject segment = new GameObject(name);
            segment.transform.SetParent(_overlay.transform, false);
            Image image = segment.AddComponent<Image>();
            image.color = Color.yellow;
            image.raycastTarget = false;
            RectTransform rect = segment.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void SetSegment(RectTransform segment, Vector2 position, Vector2 size)
        {
            if (segment == null)
            {
                return;
            }

            segment.anchoredPosition = position;
            segment.sizeDelta = size;
        }
    }
}
