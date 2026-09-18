using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Common.Dialogue;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class StoryCameraFocusPatches
    {
        // A weak table keyed on the game's own camera manager: an entry dies with the manager it is
        // keyed on, so nothing here survives a scene change or a hot reload and there is nothing for
        // a Reset to drop. It is on patch-statics.allow for the collection it is, not for what it
        // holds - which is the personas' name KEYS and world positions, never their names: the game
        // changes language without a restart, so a conversation opened in one language is spoken in
        // whichever is current when a focus is read.
        private static readonly ConditionalWeakTable<AdventureDialogueCameraManager, ConversationTargets> DialogueTargets =
            new ConditionalWeakTable<AdventureDialogueCameraManager, ConversationTargets>();

        private static StoryCameraFocusKey LastEmittedFocus;

        // The adventure game the key above was set in, held weakly as the scanner state holds it:
        // the client's adventure facade IS the running game (AdventureMapAdapter.AdventureGame),
        // bound AsSingle per game session. Quitting, loading a save or leaving a mission mid-series
        // never runs the completion postfix, and a replay's first story camera focus computes the
        // same key as the one the abandoned run announced, so it was swallowed.
        private static readonly WeakReference LastEmittedFocusGame = new WeakReference(null);

        private static readonly FieldInfo DialogueFacadeField =
            AccessTools.Field(typeof(AdventureDialogueCameraManager), "_facade");

        private static readonly FieldInfo DialogueConverterField =
            AccessTools.Field(typeof(AdventureDialogueCameraManager), "_converter");

        private static readonly FieldInfo DialogueInteractorIdField =
            AccessTools.Field(typeof(AdventureDialogueCameraManager), "_interactorId");

        [HarmonyPatch(typeof(ReactiveAdventureMenuSystem), "HandleTriggerSeriesCompleted")]
        [HarmonyPostfix]
        private static void HandleTriggerSeriesCompletedPostfix()
        {
            ResetDedupe();
        }

        [HarmonyPatch(typeof(MessageTriggerUtility), "HandleMessageTrigger")]
        [HarmonyPrefix]
        private static void HandleMessageTriggerPrefix(
            MessageTriggerData messageData,
            ILocalizationHandler localizationHandler,
            IClientAdventureFacade adventureFacade,
            ICommanderState interactingCommanderState)
        {
            if (messageData == null)
            {
                return;
            }

            if (messageData.MessageType != MessageType.StoryText
                && messageData.MessageType != MessageType.LetterBoxStoryText)
            {
                return;
            }

            PublishCameraFocusForIdentifier(
                messageData.Camera,
                adventureFacade,
                localizationHandler,
                interactingCommanderState);
        }

        [HarmonyPatch(typeof(AdventureDialogueCameraManager), "InitializeTargets")]
        [HarmonyPostfix]
        private static void InitializeTargetsPostfix(AdventureDialogueCameraManager __instance, DialogueMenu.PersonaInformation[] allPersonas)
        {
            if (__instance == null)
            {
                return;
            }

            ConversationTargets targets = BuildConversationTargets(allPersonas);
            DialogueTargets.Remove(__instance);
            DialogueTargets.Add(__instance, targets);
        }

        [HarmonyPatch(typeof(AdventureDialogueCameraManager), "Show")]
        [HarmonyPrefix]
        private static void DialogueCameraShowPrefix(
            AdventureDialogueCameraManager __instance,
            DialogueMenu.PersonaInformation currentPersona,
            DialogueDefinitionEntry entry)
        {
            if (__instance == null || entry == null)
            {
                return;
            }

            IClientAdventureFacade facade = GetDialogueFacade(__instance);
            ILocalizationHandler localizationHandler = GlobalLocalizationVariables.LocalizationHandler;

            if (entry.Camera.TargetType == CameraFocusPointTargetType.Point)
            {
                PublishCameraFocusForIdentifier(entry.Camera, facade, localizationHandler, null);
                return;
            }

            if (entry.Camera.TargetType == CameraFocusPointTargetType.Wielder)
            {
                ICommanderState commander = ResolveInteractingCommander(facade, __instance);
                StoryCameraFocusTarget target = StoryCameraFocusResolver.ResolveWielderTarget(facade, commander);
                PublishIfTarget(StoryCameraFocusKind.Wielder, target, entry.Camera.reference, facade);
                return;
            }

            ConversationTargets targets;
            if (!DialogueTargets.TryGetValue(__instance, out targets) || targets == null)
            {
                return;
            }

            List<StoryCameraFocusTarget> resolved = ResolveConversationTargets(__instance, targets);
            if (resolved.Count > 0)
            {
                PublishIfTargets(StoryCameraFocusKind.ConversationArea, resolved, entry.Camera.reference, facade);
            }
        }

        private static void PublishCameraFocusForIdentifier(
            CameraFocusPointIdentifier camera,
            IClientAdventureFacade facade,
            ILocalizationHandler localizationHandler,
            ICommanderState interactingCommanderState)
        {
            if (camera.TargetType == CameraFocusPointTargetType.Point)
            {
                StoryCameraFocusTarget target = StoryCameraFocusResolver.ResolvePointTarget(
                    facade,
                    localizationHandler,
                    camera);
                PublishIfTarget(StoryCameraFocusKind.Point, target, camera.reference, facade);
                return;
            }

            if (camera.TargetType == CameraFocusPointTargetType.Wielder)
            {
                StoryCameraFocusTarget target = StoryCameraFocusResolver.ResolveWielderTarget(facade, interactingCommanderState);
                PublishIfTarget(StoryCameraFocusKind.Wielder, target, camera.reference, facade);
            }
        }

        private static void PublishIfTarget(
            StoryCameraFocusKind kind,
            StoryCameraFocusTarget target,
            string reference,
            IClientAdventureFacade facade)
        {
            if (target == null)
            {
                return;
            }

            PublishIfTargets(kind, new[] { target }, reference, facade);
        }

        private static void PublishIfTargets(
            StoryCameraFocusKind kind,
            IEnumerable<StoryCameraFocusTarget> targets,
            string reference,
            IClientAdventureFacade facade)
        {
            if (!ModSettings.ReadStoryCameraFocusChanges)
            {
                return;
            }

            List<StoryCameraFocusTarget> targetList = targets != null
                ? targets.Where(target => target != null).ToList()
                : new List<StoryCameraFocusTarget>();
            if (targetList.Count == 0)
            {
                return;
            }

            // The dedupe key describes ONE adventure game: another game is another run of the same
            // story, whose first focus computes the same key. An unknown game (null) leaves the key
            // alone, as the scanner state's Rebind does.
            if (facade != null && !ReferenceEquals(facade, LastEmittedFocusGame.Target))
            {
                LastEmittedFocus = null;
                LastEmittedFocusGame.Target = facade;
            }

            StoryCameraFocusKey key = StoryCameraFocusKey.Create(kind, reference, targetList);
            // Native dialogue pages can re-apply the same conversation-area
            // camera focus on each page, and trigger-driven story pages can
            // close/reopen within one trigger series. Announce only real focus
            // changes until the native sequence completion hook resets this.
            if (key.Equals(LastEmittedFocus))
            {
                return;
            }

            LastEmittedFocus = key;
            AccessibilityEventBus.Publish(new StoryCameraFocusStartedEvent(kind, targetList, reference));
        }

        public static void ResetDedupe()
        {
            LastEmittedFocus = null;
            LastEmittedFocusGame.Target = null;
        }

        /// <summary>The teardown <c>SocAccessMod.Stop</c> calls. The dedupe key and the game it was
        /// set in are the only things this class keeps between calls, and the next load's first
        /// focus must not be swallowed as a repeat of one the previous load announced.</summary>
        public static void Reset()
        {
            ResetDedupe();
        }

        private static ConversationTargets BuildConversationTargets(DialogueMenu.PersonaInformation[] personas)
        {
            List<ConversationPersona> conversants = new List<ConversationPersona>();
            for (int i = 0; personas != null && i < personas.Length; i++)
            {
                DialogueMenu.PersonaInformation persona = personas[i];
                if (persona == null || !StoryCameraFocusResolver.IsValidWorldPosition(persona.WorldPosition))
                {
                    continue;
                }

                conversants.Add(new ConversationPersona(
                    persona.NameKey,
                    persona.NameKeyPluralCount,
                    persona.WorldPosition));
            }

            return new ConversationTargets(conversants);
        }

        /// <summary>Names the conversation's personas in the language that is current now, and the
        /// tiles they stand on as they are now.</summary>
        private static List<StoryCameraFocusTarget> ResolveConversationTargets(
            AdventureDialogueCameraManager cameraManager,
            ConversationTargets conversation)
        {
            List<StoryCameraFocusTarget> targets = new List<StoryCameraFocusTarget>();
            if (cameraManager == null || conversation == null || conversation.Personas.Count == 0)
            {
                return targets;
            }

            IClientAdventureFacade facade = GetDialogueFacade(cameraManager);
            object converter = GetDialogueConverter(cameraManager);
            ILocalizationHandler localizationHandler = GlobalLocalizationVariables.LocalizationHandler;
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < conversation.Personas.Count; i++)
            {
                ConversationPersona persona = conversation.Personas[i];
                string label = StoryCameraFocusResolver.LocalizeName(
                    localizationHandler,
                    persona.NameKey,
                    persona.NameKeyPluralCount);
                StoryCameraFocusTarget target = StoryCameraFocusResolver.ResolveWorldPositionTarget(
                    facade,
                    converter,
                    localizationHandler,
                    label,
                    persona.WorldPosition);
                if (target == null)
                {
                    continue;
                }

                string key = target.Label + "@" + target.Tile.x + "," + target.Tile.y;
                if (seen.Add(key))
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        private static IClientAdventureFacade GetDialogueFacade(AdventureDialogueCameraManager manager)
        {
            try
            {
                return manager != null && DialogueFacadeField != null
                    ? DialogueFacadeField.GetValue(manager) as IClientAdventureFacade
                    : null;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to read dialogue camera facade: " + exception.Message);
                return null;
            }
        }

        private static object GetDialogueConverter(AdventureDialogueCameraManager manager)
        {
            try
            {
                return manager != null && DialogueConverterField != null
                    ? DialogueConverterField.GetValue(manager)
                    : null;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to read dialogue camera converter: " + exception.Message);
                return null;
            }
        }

        private static ICommanderState ResolveInteractingCommander(
            IClientAdventureFacade facade,
            AdventureDialogueCameraManager manager)
        {
            if (facade == null || facade.Commanders == null || manager == null || DialogueInteractorIdField == null)
            {
                return null;
            }

            try
            {
                int interactorId = (int)DialogueInteractorIdField.GetValue(manager);
                return facade.Commanders.Get(interactorId);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to resolve dialogue interacting wielder: " + exception.Message);
                return null;
            }
        }

        private sealed class ConversationTargets
        {
            public ConversationTargets(List<ConversationPersona> personas)
            {
                Personas = personas ?? new List<ConversationPersona>();
            }

            public List<ConversationPersona> Personas { get; private set; }
        }

        /// <summary>One persona of a conversation, as the game gave it: a name KEY and where the
        /// persona stands, nothing localized.</summary>
        private struct ConversationPersona
        {
            public ConversationPersona(string nameKey, int nameKeyPluralCount, Vector3 worldPosition)
            {
                NameKey = nameKey ?? string.Empty;
                NameKeyPluralCount = nameKeyPluralCount;
                WorldPosition = worldPosition;
            }

            public string NameKey;
            public int NameKeyPluralCount;
            public Vector3 WorldPosition;
        }

        /// <summary>
        /// What one focus IS, in terms nothing but the game owns: the kind, the camera point the
        /// story names and the tiles focused. The names spoken are deliberately not part of it -
        /// the game changes language without a restart, and a key built from the words would stop
        /// matching the one the same focus set a moment earlier, announcing it a second time.
        /// </summary>
        private sealed class StoryCameraFocusKey
        {
            private StoryCameraFocusKey(StoryCameraFocusKind kind, string reference, List<string> targets)
            {
                Kind = kind;
                Reference = reference ?? string.Empty;
                Targets = targets ?? new List<string>();
            }

            private StoryCameraFocusKind Kind { get; set; }

            private string Reference { get; set; }

            private List<string> Targets { get; set; }

            public static StoryCameraFocusKey Create(StoryCameraFocusKind kind, string reference, IReadOnlyList<StoryCameraFocusTarget> targets)
            {
                List<string> targetKeys = new List<string>();
                if (targets != null)
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        StoryCameraFocusTarget target = targets[i];
                        if (target != null)
                        {
                            targetKeys.Add(target.Tile.x + "," + target.Tile.y);
                        }
                    }
                }

                targetKeys.Sort(StringComparer.Ordinal);
                return new StoryCameraFocusKey(kind, reference, targetKeys);
            }

            public override bool Equals(object obj)
            {
                StoryCameraFocusKey other = obj as StoryCameraFocusKey;
                if (other == null
                    || Kind != other.Kind
                    || !string.Equals(Reference, other.Reference, StringComparison.Ordinal)
                    || Targets.Count != other.Targets.Count)
                {
                    return false;
                }

                for (int i = 0; i < Targets.Count; i++)
                {
                    if (!string.Equals(Targets[i], other.Targets[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Kind;
                    for (int i = 0; i < Targets.Count; i++)
                    {
                        hash = (hash * 397) ^ Targets[i].GetHashCode();
                    }

                    return hash;
                }
            }
        }
    }
}
