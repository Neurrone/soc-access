using System;
using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// WHAT EACH OF THE MOD OPTIONS DIALOGS DRAWS.
    ///
    /// The Mod options window's buttons open dialogs stacked over it, each a
    /// <see cref="ModDialogScreen"/> and each nothing but rows. This is the content of every one:
    /// the announcement order of a group, the audio glossary, one cue's tuning, a taxonomy's custom
    /// categories, one category, and one source's subcategories.
    ///
    /// Every rule the mod-owned menus these replace enforced is enforced here: a name already spoken
    /// for is refused and said so, a keyword already present is refused and said so, a quick key
    /// says who holds it before it is taken, moving an element redraws the list and leaves the
    /// cursor on the element that moved, and the defaults are a button rather than a special case.
    ///
    /// The dialogs that EDIT one thing - a cue, a category, a source's subcategories - carry Cancel
    /// and Confirm, because a change is applied while the dialog is open (a slider replays its cue
    /// as it moves) and leaving has to be able to mean "not that". Cancel puts back a snapshot taken
    /// when the dialog opened: <c>ModSettings.SnapshotCue</c> for a cue, and the stored form of the
    /// whole taxonomy for a category, which already says its name, its key, its subcategories and
    /// its keywords in one string. The dialogs that only LIST things carry neither.
    /// </summary>
    public static class ModOptionsDialogs
    {
        /// <summary>Volume moves five percent at a time, duration ten, pitch a semitone - the steps
        /// the menus these replace used.</summary>
        private const float VolumeStep = 0.05f;
        private const float DurationStep = 0.10f;

        // ---- the order of one announcement group ----

        /// <summary>
        /// One region per element, in the order the announcement is spoken, holding that element's
        /// two settings and the two buttons that move it. The element's name is a caption, so it
        /// names the region and is read on the way in rather than repeated on all four controls.
        /// </summary>
        public static void OpenAnnouncementOrder(AnnouncementGroupDefinition group)
        {
            if (group == null)
            {
                return;
            }

            ModDialogScreen.Open(
                "mod-order-" + group.Key,
                ModText.Get(group.Label),
                screen => DrawAnnouncementOrder(screen, group));
        }

        private static void DrawAnnouncementOrder(ModDialogScreen screen, AnnouncementGroupDefinition group)
        {
            ModDialog dialog = screen.Dialog;
            IReadOnlyList<string> order = ModSettings.GetAnnouncementOrder(group);
            for (int i = 0; i < order.Count; i++)
            {
                AnnouncementElementDefinition element = group.GetElement(order[i]);
                if (element == null)
                {
                    continue;
                }

                string key = element.Key;
                dialog.AddText(ModText.Get(element.Label));
                // A toggle is drawn as a full-width row with its box at the right, so two of them
                // and two buttons side by side do not fit: measured 2026-09-07, four in one
                // horizontal layout gave each toggle 381 px of a 486 px column and squeezed both
                // buttons to nothing. The game only ever puts TWO buttons in one, so that is what
                // this puts in one.
                dialog.AddToggle(
                    ModText.Get(ModStrings.Screens.Enabled),
                    ModSettings.GetAnnouncementElementEnabled(group, element),
                    value => ModSettings.SetAnnouncementElementEnabled(group, element, value));
                dialog.AddToggle(
                    ModText.Get(ModStrings.Screens.Suffix),
                    ModSettings.GetAnnouncementElementSuffix(group, element),
                    value => ModSettings.SetAnnouncementElementSuffix(group, element, value));
                dialog.StartRow();
                dialog.AddButton(ModText.Get(ModStrings.Screens.MoveUp), () => Move(screen, group, key, -1));
                dialog.AddButton(ModText.Get(ModStrings.Screens.MoveDown), () => Move(screen, group, key, 1));
                dialog.EndRow();
            }

            dialog.AddButton(
                ModText.Get(ModStrings.Screens.ResetAllToDefaults),
                () =>
                {
                    ModSettings.ResetAnnouncementGroup(group);
                    screen.Redraw();
                });
        }

        /// <summary>
        /// Move an element and follow it with the cursor.
        ///
        /// A move redraws every row, which throws away the control the player was standing on, so
        /// the cursor would otherwise stay at the POSITION and read the element that took the old
        /// place. The two move buttons of element <c>n</c> are the factory's buttons <c>2n</c> and
        /// <c>2n+1</c>, so asking for the moved element's own button by that name puts the cursor
        /// back where the player left it.
        /// </summary>
        private static void Move(ModDialogScreen screen, AnnouncementGroupDefinition group, string key, int delta)
        {
            if (!ModSettings.MoveAnnouncementElement(group, key, delta))
            {
                return;
            }

            screen.Redraw();
            int index = IndexOf(ModSettings.GetAnnouncementOrder(group), key);
            if (index >= 0)
            {
                screen.FocusRow("options-button-" + (index * 2 + (delta < 0 ? 0 : 1)));
            }
        }

        private static int IndexOf(IReadOnlyList<string> order, string key)
        {
            for (int i = 0; order != null && i < order.Count; i++)
            {
                if (order[i] == key)
                {
                    return i;
                }
            }

            return -1;
        }

        // ---- the audio glossary ----

        public static void OpenAudioGlossary()
        {
            ModDialogScreen.Open(
                "mod-glossary",
                ModText.Get(ModStrings.Screens.AudioGlossary),
                DrawAudioGlossary);
        }

        private static void DrawAudioGlossary(ModDialogScreen screen)
        {
            ModDialog dialog = screen.Dialog;
            IReadOnlyList<CueDefinition> cues = CueLibrary.AllCues;
            for (int i = 0; i < cues.Count; i++)
            {
                CueDefinition cue = cues[i];
                dialog.AddText(ModText.Get(cue.Name));
                dialog.StartRow();
                dialog.AddButton(ModText.Get(ModStrings.Screens.Play), () => CueLibrary.PlayCue(cue.Key));
                dialog.AddButton(ModText.Get(ModStrings.Screens.Configure), () => OpenCue(cue));
                dialog.EndRow();
            }
        }

        // ---- one cue ----

        public static void OpenCue(CueDefinition cue)
        {
            if (cue == null)
            {
                return;
            }

            string key = cue.Key;
            CueTuning snapshot = ModSettings.SnapshotCue(key);
            ModDialogScreen.Open(
                "mod-cue-" + key,
                ModText.Get(ModStrings.Screens.ConfigureAnnouncementElement, ModText.Get(cue.Name)),
                screen => DrawCue(screen, key),
                () =>
                {
                    ModSettings.RestoreCue(key, snapshot);
                    return true;
                });
        }

        private static void DrawCue(ModDialogScreen screen, string key)
        {
            ModDialog dialog = screen.Dialog;
            dialog.AddToggle(
                ModText.Get(ModStrings.Screens.Enabled),
                ModSettings.GetCueEnabled(key),
                value =>
                {
                    ModSettings.SetCueEnabled(key, value);
                    CueLibrary.PlayCue(key);
                });

            // Percentages are drawn as percentages by the game's own slider, which speaks
            // value * 100 with a per cent sign, so the value is handed over as a fraction and the
            // stored whole number is what comes back.
            Percent(
                dialog.AddSlider(
                    ModText.Get(ModStrings.Screens.Volume),
                    ModSettings.GetCueVolume(key) / 100f,
                    ModSettings.CueVolumeMinimum / 100f,
                    ModSettings.CueVolumeMaximum / 100f,
                    value =>
                    {
                        ModSettings.SetCueVolume(key, Whole(value));
                        CueLibrary.PlayCue(key);
                    }),
                VolumeStep);

            IUISlider pitch = dialog.AddSlider(
                ModText.Get(ModStrings.Screens.Pitch),
                ModSettings.GetCuePitchSemitones(key),
                ModSettings.CuePitchSemitonesMinimum,
                ModSettings.CuePitchSemitonesMaximum,
                value =>
                {
                    ModSettings.SetCuePitchSemitones(key, (int)Math.Round(value));
                    CueLibrary.PlayCue(key);
                });
            if (pitch != null)
            {
                // Semitones, not a percentage: the factory's slider prefab draws as a percentage by
                // default, which turned "0" into "0%".
                pitch.UseWholeNumbers = true;
                pitch.DrawAsPercent = false;
            }

            Percent(
                dialog.AddSlider(
                    ModText.Get(ModStrings.Screens.Duration),
                    ModSettings.GetCueDurationScale(key) / 100f,
                    ModSettings.CueDurationScaleMinimum / 100f,
                    ModSettings.CueDurationScaleMaximum / 100f,
                    value =>
                    {
                        ModSettings.SetCueDurationScale(key, Whole(value));
                        CueLibrary.PlayCue(key);
                    }),
                DurationStep);

            dialog.AddButton(ModText.Get(ModStrings.Screens.Play), () => CueLibrary.PlayCue(key));
            dialog.AddButton(
                ModText.Get(ModStrings.Screens.ResetToDefaults),
                () =>
                {
                    ModSettings.ResetCue(key);
                    screen.Redraw();
                    CueLibrary.PlayCue(key);
                });
            AddCancelAndConfirm(screen);
        }

        private static void Percent(IUISlider slider, float step)
        {
            if (slider == null)
            {
                return;
            }

            slider.DrawAsPercent = true;
            slider.NearestDecimal = step;
        }

        private static int Whole(float fraction)
        {
            return (int)Math.Round(fraction * 100f);
        }

        // ---- the custom categories of one taxonomy ----

        public static void OpenCustomCategories(ScannerTaxonomy taxonomy, ModString contextLabel)
        {
            if (taxonomy == null)
            {
                return;
            }

            ModDialogScreen.Open(
                "mod-categories-" + taxonomy.Key,
                ModText.Get(ModStrings.Screens.CustomCategories, ModText.Get(contextLabel)),
                screen => DrawCustomCategories(screen, taxonomy));
        }

        private static void DrawCustomCategories(ModDialogScreen screen, ScannerTaxonomy taxonomy)
        {
            ModDialog dialog = screen.Dialog;
            IReadOnlyList<ScannerCustomCategory> categories = ModSettings.GetScannerCustomCategories(taxonomy.Key);
            for (int i = 0; i < categories.Count; i++)
            {
                int id = categories[i].Id;
                dialog.AddButton(categories[i].Name, () => OpenCustomCategory(screen, taxonomy, id));
            }

            dialog.AddButton(
                ModText.Get(ModStrings.Screens.AddCustomCategory),
                () =>
                {
                    ScannerCustomCategory added = ModSettings.AddScannerCustomCategory(
                        taxonomy.Key,
                        position => ModText.Get(ModStrings.Screens.CustomCategoryDefaultName, position));
                    if (added == null)
                    {
                        return;
                    }

                    screen.Redraw();
                    OpenCustomCategory(screen, taxonomy, added.Id);
                });
        }

        // ---- one custom category ----

        private static void OpenCustomCategory(ModDialogScreen parent, ScannerTaxonomy taxonomy, int id)
        {
            string snapshot = ModSettings.SnapshotScannerCustomCategories(taxonomy.Key);
            ScannerCustomCategory category = ModSettings.GetScannerCustomCategory(taxonomy.Key, id);
            if (category == null)
            {
                return;
            }

            ModDialogScreen.Open(
                "mod-category-" + taxonomy.Key + "-" + id,
                category.Name,
                screen => DrawCustomCategory(screen, parent, taxonomy, id),
                () =>
                {
                    ModSettings.RestoreScannerCustomCategories(taxonomy.Key, snapshot);
                    parent.Redraw();
                    return true;
                });
        }

        private static void DrawCustomCategory(ModDialogScreen screen, ModDialogScreen parent, ScannerTaxonomy taxonomy, int id)
        {
            ModDialog dialog = screen.Dialog;
            ScannerCustomCategory category = ModSettings.GetScannerCustomCategory(taxonomy.Key, id);
            if (category == null)
            {
                return;
            }

            // The box's own change event fires on every keystroke, so the name is not written as it
            // is typed - it is read off the box when the player confirms, which is also where the
            // refusal belongs.
            IUITextMeshInputField nameField = dialog.AddInputField(
                ModText.Get(ModStrings.Screens.CustomCategoryName),
                category.Name,
                null);

            if (ModSettings.SupportsScannerQuickKeys(taxonomy.Key))
            {
                AddQuickKeyDropdown(dialog, taxonomy, id, category);
            }

            IReadOnlyList<ScannerCategoryDefinition> definitions = taxonomy.Categories;
            for (int i = 0; i < definitions.Count; i++)
            {
                ScannerCategoryDefinition definition = definitions[i];
                dialog.AddButton(
                    DescribeSource(category, definition),
                    () => OpenCategorySelectors(screen, taxonomy, id, definition));
            }

            for (int i = 0; i < category.Keywords.Count; i++)
            {
                string keyword = category.Keywords[i];
                dialog.AddButton(
                    ModText.Get(ModStrings.Screens.RemoveKeyword, keyword),
                    () =>
                    {
                        ModSettings.RemoveScannerCustomCategoryKeyword(taxonomy.Key, id, keyword);
                        screen.Redraw();
                    });
            }

            IUITextMeshInputField keywordField = dialog.AddInputField(
                ModText.Get(ModStrings.Screens.AddKeyword),
                string.Empty,
                null);
            dialog.AddButton(
                ModText.Get(ModStrings.Screens.Add),
                () =>
                {
                    string trimmed = Value(keywordField);
                    // A refused keyword that was not blank was already there, and swallowing that
                    // silently reads as a dead keypress.
                    if (!ModSettings.AddScannerCustomCategoryKeyword(taxonomy.Key, id, trimmed) && trimmed.Length > 0)
                    {
                        Speak(ModText.Get(ModStrings.Screens.KeywordAlreadyAdded));
                        return;
                    }

                    screen.Redraw();
                });

            dialog.AddButton(
                ModText.Get(ModStrings.Screens.DeleteCustomCategory),
                () =>
                {
                    string name = category.Name;
                    if (!ModSettings.RemoveScannerCustomCategory(taxonomy.Key, id))
                    {
                        return;
                    }

                    Speak(ModText.Get(ModStrings.Screens.CustomCategoryDeleted, name));
                    parent.Redraw();
                    screen.Close();
                });
            AddCancelAndConfirm(screen, () =>
            {
                if (!Rename(taxonomy, id, Value(nameField)))
                {
                    return false;
                }

                parent.Redraw();
                return true;
            });
        }

        private static string Value(IUITextMeshInputField field)
        {
            return field == null ? string.Empty : (field.InputFieldValue ?? string.Empty).Trim();
        }

        /// <summary>
        /// Two categories under one name are one name in speech, which is the only way the category
        /// cycle is ever read, so a name already spoken for is refused and said so. The box keeps
        /// what was typed, so a near miss is edited rather than typed out again.
        /// </summary>
        private static bool Rename(ScannerTaxonomy taxonomy, int id, string name)
        {
            if (ScannerCustomCategoryNameConflict.Exists(
                    name,
                    taxonomy,
                    ModSettings.GetScannerCustomCategories(taxonomy.Key),
                    id))
            {
                Speak(ModText.Get(ModStrings.Screens.CustomCategoryNameTaken, name));
                return false;
            }

            ModSettings.RenameScannerCustomCategory(taxonomy.Key, id, name);
            return true;
        }

        /// <summary>
        /// The one key that walks this category on the adventure map. Every option says who holds
        /// it, because picking one that is taken MOVES it, and the player deserves to know what
        /// they are about to take it from.
        /// </summary>
        private static void AddQuickKeyDropdown(ModDialog dialog, ScannerTaxonomy taxonomy, int id, ScannerCustomCategory category)
        {
            List<UITextMeshDropdown.Option> options = new List<UITextMeshDropdown.Option>();
            List<ScannerQuickKey> keys = new List<ScannerQuickKey>();
            int value = 0;
            for (int i = 0; i <= ScannerQuickKeys.Assignable.Length; i++)
            {
                ScannerQuickKey quickKey = i < ScannerQuickKeys.Assignable.Length
                    ? ScannerQuickKeys.Assignable[i]
                    : ScannerQuickKey.None;
                if (category.QuickKey == quickKey)
                {
                    value = keys.Count;
                }

                keys.Add(quickKey);
                options.Add(new UITextMeshDropdown.Option(DescribeQuickKey(taxonomy, category, quickKey)));
            }

            dialog.AddDropdown(
                ModText.Get(ModStrings.Screens.CustomCategoryKeyTitle, category.Name),
                options,
                value,
                index =>
                {
                    if (index >= 0 && index < keys.Count)
                    {
                        ModSettings.SetScannerCustomCategoryQuickKey(taxonomy.Key, id, keys[index]);
                    }
                });
        }

        private static string DescribeQuickKey(ScannerTaxonomy taxonomy, ScannerCustomCategory category, ScannerQuickKey quickKey)
        {
            string name = ScannerQuickKeyText.Name(quickKey);
            if (category.QuickKey == quickKey)
            {
                return ModText.Get(ModStrings.Screens.CustomCategoryKeyCurrent, name);
            }

            ScannerCustomCategory holder = ModSettings.GetScannerCustomCategoryByQuickKey(taxonomy.Key, quickKey);
            return holder != null
                ? ModText.Get(ModStrings.Screens.CustomCategoryKeyHeldBy, name, holder.Name)
                : name;
        }

        /// <summary>Says how much of a source category this custom category takes, so the player can
        /// see what is picked without opening every one.</summary>
        private static string DescribeSource(ScannerCustomCategory category, ScannerCategoryDefinition definition)
        {
            string label = definition.Label != null ? definition.Label() : definition.Key;
            int count = 0;
            for (int i = 0; i < definition.Subcategories.Count; i++)
            {
                if (category.HasSelector(definition.Key, definition.Subcategories[i].Key))
                {
                    count++;
                }
            }

            return ModText.Get(
                ModStrings.Common.ListSeparator,
                label,
                ModText.Plural(ModStrings.Screens.SelectedSubcategoryCount, count, count));
        }

        // ---- the subcategories of one source ----

        private static void OpenCategorySelectors(
            ModDialogScreen parent,
            ScannerTaxonomy taxonomy,
            int id,
            ScannerCategoryDefinition definition)
        {
            string snapshot = ModSettings.SnapshotScannerCustomCategories(taxonomy.Key);
            ModDialogScreen.Open(
                "mod-selectors-" + taxonomy.Key + "-" + id + "-" + definition.Key,
                definition.Label != null ? definition.Label() : definition.Key,
                screen => DrawCategorySelectors(screen, parent, taxonomy, id, definition),
                () =>
                {
                    ModSettings.RestoreScannerCustomCategories(taxonomy.Key, snapshot);
                    parent.Redraw();
                    return true;
                });
        }

        private static void DrawCategorySelectors(
            ModDialogScreen screen,
            ModDialogScreen parent,
            ScannerTaxonomy taxonomy,
            int id,
            ScannerCategoryDefinition definition)
        {
            ModDialog dialog = screen.Dialog;
            for (int i = 0; i < definition.Subcategories.Count; i++)
            {
                ScannerSubcategoryDefinition subcategory = definition.Subcategories[i];
                string subcategoryKey = subcategory.Key;
                ScannerCustomCategory category = ModSettings.GetScannerCustomCategory(taxonomy.Key, id);
                dialog.AddToggle(
                    subcategory.Label != null ? subcategory.Label() : subcategoryKey,
                    category != null && category.HasSelector(definition.Key, subcategoryKey),
                    value => ModSettings.SetScannerCustomCategorySelector(
                        taxonomy.Key,
                        id,
                        definition.Key,
                        subcategoryKey,
                        value));
            }

            AddCancelAndConfirm(screen, () =>
            {
                parent.Redraw();
                return true;
            });
        }

        // ---- shared ----

        /// <summary>The two ways out of a dialog that edits something, drawn side by side along the
        /// bottom as the game's own popups draw them.</summary>
        private static void AddCancelAndConfirm(ModDialogScreen screen, Func<bool> confirmed = null)
        {
            ModDialog dialog = screen.Dialog;
            dialog.StartRow();
            dialog.AddButton(GameText.Get("Common/Cancel", "Cancel"), () => screen.Cancel());
            dialog.AddButton(
                GameText.Get("Common/Confirm", "Confirm"),
                () =>
                {
                    // A confirm the dialog refuses - a name already spoken for - leaves it open, so
                    // the player edits the near miss rather than typing it out again.
                    if (confirmed == null || confirmed())
                    {
                        screen.Close();
                    }
                });
            dialog.EndRow();
        }

        private static void Speak(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
        }
    }
}
