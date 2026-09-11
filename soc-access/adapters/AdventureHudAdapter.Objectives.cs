using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Commander;
using SongsOfConquest.Common.Levels;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Objectives;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// THE OBJECTIVES PANEL: the rows the game draws, the state each one is in, and where its
    /// marker stands relative to the selected wielder. The rows are pooled, so they are snapshotted
    /// once per frame rather than read per question.
    ///
    /// Split out of AdventureHudAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureHudAdapter
    {
        public bool IsObjectivesMenuVisible()
        {
            bool hudVisible = IsObjectivesHudVisible();
            int entryCount = hudVisible ? GetObjectiveEntryCount() : 0;
            return hudVisible && entryCount > 0;
        }

        public bool IsObjectiveVisible(int index)
        {
            return GetObjectiveSnapshot(index) != null;
        }

        /// <summary>What the objectives panel draws this entry as, empty where it draws nothing.
        /// </summary>
        public string GetObjectiveText(int index)
        {
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            return snapshot != null ? UITextMeshTextUtility.Spoken(snapshot.Text) : string.Empty;
        }

        /// <summary>Whether this entry is a lose condition rather than something to achieve.</summary>
        public bool IsObjectiveLoseCondition(int index)
        {
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            return snapshot != null && snapshot.IsLoseCondition;
        }

        /// <summary>Whether every objective of this entry has reached full progress.</summary>
        public bool IsObjectiveComplete(int index)
        {
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            return snapshot != null && snapshot.IsComplete;
        }

        /// <summary>Whether the game still counts this entry as achievable.</summary>
        public bool CanObjectiveBeCompleted(int index)
        {
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            return snapshot != null && snapshot.CanBeCompleted;
        }

        /// <summary>
        /// Where this entry's marker stands relative to the selected wielder, in tiles, together with
        /// the size of the map it stands on. Answers false unless the entry has exactly ONE unfinished
        /// objective with a location and there is a living wielder selected to measure from.
        /// </summary>
        public bool TryGetObjectiveMarkerOffset(int index, out Vector2Int offset, out Vector2Int mapSize)
        {
            offset = Vector2Int.zero;
            mapSize = Vector2Int.zero;
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            Objective objective;
            if (snapshot == null || !TryGetSingleObjectiveMarker(snapshot, out objective))
            {
                return false;
            }

            ICommanderState selectedCommander = SelectionHandler != null ? SelectionHandler.SelectedCommander : null;
            if (selectedCommander == null || !selectedCommander.IsAlive)
            {
                return false;
            }

            int mapWidth = _map != null && _map.Facade != null && _map.Facade.Level != null ? _map.Facade.Level.Width : 0;
            int mapHeight = _map != null && _map.Facade != null && _map.Facade.Level != null ? _map.Facade.Level.Height : 0;
            if (mapWidth <= 0 || mapHeight <= 0)
            {
                return false;
            }

            offset = new Vector2Int(
                objective.location.x - selectedCommander.Position.x,
                objective.location.y - selectedCommander.Position.y);
            mapSize = new Vector2Int(mapWidth, mapHeight);
            return true;
        }

        private static bool TryGetSingleObjectiveMarker(ObjectiveEntrySnapshot snapshot, out Objective objective)
        {
            objective = null;
            IReadOnlyList<Objective> objectives = snapshot != null && snapshot.Entry != null ? snapshot.Entry.Objectives : null;
            if (objectives == null)
            {
                return false;
            }

            for (int i = 0; i < objectives.Count; i++)
            {
                Objective candidate = objectives[i];
                if (candidate == null || !candidate.hasLocation || candidate.progress >= 1f)
                {
                    continue;
                }

                if (objective != null)
                {
                    objective = null;
                    return false;
                }

                objective = candidate;
            }

            return objective != null;
        }

        public void FocusObjective(int index)
        {
            InvokeNoArgs(ObjectivesHud, ObjectivesHudExpandMethod);
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            Component component = snapshot != null ? snapshot.Text : null;
            if (component != null)
            {
                NativeSelectionUtility.Select(component);
                NativeSelectionUtility.PointerEnter(component);
            }
        }

        public void UnfocusObjective()
        {
            InvokeNoArgs(ObjectivesHud, ObjectivesHudShrinkMethod);
        }

        public Tooltip GetObjectiveTooltip(int index)
        {
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            return Tooltip.ForComponent(snapshot != null ? snapshot.Text : null, LocalizationHandler);
        }

        private int GetObjectiveEntryCount()
        {
            return GetObjectiveSnapshots().Count;
        }

        private bool IsObjectivesHudVisible()
        {
            if (!IsAdventureHudVisible())
            {
                return false;
            }

            GameObject container = HudStateSettings != null ? HudStateSettings.ObjectivesContainer : null;
            if (HudGroupVisible(container))
            {
                return true;
            }

            ObjectivesHUD.Settings settings = ObjectivesSettings;
            return GameObjects.IsLive(settings != null ? settings.ObjectiveHeader as Component : null)
                || GameObjects.IsLive(ObjectivesEntryContainer as Component);
        }

        /// <summary>The objectives as the HUD is drawing them, walked at most once a frame: the map's
        /// build asks whether the menu is visible, then whether each of sixteen slots is, then for
        /// each one's tooltip, which is about seventeen walks of the same subtree for one frame's
        /// worth of unchanged rows.</summary>
        private List<ObjectiveEntrySnapshot> GetObjectiveSnapshots()
        {
            int frame = Time.frameCount;
            if (_objectiveSnapshots != null && _objectiveSnapshotsFrame == frame)
            {
                return _objectiveSnapshots;
            }

            _objectiveSnapshotsFrame = frame;
            _objectiveSnapshots = BuildObjectiveSnapshots();
            return _objectiveSnapshots;
        }

        private List<ObjectiveEntrySnapshot> BuildObjectiveSnapshots()
        {
            Transform container = ObjectivesEntryContainer;
            List<ObjectiveEntrySnapshot> result = new List<ObjectiveEntrySnapshot>();
            if (container == null || !container.gameObject.activeInHierarchy)
            {
                return result;
            }

            Dictionary<UITextMesh, IObjectivesHUDEntry> entriesByText = GetObjectiveEntriesByText();
            UITextMesh[] texts = container.GetComponentsInChildren<UITextMesh>(false);
            for (int i = 0; i < texts.Length; i++)
            {
                UITextMesh text = texts[i];
                if (text != null && GameObjects.IsLive(text) && !string.IsNullOrWhiteSpace(UITextMeshTextUtility.Spoken(text)))
                {
                    IObjectivesHUDEntry entry;
                    entriesByText.TryGetValue(text, out entry);
                    if (entry != null)
                    {
                        result.Add(new ObjectiveEntrySnapshot(text, entry));
                    }
                }
            }

            return result;
        }

        private ObjectiveEntrySnapshot GetObjectiveSnapshot(int index)
        {
            List<ObjectiveEntrySnapshot> snapshots = GetObjectiveSnapshots();
            return index >= 0 && index < snapshots.Count ? snapshots[index] : null;
        }

        private Dictionary<UITextMesh, IObjectivesHUDEntry> GetObjectiveEntriesByText()
        {
            Dictionary<UITextMesh, IObjectivesHUDEntry> result = new Dictionary<UITextMesh, IObjectivesHUDEntry>();
            ObjectivesHUD hud = ObjectivesHud;
            if (hud == null || ObjectivesHudObjectiveEntriesField == null)
            {
                return result;
            }

            object rawEntries = ObjectivesHudObjectiveEntriesField.GetValue(hud);
            IEnumerable<IObjectivesHUDEntry> entries = rawEntries as IEnumerable<IObjectivesHUDEntry>;
            if (entries == null)
            {
                return result;
            }

            foreach (IObjectivesHUDEntry entry in entries)
            {
                UITextMesh text = GetObjectiveEntryText(entry);
                if (text != null && !result.ContainsKey(text))
                {
                    result.Add(text, entry);
                }
            }

            return result;
        }

        private UITextMesh GetObjectiveEntryText(IObjectivesHUDEntry entry)
        {
            if (entry == null || ObjectivesHudEntrySettingsField == null || ObjectivesHudEntryObjectiveTextField == null)
            {
                return null;
            }

            try
            {
                object settings = ObjectivesHudEntrySettingsField.GetValue(entry);
                return settings != null ? ObjectivesHudEntryObjectiveTextField.GetValue(settings) as UITextMesh : null;
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading an objective row's text mesh", exception);
                return null;
            }
        }

        private sealed class ObjectiveEntrySnapshot
        {
            public ObjectiveEntrySnapshot(UITextMesh text, IObjectivesHUDEntry entry)
            {
                Text = text;
                Entry = entry;
            }

            public UITextMesh Text { get; private set; }

            public IObjectivesHUDEntry Entry { get; private set; }

            public bool IsComplete
            {
                get
                {
                    IReadOnlyList<Objective> objectives = Entry != null ? Entry.Objectives : null;
                    if (objectives == null || objectives.Count == 0)
                    {
                        return false;
                    }

                    if (objectives.Count == 1 && objectives[0].currentValue == 0 && objectives[0].maxValue == 0)
                    {
                        return true;
                    }

                    for (int i = 0; i < objectives.Count; i++)
                    {
                        if (objectives[i] == null || objectives[i].progress < 1f)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public bool CanBeCompleted
            {
                get { return Entry != null && Entry.CanBeCompleted; }
            }

            public bool IsLoseCondition
            {
                get { return Entry != null && Entry.IsLoseCondition; }
            }

        }
    }
}
