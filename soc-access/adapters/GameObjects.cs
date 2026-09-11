using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>The two questions an adapter asks of a game object before it reads anything off it:
    /// is it drawn, and does it still belong to a loaded scene. A destroyed Unity object compares
    /// equal to null, so both answer false for one rather than throwing.</summary>
    public static class GameObjects
    {
        /// <summary>The object exists and is drawn: active in its hierarchy.</summary>
        public static bool IsLive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        /// <summary>The component exists and its object is drawn.</summary>
        public static bool IsLive(Component component)
        {
            return component != null && IsLive(component.gameObject);
        }

        /// <summary>The object belongs to a scene that is still loaded, which is what separates a
        /// live menu from the prefab copy <c>Resources.FindObjectsOfTypeAll</c> also returns. It says
        /// nothing about whether the object is drawn.</summary>
        public static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        /// <summary>As above, for the object a component sits on.</summary>
        public static bool IsLiveSceneObject(Component component)
        {
            return component != null && IsLiveSceneObject(component.gameObject);
        }

        /// <summary>The container is drawn AND its canvas group is not hiding it: a HUD group the
        /// game faded out keeps its object active, so alpha, interactable and blocksRaycasts are the
        /// state that says whether the player can see and use what is inside. A container with no
        /// canvas group is simply drawn or not.</summary>
        public static bool IsGroupVisible(GameObject gameObject)
        {
            if (!IsLive(gameObject))
            {
                return false;
            }

            CanvasGroup canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                return true;
            }

            return canvasGroup.alpha > 0.01f && canvasGroup.interactable && canvasGroup.blocksRaycasts;
        }
    }
}
