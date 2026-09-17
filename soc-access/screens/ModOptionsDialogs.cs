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
    /// A dialog carries Cancel and Confirm only where leaving has to be able to mean "not that":
    /// a cue, whose sliders replay it as they move, and one source's subcategories. Cancel puts
    /// back a snapshot taken when the dialog opened - <c>ModSettings.SnapshotCue</c> for a cue, and
    /// the stored form of the whole taxonomy for the subcategories, which already says every
    /// category's name, key, subcategories and keywords in one string. Everywhere else, including
    /// the editor for one category, a change is written and saved as it is made and leaving simply
    /// leaves, as every toggle and slider in the Mod options window itself does.
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

        /// <summary>The three slots, always all three and always in order, each named by its number
        /// and by what it holds, so an empty one is a row the player can walk onto and fill rather
        /// than something they have to add first.</summary>
        private static void DrawCustomCategories(ModDialogScreen screen, ScannerTaxonomy taxonomy)
        {
            ModDialog dialog = screen.Dialog;
            for (int i = 0; i < ScannerCustomSlots.Count; i++)
            {
                int slot = i;
                ScannerCustomCategory category = ModSettings.GetScannerCustomCategory(taxonomy.Key, slot);
                dialog.AddButton(
                    ModText.Get(
                        ModStrings.Screens.CustomCategorySlot,
                        slot + 1,
                        category != null
                            ? category.Name
                            : ModText.Get(ModStrings.Screens.CustomCategorySlotEmpty)),
                    () => OpenCustomCategory(screen, taxonomy, slot));
            }
        }

        // ---- one slot ----

        /// <summary>
        /// The editor for one slot. An empty slot is filled as it is opened, under the name its
        /// number gives it, because a category is a name plus what it asks for and there is nothing
        /// to tick columns onto until the slot holds one. Every change here is written as it is
        /// made, so leaving keeps it; a slot still untouched when the editor is left is emptied
        /// again rather than left holding the name the opening gave it.
        /// </summary>
        private static void OpenCustomCategory(ModDialogScreen parent, ScannerTaxonomy taxonomy, int slot)
        {
            string defaultName = ModText.Get(ModStrings.Screens.CustomCategoryDefaultName, slot + 1);
            ScannerCustomCategory category = ModSettings.GetScannerCustomCategory(taxonomy.Key, slot)
                ?? ModSettings.AddScannerCustomCategory(taxonomy.Key, slot, defaultName);
            if (category == null)
            {
                return;
            }

            ModDialogScreen.Open(
                "mod-category-" + taxonomy.Key + "-" + slot,
                ModText.Get(ModStrings.Screens.CustomCategorySlot, slot + 1, category.Name),
                screen => DrawCustomCategory(screen, parent, taxonomy, slot),
                () =>
                {
                    ForgetUntouchedCategory(taxonomy, slot, defaultName);
                    parent.Redraw();
                    return true;
                });
        }

        /// <summary>Leaving a slot still exactly as the opening left it empties it again, so looking
        /// at an empty slot does not put a category with a name and nothing else into the cycle - a
        /// scope that would answer every key with nothing found.</summary>
        private static void ForgetUntouchedCategory(ScannerTaxonomy taxonomy, int slot, string defaultName)
        {
            ScannerCustomCategory category = ModSettings.GetScannerCustomCategory(taxonomy.Key, slot);
            if (category != null && category.IsUntouched(defaultName))
            {
                ModSettings.ClearScannerCustomCategory(taxonomy.Key, slot);
            }
        }

        private static void DrawCustomCategory(ModDialogScreen screen, ModDialogScreen parent, ScannerTaxonomy taxonomy, int slot)
        {
            ModDialog dialog = screen.Dialog;
            ScannerCustomCategory category = ModSettings.GetScannerCustomCategory(taxonomy.Key, slot);
            if (category == null)
            {
                return;
            }

            // The box's own change event fires on every keystroke, so the name is not written as it
            // is typed - it is committed when the EDIT ENDS, which is Enter, Escape, or the focus
            // leaving the box, and that is also where the refusal belongs.
            IUITextMeshInputField nameField = null;
            nameField = dialog.AddInputField(
                ModText.Get(ModStrings.Screens.CustomCategoryName),
                category.Name,
                null,
                text => CommitName(screen, parent, taxonomy, slot, nameField, text));

            IReadOnlyList<ScannerCategoryDefinition> definitions = taxonomy.Categories;
            for (int i = 0; i < definitions.Count; i++)
            {
                ScannerCategoryDefinition definition = definitions[i];
                dialog.AddButton(
                    DescribeSource(category, definition),
                    () => OpenCategorySelectors(screen, taxonomy, slot, definition));
            }

            for (int i = 0; i < category.Keywords.Count; i++)
            {
                string keyword = category.Keywords[i];
                dialog.AddButton(
                    ModText.Get(ModStrings.Screens.RemoveKeyword, keyword),
                    () =>
                    {
                        ModSettings.RemoveScannerCustomCategoryKeyword(taxonomy.Key, slot, keyword);
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
                    if (!ModSettings.AddScannerCustomCategoryKeyword(taxonomy.Key, slot, trimmed) && trimmed.Length > 0)
                    {
                        Speak(ModText.Get(ModStrings.Screens.KeywordAlreadyAdded));
                        return;
                    }

                    screen.Redraw();
                });

            // Emptying the slot is the delete: the slot itself stays, so the key that walks it keeps
            // answering and nothing is renumbered.
            dialog.AddButton(
                ModText.Get(ModStrings.Screens.ClearCustomCategory),
                () =>
                {
                    if (!ModSettings.ClearScannerCustomCategory(taxonomy.Key, slot))
                    {
                        return;
                    }

                    Speak(ModText.Get(ModStrings.Screens.CustomCategoryCleared, slot + 1));
                    parent.Redraw();
                    screen.Close();
                });
        }

        /// <summary>
        /// The name, written when the edit ends. A name left as it was is not a change and passes in
        /// silence; a refused one leaves the box holding the last accepted name, so the editor never
        /// shows something the category is not called. The refusal is stacked over the editor from
        /// inside this callback, and the editor itself is NOT redrawn here - that would destroy the
        /// very box the callback is running on. The parent is, so the slot list says the new name,
        /// and so is the title, which carries the name the editor opened with.
        /// </summary>
        private static void CommitName(
            ModDialogScreen screen,
            ModDialogScreen parent,
            ScannerTaxonomy taxonomy,
            int slot,
            IUITextMeshInputField field,
            string text)
        {
            ScannerCustomCategory category = ModSettings.GetScannerCustomCategory(taxonomy.Key, slot);
            if (category == null)
            {
                return;
            }

            string name = (text ?? string.Empty).Trim();
            if (name == category.Name)
            {
                return;
            }

            if (!Rename(taxonomy, slot, name))
            {
                if (field != null)
                {
                    field.InputFieldValue = category.Name;
                }

                return;
            }

            screen.SetTitle(ModText.Get(ModStrings.Screens.CustomCategorySlot, slot + 1, name));
            parent.Redraw();
        }

        private static string Value(IUITextMeshInputField field)
        {
            return field == null ? string.Empty : (field.InputFieldValue ?? string.Empty).Trim();
        }

        /// <summary>
        /// Two categories under one name are one name in speech, which is the only way the category
        /// cycle is ever read, so a name already spoken for is refused, and so is an empty box,
        /// which would leave a category the reader speaks as silence. Each refusal is a dialog of
        /// its own stacked over the editor, so it is read on arrival and has to be dismissed rather
        /// than passing by as one spoken line; the editor is left open underneath with what was
        /// typed still in the box, so a near miss is edited rather than typed out again.
        /// </summary>
        private static bool Rename(ScannerTaxonomy taxonomy, int slot, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                OpenNameRefused(
                    "mod-category-name-empty",
                    ModText.Get(ModStrings.Screens.CustomCategoryNameMissingTitle),
                    ModText.Get(ModStrings.Screens.CustomCategoryNameEmpty));
                return false;
            }

            if (ScannerCustomCategoryNameConflict.Exists(
                    name,
                    taxonomy,
                    ModSettings.GetScannerCustomSlots(taxonomy.Key),
                    slot))
            {
                OpenNameRefused(
                    "mod-category-name-taken",
                    ModText.Get(ModStrings.Screens.CustomCategoryNameTakenTitle),
                    ModText.Get(ModStrings.Screens.CustomCategoryNameTaken, name));
                return false;
            }

            ModSettings.RenameScannerCustomCategory(taxonomy.Key, slot, name);
            return true;
        }

        /// <summary>
        /// The refusal itself: a title, the message, and the one button that leaves. The message is
        /// a text row heading the button, so it is the button's region and is read both on arrival
        /// and on every return to the cursor - "Pickups is already the name of a category, OK,
        /// button". OK, the window's close button and Escape are all the same way out, and none of
        /// them changes anything.
        /// </summary>
        private static void OpenNameRefused(string key, string title, string message)
        {
            ModDialogScreen.Open(
                key,
                title,
                screen =>
                {
                    ModDialog dialog = screen.Dialog;
                    dialog.AddText(message);
                    dialog.AddButton(
                        GameText.Get("Common/Ok", ModText.Get(ModStrings.Screens.Ok)),
                        () => screen.Close());
                });
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
            int slot,
            ScannerCategoryDefinition definition)
        {
            string snapshot = ModSettings.SnapshotScannerCustomCategories(taxonomy.Key);
            ModDialogScreen.Open(
                "mod-selectors-" + taxonomy.Key + "-" + slot + "-" + definition.Key,
                definition.Label != null ? definition.Label() : definition.Key,
                screen => DrawCategorySelectors(screen, parent, taxonomy, slot, definition),
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
            int slot,
            ScannerCategoryDefinition definition)
        {
            ModDialog dialog = screen.Dialog;
            for (int i = 0; i < definition.Subcategories.Count; i++)
            {
                ScannerSubcategoryDefinition subcategory = definition.Subcategories[i];
                string subcategoryKey = subcategory.Key;
                ScannerCustomCategory category = ModSettings.GetScannerCustomCategory(taxonomy.Key, slot);
                dialog.AddToggle(
                    subcategory.Label != null ? subcategory.Label() : subcategoryKey,
                    category != null && category.HasSelector(definition.Key, subcategoryKey),
                    value => ModSettings.SetScannerCustomCategorySelector(
                        taxonomy.Key,
                        slot,
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
            dialog.AddButton(GameText.Get("Common/Cancel", ModText.Get(ModStrings.Actions.Cancel)), () => screen.Cancel());
            dialog.AddButton(
                GameText.Get("Common/Confirm", ModText.Get(ModStrings.Screens.Confirm)),
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
