using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using ModIOBrowser;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.UI
{
    internal sealed class TmpInputFieldWidget : Widget
    {
        private readonly string _label;
        private readonly Func<TMP_InputField> _getField;
        private readonly bool _activateAfterEndOfFrame;
        private readonly TextInputEchoHelper _echo = new TextInputEchoHelper();
        private bool _hasSavedVirtualKeyboardSetting;
        private bool _savedVirtualKeyboardSetting;
        private bool _isFocused;

        public TmpInputFieldWidget(string id, string label, Func<TMP_InputField> getField, bool activateAfterEndOfFrame = false)
            : base(id)
        {
            _label = label ?? string.Empty;
            _getField = getField;
            _activateAfterEndOfFrame = activateAfterEndOfFrame;
        }

        public override bool IsVisible
        {
            get
            {
                TMP_InputField field = GetField();
                return field != null && field.gameObject.activeInHierarchy;
            }
        }

        public override string GetLabel()
        {
            TMP_InputField field = GetField();
            string value = field != null ? field.text : string.Empty;
            return string.IsNullOrWhiteSpace(value)
                ? _label
                : ModText.Get(ModStrings.Common.ListSeparator, _label, value);
        }

        public override string GetRole()
        {
            return ModText.Get(ModStrings.UI.RoleEdit);
        }

        public override string GetStatus()
        {
            TMP_InputField field = GetField();
            return field == null || field.interactable
                ? string.Empty
                : ModText.Get(ModStrings.UI.StatusDisabled);
        }

        public override bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.Activate.Key;
        }

        public override bool HandleAction(InputAction action)
        {
            if (action == null || action.Key != AccessibilityActions.Activate.Key)
            {
                return false;
            }

            TMP_InputField field = GetField();
            if (field == null || !field.interactable)
            {
                return false;
            }

            DisableVirtualKeyboardDelegateWhileEditing();
            ActivateOrScheduleNativeField(field);
            return true;
        }

        public override void Update()
        {
            _echo.Update();
        }

        protected override void OnFocus()
        {
            _isFocused = true;
            TMP_InputField field = GetField();
            if (field == null || !field.interactable)
            {
                return;
            }

            DisableVirtualKeyboardDelegateWhileEditing();
            ActivateOrScheduleNativeField(field);
            _echo.Begin(field);
        }

        protected override void OnUnfocus()
        {
            _isFocused = false;
            _echo.Stop();
            TMP_InputField field = GetField();
            if (field != null)
            {
                field.DeactivateInputField();
            }

            RestoreVirtualKeyboardDelegateSetting();
        }

        private TMP_InputField GetField()
        {
            return _getField != null ? _getField() : null;
        }

        private void ActivateOrScheduleNativeField(TMP_InputField field)
        {
            if (!_activateAfterEndOfFrame)
            {
                ActivateNativeField(field);
                return;
            }

            SocAccessMod.Instance?.StartCoroutine(ActivateAfterEndOfFrame(field));
        }

        private IEnumerator ActivateAfterEndOfFrame(TMP_InputField field)
        {
            yield return new WaitForEndOfFrame();
            if (!_isFocused || field == null || !field.gameObject.activeInHierarchy || !field.interactable)
            {
                yield break;
            }

            if (!ReferenceEquals(field, GetField()))
            {
                yield break;
            }

            ActivateNativeField(field);
        }

        private static void ActivateNativeField(TMP_InputField field)
        {
            SelectViaModIoNavigation(field);
            field.Select();
            field.ActivateInputField();
        }

        private static void SelectViaModIoNavigation(TMP_InputField field)
        {
            if (field == null)
            {
                return;
            }

            Type type = AccessTools.TypeByName("ModIOBrowser.InputNavigation");
            MethodInfo select = type != null ? AccessTools.Method(type, "Select", new[] { typeof(Selectable), typeof(bool) }) : null;
            if (type == null || select == null)
            {
                return;
            }

            UnityEngine.Object[] instances = Resources.FindObjectsOfTypeAll(type);
            if (instances.Length == 0)
            {
                return;
            }

            select.Invoke(instances[0], new object[] { field, true });
        }

        private void DisableVirtualKeyboardDelegateWhileEditing()
        {
            if (SharedUi.settings == null)
            {
                return;
            }

            if (!_hasSavedVirtualKeyboardSetting)
            {
                _savedVirtualKeyboardSetting = SharedUi.settings.StandaloneUsesVKDelegate;
                _hasSavedVirtualKeyboardSetting = true;
            }

            SharedUi.settings.StandaloneUsesVKDelegate = false;
        }

        private void RestoreVirtualKeyboardDelegateSetting()
        {
            if (!_hasSavedVirtualKeyboardSetting || SharedUi.settings == null)
            {
                _hasSavedVirtualKeyboardSetting = false;
                return;
            }

            SharedUi.settings.StandaloneUsesVKDelegate = _savedVirtualKeyboardSetting;
            _hasSavedVirtualKeyboardSetting = false;
        }
    }
}
