using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class DialogueMenuAdapter : IStoryTextAdapter
    {
        private static readonly AccessTools.FieldRef<DialogueMenu, DialogueMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<DialogueMenu, DialogueMenu.Settings>("_settings");

        private static readonly FieldInfo StateMachineField =
            AccessTools.Field(typeof(DialogueMenu), "_stateMachine");

        private static readonly PropertyInfo CurrentStateTypeProperty =
            AccessTools.Property(StateMachineField.FieldType, "CurrentStateType");

        private static readonly MethodInfo HandlePrimaryClickedMethod =
            AccessTools.Method(typeof(DialogueMenu), "HandlePrimaryClicked");

        private readonly DialogueMenu _dialogueMenu;

        public DialogueMenuAdapter(DialogueMenu dialogueMenu)
        {
            _dialogueMenu = dialogueMenu;
        }

        public object SourceKey
        {
            get { return _dialogueMenu; }
        }

        public string Title
        {
            get
            {
                DialogueMenu.Settings settings = GetSettings();
                return SpeechTextSanitizer.Normalize(settings != null
                    ? UITextMeshTextUtility.GetEffectiveText(settings.NameText)
                    : string.Empty);
            }
        }

        public string Body
        {
            get
            {
                DialogueMenu.Settings settings = GetSettings();
                return SpeechTextSanitizer.Normalize(settings != null
                    ? UITextMeshTextUtility.GetEffectiveText(settings.DialogueText)
                    : string.Empty);
            }
        }

        public bool IsPresent()
        {
            if (_dialogueMenu == null)
            {
                return false;
            }

            DialogueMenu.Settings settings = GetSettings();
            if (settings == null || !IsContainerActiveInLoadedScene(settings.Container))
            {
                return false;
            }

            if (IsWaitingForInput())
            {
                return HasVisibleText(settings);
            }

            return IsTypingText() && HasVisibleText(settings) && GetMaxVisibleCharacters(settings.DialogueText) > 0;
        }

        public bool AdvanceNow()
        {
            // While the native dialogue is swapping entries, keep swallowing
            // repeated activations on the old accessibility page. The next
            // page is announced only after its body text has been assigned.
            if (DialogueMenuAdvanceGuard.IsPending(_dialogueMenu))
            {
                return true;
            }

            if (!IsPresent() || HandlePrimaryClickedMethod == null)
            {
                return false;
            }

            DialogueMenuAdvanceGuard.MarkPending(_dialogueMenu);
            bool wasTyping = IsTypingText();
            HandlePrimaryClickedMethod.Invoke(_dialogueMenu, null);
            if (wasTyping && IsWaitingForInput())
            {
                HandlePrimaryClickedMethod.Invoke(_dialogueMenu, null);
            }

            return true;
        }

        private DialogueMenu.Settings GetSettings()
        {
            return _dialogueMenu != null ? SettingsRef(_dialogueMenu) : null;
        }

        private bool IsTypingText()
        {
            return IsState("TypingText");
        }

        private bool IsWaitingForInput()
        {
            return IsState("WaitingForInput");
        }

        private bool IsState(string stateName)
        {
            if (_dialogueMenu == null || StateMachineField == null || CurrentStateTypeProperty == null)
            {
                return false;
            }

            object stateMachine = StateMachineField.GetValue(_dialogueMenu);
            object currentState = stateMachine != null ? CurrentStateTypeProperty.GetValue(stateMachine, null) : null;
            return string.Equals(currentState != null ? currentState.ToString() : string.Empty, stateName, StringComparison.Ordinal);
        }

        private static bool HasVisibleText(DialogueMenu.Settings settings)
        {
            return settings != null
                && (!string.IsNullOrWhiteSpace(UITextMeshTextUtility.GetEffectiveText(settings.NameText))
                    || !string.IsNullOrWhiteSpace(UITextMeshTextUtility.GetEffectiveText(settings.DialogueText)));
        }

        private static int GetMaxVisibleCharacters(UITextMesh textMesh)
        {
            TMP_Text tmpText = textMesh as TMP_Text;
            return tmpText != null ? tmpText.maxVisibleCharacters : int.MaxValue;
        }

        private static bool IsContainerActiveInLoadedScene(UITransform container)
        {
            Component component = container as Component;
            GameObject gameObject = component != null ? component.gameObject : null;
            return gameObject != null
                && gameObject.activeInHierarchy
                && gameObject.scene.IsValid()
                && gameObject.scene.isLoaded;
        }
    }
}
