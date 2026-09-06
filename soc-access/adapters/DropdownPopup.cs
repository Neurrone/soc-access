using System.Collections;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The game's own dropdown popup, worked from the outside: shown, hidden, asked whether it is up,
    /// and told which entry to highlight.
    ///
    /// Every page that draws a dropdown draws the same <c>UITextMeshDropdown</c> over a TMP dropdown,
    /// so this is written once and every adapter's drop list answers with it.
    /// </summary>
    public static class DropdownPopup
    {
        private static readonly FieldInfo TmpItemsField = AccessTools.Field(typeof(TMP_Dropdown), "m_Items");

        public static TMP_Dropdown GetTmpDropdown(IUITextMeshDropdown dropdown)
        {
            Component component = dropdown as Component;
            return component != null ? component.GetComponentInChildren<TMP_Dropdown>(true) : null;
        }

        public static bool Show(IUITextMeshDropdown dropdown)
        {
            if (dropdown == null || !dropdown.Active || !dropdown.Interactable)
            {
                return false;
            }

            dropdown.Show();
            return true;
        }

        public static bool Hide(IUITextMeshDropdown dropdown)
        {
            TMP_Dropdown tmpDropdown = GetTmpDropdown(dropdown);
            if (tmpDropdown == null)
            {
                return false;
            }

            tmpDropdown.Hide();
            return true;
        }

        public static bool IsOpen(IUITextMeshDropdown dropdown)
        {
            return dropdown != null && dropdown.Active && dropdown.IsExpanded;
        }

        /// <summary>Put the game's own highlight on one entry of the open popup, the way hovering it
        /// does. The entries are the toggles TMP builds when the list opens (<c>TMP_Dropdown.m_Items</c>,
        /// one per option in option order), and selecting one is also what the template's
        /// <c>AutoScrollToSelected</c> scrolls to.</summary>
        public static bool FocusOption(IUITextMeshDropdown dropdown, int index)
        {
            TMP_Dropdown tmpDropdown = GetTmpDropdown(dropdown);
            IList items = tmpDropdown != null && TmpItemsField != null
                ? TmpItemsField.GetValue(tmpDropdown) as IList
                : null;
            if (items == null || index < 0 || index >= items.Count)
            {
                return false;
            }

            // The entry's own type is protected, so it is read as the behaviour it is: the toggle that
            // makes the row clickable sits on the same object TMP builds for it.
            Component item = items[index] as Component;
            Toggle toggle = item != null ? item.GetComponent<Toggle>() : null;
            return toggle != null && NativeSelectionUtility.Select(toggle);
        }
    }
}
