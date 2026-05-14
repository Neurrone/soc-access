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
    internal static class StoryCameraFocusPatches
    {
        private static readonly ConditionalWeakTable<AdventureDialogueCameraManager, ConversationTargets> DialogueTargets =
            new ConditionalWeakTable<AdventureDialogueCameraManager, ConversationTargets>();

        private static readonly Dictionary<object, StoryCameraFocusKey> LastFocusBySource =
            new Dictionary<object, StoryCameraFocusKey>(ReferenceEqualityComparer.Instance);

        private static readonly object MessageFocusSource = new object();

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
            object converter,
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
                MessageFocusSource,
                messageData.Camera,
                adventureFacade,
                converter,
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

            ConversationTargets targets = BuildConversationTargets(__instance, allPersonas);
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
            object converter = GetDialogueConverter(__instance);
            ILocalizationHandler localizationHandler = GlobalLocalizationVariables.LocalizationHandler;

            if (entry.Camera.TargetType == CameraFocusPointTargetType.Point)
            {
                PublishCameraFocusForIdentifier(__instance, entry.Camera, facade, converter, localizationHandler, null);
                return;
            }

            if (entry.Camera.TargetType == CameraFocusPointTargetType.Wielder)
            {
                ICommanderState commander = ResolveInteractingCommander(facade, __instance);
                StoryCameraFocusTarget target = StoryCameraFocusResolver.ResolveWielderTarget(facade, commander);
                PublishIfTarget(__instance, StoryCameraFocusKind.Wielder, target, entry.Camera.reference);
                return;
            }

            ConversationTargets targets;
            if (DialogueTargets.TryGetValue(__instance, out targets) && targets != null && targets.Targets.Count > 0)
            {
                PublishIfTargets(__instance, StoryCameraFocusKind.ConversationArea, targets.Targets, entry.Camera.reference);
            }
        }

        private static void PublishCameraFocusForIdentifier(
            object source,
            CameraFocusPointIdentifier camera,
            IClientAdventureFacade facade,
            object converter,
            ILocalizationHandler localizationHandler,
            ICommanderState interactingCommanderState)
        {
            if (camera.TargetType == CameraFocusPointTargetType.Point)
            {
                StoryCameraFocusTarget target = StoryCameraFocusResolver.ResolvePointTarget(
                    facade,
                    converter,
                    localizationHandler,
                    camera);
                PublishIfTarget(source, StoryCameraFocusKind.Point, target, camera.reference);
                return;
            }

            if (camera.TargetType == CameraFocusPointTargetType.Wielder)
            {
                StoryCameraFocusTarget target = StoryCameraFocusResolver.ResolveWielderTarget(facade, interactingCommanderState);
                PublishIfTarget(source, StoryCameraFocusKind.Wielder, target, camera.reference);
            }
        }

        private static void PublishIfTarget(object source, StoryCameraFocusKind kind, StoryCameraFocusTarget target, string reference)
        {
            if (target == null)
            {
                return;
            }

            PublishIfTargets(source, kind, new[] { target }, reference);
        }

        private static void PublishIfTargets(object source, StoryCameraFocusKind kind, IEnumerable<StoryCameraFocusTarget> targets, string reference)
        {
            List<StoryCameraFocusTarget> targetList = targets != null
                ? targets.Where(target => target != null).ToList()
                : new List<StoryCameraFocusTarget>();
            if (targetList.Count == 0)
            {
                return;
            }

            StoryCameraFocusKey key = StoryCameraFocusKey.Create(kind, reference, targetList);
            object resolvedSource = source ?? MessageFocusSource;
            StoryCameraFocusKey previous;
            // Native dialogue pages can re-apply the same conversation-area
            // camera focus on each page, and trigger-driven story pages can
            // close/reopen within one trigger series. Announce only real focus
            // changes until the native sequence completion hook resets this.
            if (LastFocusBySource.TryGetValue(resolvedSource, out previous) && key.Equals(previous))
            {
                return;
            }

            LastFocusBySource[resolvedSource] = key;
            AccessibilityEventBus.Publish(new StoryCameraFocusStartedEvent(kind, targetList, reference));
        }

        public static void ResetDedupe()
        {
            LastFocusBySource.Clear();
        }

        private static ConversationTargets BuildConversationTargets(
            AdventureDialogueCameraManager cameraManager,
            DialogueMenu.PersonaInformation[] personas)
        {
            List<StoryCameraFocusTarget> targets = new List<StoryCameraFocusTarget>();
            if (cameraManager == null || personas == null || personas.Length == 0)
            {
                return new ConversationTargets(targets);
            }

            IClientAdventureFacade facade = GetDialogueFacade(cameraManager);
            object converter = GetDialogueConverter(cameraManager);
            ILocalizationHandler localizationHandler = GlobalLocalizationVariables.LocalizationHandler;
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < personas.Length; i++)
            {
                DialogueMenu.PersonaInformation persona = personas[i];
                if (persona == null || !StoryCameraFocusResolver.IsValidWorldPosition(persona.WorldPosition))
                {
                    continue;
                }

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

            return new ConversationTargets(targets);
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
                SocAccessPlugin.Instance?.LogWarning("Failed to read dialogue camera facade: " + exception.Message);
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
                SocAccessPlugin.Instance?.LogWarning("Failed to read dialogue camera converter: " + exception.Message);
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
                SocAccessPlugin.Instance?.LogWarning("Failed to resolve dialogue interacting wielder: " + exception.Message);
                return null;
            }
        }

        private static Vector2Int? WorldPositionToTile(object converter, Vector3 worldPosition)
        {
            return StoryCameraFocusResolver.TryWorldToTile(converter, worldPosition);
        }

        private sealed class ConversationTargets
        {
            public ConversationTargets(List<StoryCameraFocusTarget> targets)
            {
                Targets = targets ?? new List<StoryCameraFocusTarget>();
            }

            public List<StoryCameraFocusTarget> Targets { get; private set; }
        }

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
                            targetKeys.Add((target.Label ?? string.Empty) + "@" + target.Tile.x + "," + target.Tile.y);
                        }
                    }
                }

                return new StoryCameraFocusKey(kind, reference, targetKeys);
            }

            public override bool Equals(object obj)
            {
                StoryCameraFocusKey other = obj as StoryCameraFocusKey;
                if (other == null || Kind != other.Kind || Reference != other.Reference || Targets.Count != other.Targets.Count)
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
                    hash = (hash * 397) ^ Reference.GetHashCode();
                    for (int i = 0; i < Targets.Count; i++)
                    {
                        hash = (hash * 397) ^ Targets[i].GetHashCode();
                    }

                    return hash;
                }
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
