using System;
using System.Collections.Generic;
using System.Reflection;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The read the two kingdom overview menus share. Both are a page the game fills once in Show
    /// and leaves alone until Hide: a title outside the entries, a list of drawn entries, and a list
    /// of drawn rows under each entry, every text of which is a <see cref="UITextMesh"/> behind a
    /// private field. Each adapter keeps its own field table and its own item types; only the
    /// reading is here.
    /// </summary>
    public static class KingdomOverviewRead
    {
        /// <summary>The menu's own drawn title: the first non-empty text that is not part of an
        /// entry or a row. Empty when the menu draws none.</summary>
        public static string FindTitle<TEntry, TRow>(Component menu)
            where TEntry : Component
            where TRow : Component
        {
            if (menu == null)
            {
                return string.Empty;
            }

            UITextMesh[] texts = menu.GetComponentsInChildren<UITextMesh>(includeInactive: false);
            for (int i = 0; i < texts.Length; i++)
            {
                UITextMesh text = texts[i];
                if (text == null
                    || text.GetComponentInParent<TEntry>() != null
                    || text.GetComponentInParent<TRow>() != null)
                {
                    continue;
                }

                string candidate = NormalizeText(text);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        /// <summary>Every drawn <typeparamref name="TEntry"/> under <paramref name="root"/>, in the
        /// order the menu draws them, projected by <paramref name="build"/>. A projection that
        /// returns null is dropped.</summary>
        public static List<TItem> FindEntries<TEntry, TItem>(Component root, Func<TEntry, TItem> build)
            where TEntry : Component
            where TItem : class
        {
            List<TItem> items = new List<TItem>();
            if (root == null || build == null)
            {
                return items;
            }

            TEntry[] entries = root.GetComponentsInChildren<TEntry>(includeInactive: false);
            for (int i = 0; i < entries.Length; i++)
            {
                TEntry entry = entries[i];
                if (entry == null || !entry.gameObject.activeInHierarchy)
                {
                    continue;
                }

                TItem item = build(entry);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        /// <summary>The text the game drew into one of an entry's private UITextMesh fields.</summary>
        public static string ReadText(object target, FieldInfo field)
        {
            return NormalizeText(GetText(target, field));
        }

        public static UITextMesh GetText(object target, FieldInfo field)
        {
            return Reflect.Get<UITextMesh>(target, field);
        }

        public static string NormalizeText(UITextMesh text)
        {
            return text != null
                ? SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text))
                : string.Empty;
        }

        public static bool FocusButton(UIButton button)
        {
            Selectable selectable = button != null ? button.GetSelectable() : null;
            return NativeSelectionUtility.Select(selectable);
        }
    }
}
