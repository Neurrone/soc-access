using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Dialogue;
using SongsOfConquestAccess.UI;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class DialogueMenuAdapter : IStoryTextAdapter
    {
        private static readonly AccessTools.FieldRef<DialogueMenu, DialogueMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<DialogueMenu, DialogueMenu.Settings>("_settings");

        private static readonly FieldInfo StateMachineField =
            AccessTools.Field(typeof(DialogueMenu), "_stateMachine");

        private static readonly FieldInfo CurrentDialogueField =
            AccessTools.Field(typeof(DialogueMenu), "_currentDialogue");

        private static readonly FieldInfo CurrentEntryIndexField =
            AccessTools.Field(typeof(DialogueMenu), "_currentEntryIndex");

        private static readonly FieldInfo ActiveConversantsField =
            AccessTools.Field(typeof(DialogueMenu), "_activeConversants");

        private static readonly MethodInfo GetHeaderTextMethod =
            AccessTools.Method(typeof(DialogueMenu), "GetHeaderText");

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
                string nativeHeader;
                if (TryGetNativeHeaderText(out nativeHeader))
                {
                    return SpokenLines.Clean(nativeHeader);
                }

                return SpokenLines.Clean(GetVisibleNameText(GetSettings()));
            }
        }

        public string Body
        {
            get { return _body.Joined(RawBody); }
        }

        /// <summary>Whether the menu is drawing any line at all.</summary>
        public bool HasBody
        {
            get { return _body.HasAny(RawBody); }
        }

        /// <summary>The paragraphs the game broke the line into, kept apart rather than collapsed:
        /// the dialogue is read a paragraph at a time.</summary>
        public IList<string> BodyLines
        {
            get { return _body.Lines(RawBody); }
        }

        // The line, split at most once a frame and re-split whenever the game rewrites it, which it
        // does a letter at a time while it types (AGENTS.md, Performance).
        private readonly BodyText _body = new BodyText();

        private string RawBody
        {
            get
            {
                DialogueMenu.Settings settings = GetSettings();
                return settings != null ? UITextMeshTextUtility.GetEffectiveText(settings.DialogueText) : string.Empty;
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

            object state = CurrentState();
            if (IsWaitingForInput(state))
            {
                return HasVisibleText(settings);
            }

            return IsTypingText(state) && HasVisibleText(settings) && GetMaxVisibleCharacters(settings.DialogueText) > 0;
        }

        public bool AdvanceNow()
        {
            if (!IsPresent() || HandlePrimaryClickedMethod == null)
            {
                return false;
            }

            bool wasTyping = IsTypingText();
            HandlePrimaryClickedMethod.Invoke(_dialogueMenu, null);
            if (wasTyping && IsWaitingForInput())
            {
                HandlePrimaryClickedMethod.Invoke(_dialogueMenu, null);
            }

            // The advance can move the menu on to the next line inside this frame, so the header
            // read above is no longer the one being drawn.
            _headerFrame = -1;
            return true;
        }

        private DialogueMenu.Settings GetSettings()
        {
            return _dialogueMenu != null ? SettingsRef(_dialogueMenu) : null;
        }

        /// <summary>Which state the menu's own state machine is in, read live: the mod's own advance
        /// moves it inside the frame, so it is never remembered across a call.</summary>
        private object CurrentState()
        {
            ProbeStateMachine();
            if (_dialogueMenu == null || StateMachineField == null || CurrentStateTypeProperty == null)
            {
                return null;
            }

            object stateMachine = StateMachineField.GetValue(_dialogueMenu);
            return stateMachine != null ? CurrentStateTypeProperty.GetValue(stateMachine, null) : null;
        }

        private bool IsTypingText()
        {
            return IsTypingText(CurrentState());
        }

        private bool IsWaitingForInput()
        {
            return IsWaitingForInput(CurrentState());
        }

        // The state is the game's own private enum, so it is compared as the VALUE it is rather than
        // through ToString, which allocated a string on every read and ran twice per build.
        private static bool IsTypingText(object state)
        {
            return state != null && TypingTextState != null && state.Equals(TypingTextState);
        }

        private static bool IsWaitingForInput(object state)
        {
            return state != null && WaitingForInputState != null && state.Equals(WaitingForInputState);
        }

        // Probed on first use, not in a static initialiser: the state machine's field handle is what
        // names the enum's type, and a renamed game field would otherwise throw inside this type's
        // constructor and take the whole adapter down with it.
        private static PropertyInfo CurrentStateTypeProperty;
        private static object TypingTextState;
        private static object WaitingForInputState;
        private static bool StateMachineProbed;

        private static void ProbeStateMachine()
        {
            if (StateMachineProbed)
            {
                return;
            }

            StateMachineProbed = true;
            if (StateMachineField == null)
            {
                return;
            }

            CurrentStateTypeProperty = AccessTools.Property(StateMachineField.FieldType, "CurrentStateType");
            Type stateType = CurrentStateTypeProperty != null ? CurrentStateTypeProperty.PropertyType : null;
            if (stateType == null || !stateType.IsEnum)
            {
                return;
            }

            TypingTextState = StateValue(stateType, "TypingText");
            WaitingForInputState = StateValue(stateType, "WaitingForInput");
        }

        private static object StateValue(Type stateType, string name)
        {
            return Enum.IsDefined(stateType, name) ? Enum.Parse(stateType, name) : null;
        }

        private bool HasVisibleText(DialogueMenu.Settings settings)
        {
            string nativeHeader;
            return settings != null
                && ((!TryGetNativeHeaderText(out nativeHeader) && !string.IsNullOrWhiteSpace(GetVisibleNameText(settings)))
                    || !string.IsNullOrWhiteSpace(nativeHeader)
                    || !string.IsNullOrWhiteSpace(UITextMeshTextUtility.GetEffectiveText(settings.DialogueText)));
        }

        private static int GetMaxVisibleCharacters(UITextMesh textMesh)
        {
            TMP_Text tmpText = textMesh as TMP_Text;
            return tmpText != null ? tmpText.maxVisibleCharacters : int.MaxValue;
        }

        /// <summary>Who is speaking, composed by the game's own GetHeaderText. Read once a frame:
        /// the screen's name, its title node and the presence check all ask for it, and the answer is
        /// one reflective invoke (AGENTS.md, Performance). The mod's own advance is what can change
        /// it inside a frame, and it drops this.</summary>
        private bool TryGetNativeHeaderText(out string header)
        {
            int frame = Time.frameCount;
            if (_headerFrame != frame)
            {
                _headerFrame = frame;
                _headerFound = ReadNativeHeaderText(out _header);
            }

            header = _header;
            return _headerFound;
        }

        private string _header = string.Empty;
        private bool _headerFound;
        private int _headerFrame = -1;

        private bool ReadNativeHeaderText(out string header)
        {
            header = string.Empty;
            if (_dialogueMenu == null
                || CurrentDialogueField == null
                || CurrentEntryIndexField == null
                || ActiveConversantsField == null
                || GetHeaderTextMethod == null)
            {
                return false;
            }

            try
            {
                DialogueDefinition dialogue = CurrentDialogueField.GetValue(_dialogueMenu) as DialogueDefinition;
                if (dialogue == null || dialogue.Entries == null)
                {
                    return false;
                }

                int entryIndex = (int)CurrentEntryIndexField.GetValue(_dialogueMenu);
                if (entryIndex < 0 || entryIndex >= dialogue.Entries.Count)
                {
                    return false;
                }

                DialogueDefinitionEntry entry = dialogue.Entries[entryIndex];
                Dictionary<string, DialogueMenu.PersonaInformation> conversants =
                    ActiveConversantsField.GetValue(_dialogueMenu) as Dictionary<string, DialogueMenu.PersonaInformation>;
                DialogueMenu.PersonaInformation conversant;
                if (entry == null
                    || conversants == null
                    || !conversants.TryGetValue(entry.UniqueIdentifier, out conversant))
                {
                    return false;
                }

                header = GetHeaderTextMethod.Invoke(_dialogueMenu, new object[] { entry, conversant }) as string ?? string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to resolve dialogue header text: " + exception.Message);
                return false;
            }
        }

        private static string GetVisibleNameText(DialogueMenu.Settings settings)
        {
            return settings != null && IsNameVisible(settings)
                ? UITextMeshTextUtility.GetEffectiveText(settings.NameText)
                : string.Empty;
        }

        private static bool IsNameVisible(DialogueMenu.Settings settings)
        {
            if (settings == null || settings.NameText == null)
            {
                return false;
            }

            Component component = settings.NameText as Component;
            return component != null
                && component.gameObject != null
                && component.gameObject.activeInHierarchy
                && (settings.NameCanvasGroup == null || settings.NameCanvasGroup.alpha > 0.001f);
        }

        private static bool IsContainerActiveInLoadedScene(UITransform container)
        {
            Component component = container as Component;
            GameObject gameObject = component != null ? component.gameObject : null;
            return GameObjects.IsLive(gameObject) && GameObjects.IsLiveSceneObject(gameObject);
        }
    }
}
