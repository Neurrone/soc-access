using System.Collections.Generic;
using System.Reflection;
using SongsOfConquestAccess.UI;
using _8_UILayer.ClientView.Menu.Paus;
using HarmonyLib;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class PauseMenuAdapter : IPresent
    {
        private static readonly AccessTools.FieldRef<PauseMenu, PauseMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<PauseMenu, PauseMenu.Settings>("_settings");

        private readonly PauseMenu _pauseMenu;
        private List<Item> _items;
        private int _itemsFrame = -1;

        public PauseMenuAdapter(PauseMenu pauseMenu)
        {
            _pauseMenu = pauseMenu;
        }

        public string Title
        {
            get
            {
                PauseMenu.Settings settings = Settings;
                return SpokenLines.Clean(
                    UITextMeshTextUtility.GetEffectiveText(settings != null ? settings.titleText : null));
            }
        }

        /// <summary>The menu's visible buttons, listed at most once a frame: the screen's build asks
        /// whether the menu is present, which counts them, and then for the list itself.</summary>
        public IReadOnlyList<Item> Items
        {
            get
            {
                int frame = Time.frameCount;
                if (_items != null && _itemsFrame == frame)
                {
                    return _items;
                }

                _itemsFrame = frame;
                _items = BuildItems();
                return _items;
            }
        }

        private List<Item> BuildItems()
        {
            List<Item> items = new List<Item>();
            PauseMenu.Settings settings = Settings;
            if (settings == null)
            {
                return items;
            }

            AddItem(items, "continue", settings.continueButton);
            AddItem(items, "quick-save", settings.quickSaveButton);
            AddItem(items, "quick-load", settings.quickLoadButton);
            AddItem(items, "save", settings.saveButton);
            AddItem(items, "load", settings.loadButton);
            AddItem(items, "restart", settings.restartButton);
            AddItem(items, "options", settings.optionsButton);
            // The mod's own entry, drawn straight after Options by ModOptionsEntries. The pause
            // menu's items are fixed serialized fields rather than a list, so it is named here
            // as well as drawn there.
            AddItem(items, "mod-options", ModOptionsEntries.PauseMenuButton);
            AddItem(items, "tutorials", settings.tutorialsButton);
            AddItem(items, "quit-to-main-menu", settings.exitButton);
            AddItem(items, "quit-application", settings.quitButton);
            AddItem(items, "surrender", settings.surrenderButton);
            AddItem(items, "surrender-battle", settings.surrenderBattleOnlyButton);
            AddItem(items, "quit-to-map-editor", settings.quitToMapEditorButton);
            return items;
        }

        public bool IsPresent()
        {
            PauseMenu.Settings settings = Settings;
            if (settings == null)
            {
                return false;
            }

            if (!IsCanvasVisible(settings.ContainerCanvasGroup) || !IsTransformActive(settings.parent))
            {
                return false;
            }

            IReadOnlyList<Item> items = Items;
            return items != null && items.Count > 0;
        }

        private PauseMenu.Settings Settings
        {
            get
            {
                if (_pauseMenu == null)
                {
                    return null;
                }

                try
                {
                    return SettingsRef(_pauseMenu);
                }
                catch (System.Exception exception)
                {
                    SocAccessMod.Instance?.LogWarning("Failed to read PauseMenu settings: " + exception.Message);
                    return null;
                }
            }
        }

        private static void AddItem(List<Item> items, string id, UIButton button)
        {
            if (items == null || !MenuButtonAdapterBase.IsButtonVisible(button))
            {
                return;
            }

            string label = MenuButtonTextUtility.GetDirectButtonText(button);
            if (string.IsNullOrWhiteSpace(label))
            {
                SocAccessMod.Instance?.LogWarning("Skipping visible pause menu button with empty direct text: " + id);
                return;
            }

            items.Add(new Item(id, button));
        }

        private static bool IsCanvasVisible(CanvasGroup canvasGroup)
        {
            if (canvasGroup == null)
            {
                return false;
            }

            GameObject gameObject = canvasGroup.gameObject;
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static bool IsTransformActive(IUITransform transform)
        {
            return transform == null || transform.Active;
        }

        public sealed class Item : IMenuButtonAdapter
        {
            public Item(string id, UIButton button)
            {
                Id = id;
                Button = button;
            }

            public string Id { get; private set; }

            public UIButton Button { get; private set; }

            public string GetLabel()
            {
                return MenuButtonTextUtility.GetDirectButtonText(Button);
            }

            /// <summary>No status of its own; whether the button is refusing is
            /// <see cref="IsEnabled"/>, and the wording for that belongs to the screen.</summary>
            public string GetStatus()
            {
                return string.Empty;
            }

            public bool IsVisible()
            {
                return MenuButtonAdapterBase.IsButtonVisible(Button);
            }

            public bool IsEnabled()
            {
                return Button != null && Button.Interactable;
            }

            public bool Activate()
            {
                if (Button == null || !Button.Active || !Button.Interactable || !IsVisible())
                {
                    return false;
                }

                using (NativeScreenInputPositionOverride.Apply(Button.Position))
                {
                    return NativeSelectionUtility.Click(Button);
                }
            }

            public void Select()
            {
                if (Button != null)
                {
                    NativeSelectionUtility.Select(Button);
                }
            }
        }

        private sealed class NativeScreenInputPositionOverride : System.IDisposable
        {
            private readonly object _response;
            private readonly PropertyInfo _positionProperty;
            private readonly object _oldPosition;
            private bool _disposed;

            private NativeScreenInputPositionOverride(object response, PropertyInfo positionProperty, Vector2 position)
            {
                _response = response;
                _positionProperty = positionProperty;
                _oldPosition = _positionProperty.GetValue(_response, null);
                _positionProperty.SetValue(_response, position, null);
            }

            public static NativeScreenInputPositionOverride Apply(Vector2 position)
            {
                object response = ResolveWritablePrimaryResponse();
                if (response == null)
                {
                    SocAccessMod.Instance?.LogWarning("PauseMenuAdapter could not override native screen input position");
                    return null;
                }

                PropertyInfo positionProperty = AccessTools.Property(response.GetType(), "Position");
                if (positionProperty == null || !positionProperty.CanWrite)
                {
                    SocAccessMod.Instance?.LogWarning("PauseMenuAdapter could not override native screen input position because Position was not writable on " + response.GetType().FullName);
                    return null;
                }

                return new NativeScreenInputPositionOverride(response, positionProperty, position);
            }

            public void Dispose()
            {
                if (_disposed || _response == null || _positionProperty == null)
                {
                    return;
                }

                _positionProperty.SetValue(_response, _oldPosition, null);
                _disposed = true;
            }

            private static object ResolveWritablePrimaryResponse()
            {
                IInputManager inputManager = InputManagerStaticAccessUnsafe.Current;
                object response = inputManager != null && inputManager.Screen != null
                    ? inputManager.Screen.Primary
                    : null;
                if (response == null)
                {
                    return null;
                }

                if (HasWritablePosition(response))
                {
                    return response;
                }

                FieldInfo currentResponseField = AccessTools.Field(response.GetType(), "_currentResponse");
                object currentResponse = currentResponseField != null ? currentResponseField.GetValue(response) : null;
                if (HasWritablePosition(currentResponse))
                {
                    return currentResponse;
                }

                FieldInfo mouseResponseField = AccessTools.Field(response.GetType(), "_mouseResponse");
                object mouseResponse = mouseResponseField != null ? mouseResponseField.GetValue(response) : null;
                return HasWritablePosition(mouseResponse) ? mouseResponse : null;
            }

            private static bool HasWritablePosition(object response)
            {
                if (response == null)
                {
                    return false;
                }

                PropertyInfo property = AccessTools.Property(response.GetType(), "Position");
                return property != null && property.CanWrite;
            }
        }
    }
}
