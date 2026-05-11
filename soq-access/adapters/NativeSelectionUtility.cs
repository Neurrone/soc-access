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

            return false;
        }

        public static bool PointerClick(Component component)
        {
            return Click(component, PointerEventData.InputButton.Left);
        }

        public static bool PointerRightClick(Component component)
        {
            return Click(component, PointerEventData.InputButton.Right);
        }

        public static bool PointerEnter(Component component)
        {
            if (component == null)
            {
                return false;
            }

            IPointerEnterHandler enterHandler = component as IPointerEnterHandler;
            if (enterHandler == null)
            {
                enterHandler = component.GetComponent<IPointerEnterHandler>();
            }

            if (enterHandler == null)
            {
                return false;
            }

            enterHandler.OnPointerEnter(new PointerEventData(EventSystem.current));
            return true;
        }

        public static bool PointerExit(Component component)
        {
            if (component == null)
            {
                return false;
            }

            IPointerExitHandler exitHandler = component as IPointerExitHandler;
            if (exitHandler == null)
            {
                exitHandler = component.GetComponent<IPointerExitHandler>();
            }

            if (exitHandler == null)
            {
                return false;
            }

            exitHandler.OnPointerExit(new PointerEventData(EventSystem.current));
            return true;
        }

        private static bool Click(Component component, PointerEventData.InputButton button)
        {
            if (component == null)
            {
                return false;
            }

            IPointerClickHandler clickHandler = component as IPointerClickHandler;
            if (clickHandler == null)
            {
                clickHandler = component.GetComponent<IPointerClickHandler>();
            }

            if (clickHandler == null)
            {
                return false;
            }

            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                button = button
            };
            clickHandler.OnPointerClick(eventData);
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
