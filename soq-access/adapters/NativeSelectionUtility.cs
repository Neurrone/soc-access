using SongsOfConquest.Client.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal static class NativeSelectionUtility
    {
        public static bool Select(Component component)
        {
            return component != null && Select(component.gameObject);
        }

        public static bool Select(Selectable selectable)
        {
            if (selectable == null)
            {
                return false;
            }

            if (Select(selectable.gameObject))
            {
                return true;
            }

            selectable.Select();
            return true;
        }

        public static bool Click(IUIButton button)
        {
            if (button == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            Component component = button as Component;
            IPointerClickHandler clickHandler = component as IPointerClickHandler;
            if (clickHandler != null)
            {
                PointerEventData eventData = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left
                };
                clickHandler.OnPointerClick(eventData);
                return true;
            }

            button.OnClicked?.Invoke();
            return true;
        }

        public static bool Select(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            if (EventSystem.current == null)
            {
                Selectable selectable = gameObject.GetComponent<Selectable>();
                if (selectable == null)
                {
                    return false;
                }

                selectable.Select();
                return true;
            }

            EventSystem.current.SetSelectedGameObject(gameObject);
            return true;
        }
    }
}
