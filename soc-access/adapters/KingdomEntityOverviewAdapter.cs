using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Entities;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The kingdom's building overview, read off the game's own entries. One
    /// <see cref="KingdomEntityOverviewCategoryEntry"/> per settlement (plus the catch-all the game
    /// spawns with no parent), each drawing its name, its tier, a band of six resource incomes, and
    /// one <see cref="KingdomEntityOverviewClaimedEntry"/> per building.
    /// </summary>
    public sealed class KingdomEntityOverviewAdapter
    {
        private static readonly FieldInfo CategoryTextField =
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_categoryText");
        private static readonly FieldInfo UpgradeTextField =
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_upgradeText");
        private static readonly FieldInfo ParentField =
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_parent");
        private static readonly FieldInfo AmountTextField =
            AccessTools.Field(typeof(KingdomEntityOverviewClaimedEntry), "_amount");
        private static readonly FieldInfo NameTextField =
            AccessTools.Field(typeof(KingdomEntityOverviewClaimedEntry), "_text");
        private static readonly FieldInfo LevelTextField =
            AccessTools.Field(typeof(KingdomEntityOverviewClaimedEntry), "_level");
        private static readonly MethodInfo CategoryClickMethod =
            AccessTools.Method(typeof(KingdomEntityOverviewCategoryEntry), "HandleCategoryTextClicked");

        /// <summary>The six income figures a category draws, in the order
        /// <c>KingdomEntityOverviewCategoryEntry.SetIncomeTexts</c> fills them.</summary>
        private static readonly ResourceType[] IncomeResources =
        {
            ResourceType.Gold,
            ResourceType.Stone,
            ResourceType.Wood,
            ResourceType.Glimmerweave,
            ResourceType.AncientAmber,
            ResourceType.CelestialOre,
        };

        private static readonly FieldInfo[] IncomeTextFields =
        {
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_goldIncomeText"),
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_stoneIncomeText"),
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_woodIncomeText"),
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_glimmerWeaveIncomeText"),
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_ancientAmberIncomeText"),
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_celestialOreIncomeText"),
        };

        private readonly KingdomEntityOverviewMenu _menu;

        // What the menu drew, read once. The game fills the whole page inside
        // KingdomEntityOverviewMenu.Show and never touches it again until Hide, which ends this
        // adapter, so reading the entries every frame re-read a page that cannot change.
        private List<CategoryItem> _categories;
        private string _title;

        public KingdomEntityOverviewAdapter(KingdomEntityOverviewMenu menu)
        {
            _menu = menu;
        }

        public bool IsPresent()
        {
            return _menu != null && _menu.IsVisible;
        }

        /// <summary>The menu's own drawn title. Empty when the menu draws none.</summary>
        public string Title
        {
            get
            {
                if (_title == null)
                {
                    _title = ReadTitle();
                }

                return _title;
            }
        }

        private string ReadTitle()
        {
            if (_menu == null)
            {
                return string.Empty;
            }

            UITextMesh[] texts = ((Component)_menu).GetComponentsInChildren<UITextMesh>(includeInactive: false);
            for (int i = 0; i < texts.Length; i++)
            {
                UITextMesh text = texts[i];
                if (text == null || IsOverviewEntryText(text))
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

        /// <summary>The categories in hierarchy order, which is the order the menu draws them. Read
        /// off the page once: the game builds it in Show and leaves it alone until Hide.</summary>
        public IReadOnlyList<CategoryItem> GetCategories()
        {
            if (_categories == null && IsPresent())
            {
                _categories = ReadCategories();
            }

            return _categories ?? new List<CategoryItem>();
        }

        private List<CategoryItem> ReadCategories()
        {
            List<CategoryItem> categories = new List<CategoryItem>();
            KingdomEntityOverviewCategoryEntry[] entries =
                ((Component)_menu).GetComponentsInChildren<KingdomEntityOverviewCategoryEntry>(includeInactive: false);
            for (int i = 0; i < entries.Length; i++)
            {
                KingdomEntityOverviewCategoryEntry entry = entries[i];
                if (entry == null || !entry.gameObject.activeInHierarchy)
                {
                    continue;
                }

                categories.Add(BuildCategory(entry));
            }

            return categories;
        }

        /// <summary>The game's own hide path, which is what clicking the blocker behind the menu
        /// runs.</summary>
        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Hide();
            return true;
        }

        private static CategoryItem BuildCategory(KingdomEntityOverviewCategoryEntry entry)
        {
            List<IncomeItem> incomes = new List<IncomeItem>(IncomeResources.Length);
            for (int i = 0; i < IncomeResources.Length; i++)
            {
                UITextMesh text = GetText(entry, IncomeTextFields[i]);
                incomes.Add(new IncomeItem(
                    GameText.Get("Common/Resource/" + IncomeResources[i], string.Empty),
                    NormalizeText(text),
                    text != null && text.gameObject.activeInHierarchy));
            }

            List<RowItem> rows = new List<RowItem>();
            KingdomEntityOverviewClaimedEntry[] buildings =
                entry.GetComponentsInChildren<KingdomEntityOverviewClaimedEntry>(includeInactive: false);
            for (int i = 0; i < buildings.Length; i++)
            {
                KingdomEntityOverviewClaimedEntry building = buildings[i];
                if (building == null || !building.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string name = NormalizeText(GetText(building, NameTextField));
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                rows.Add(new RowItem(
                    building,
                    building.Button,
                    NormalizeText(GetText(building, AmountTextField)),
                    name,
                    WrittenText(GetText(building, LevelTextField)),
                    () => ClickBuilding(building),
                    () => FocusButton(building.Button)));
            }

            return new CategoryItem(
                entry,
                NormalizeText(GetText(entry, CategoryTextField)),
                WrittenText(GetText(entry, UpgradeTextField)),
                GetParent(entry) != null,
                () => ClickCategory(entry),
                incomes,
                rows);
        }

        // The category's name is a UITextMesh whose click is delivered by UITransform.Update from the
        // real mouse position (decompiled), so there is no native call to make: the handler is invoked
        // directly.
        private static bool ClickCategory(KingdomEntityOverviewCategoryEntry entry)
        {
            if (entry == null || CategoryClickMethod == null || GetParent(entry) == null)
            {
                return false;
            }

            try
            {
                CategoryClickMethod.Invoke(entry, new object[] { Vector2.zero });
                return true;
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning("KingdomEntityOverviewAdapter failed to click category: " + ex.Message);
                return false;
            }
        }

        // The row IS a UIButton, and the entry wires HandleButtonClicked onto its OnClicked in
        // OnEnable, so the native click reaches the same handler the mouse does.
        private static bool ClickBuilding(KingdomEntityOverviewClaimedEntry entry)
        {
            return entry != null && NativeSelectionUtility.Click(entry.Button);
        }

        private static bool FocusButton(UIButton button)
        {
            Selectable selectable = button != null ? button.GetSelectable() : null;
            return NativeSelectionUtility.Select(selectable);
        }

        private static IMapEntity GetParent(KingdomEntityOverviewCategoryEntry entry)
        {
            return GetField<IMapEntity>(entry, ParentField);
        }

        private static UITextMesh GetText(object target, FieldInfo field)
        {
            return GetField<UITextMesh>(target, field);
        }

        private static T GetField<T>(object target, FieldInfo field) where T : class
        {
            if (target == null || field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(target) as T;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A tier text as the game wrote it: the claimed catch-all's rows and header get an
        /// empty string written, and the mesh then still carries the prefab's placeholder ("9999",
        /// "1/4"), which is not drawn and must not be read.</summary>
        private static string WrittenText(UITextMesh text)
        {
            return text != null
                ? SpokenLines.Clean(UITextMeshTextUtility.GetStringBuilderText(text) ?? string.Empty)
                : string.Empty;
        }

        private static string NormalizeText(UITextMesh text)
        {
            return text != null
                ? SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text))
                : string.Empty;
        }

        private static bool IsOverviewEntryText(UITextMesh text)
        {
            if (text == null)
            {
                return true;
            }

            return text.GetComponentInParent<KingdomEntityOverviewCategoryEntry>() != null
                || text.GetComponentInParent<KingdomEntityOverviewClaimedEntry>() != null;
        }

        /// <summary>One settlement's entry, or the catch-all the game spawns for everything with no
        /// parent (which draws a name and incomes but no tier and has no camera target).</summary>
        public sealed class CategoryItem
        {
            public CategoryItem(
                Component entry,
                string name,
                string tier,
                bool hasCameraTarget,
                Func<bool> moveCamera,
                IReadOnlyList<IncomeItem> incomes,
                IReadOnlyList<RowItem> rows)
            {
                Entry = entry;
                Name = name ?? string.Empty;
                Tier = tier ?? string.Empty;
                HasCameraTarget = hasCameraTarget;
                MoveCamera = moveCamera;
                Incomes = incomes ?? new IncomeItem[0];
                Rows = rows ?? new RowItem[0];
            }

            /// <summary>The drawn entry: what the row stands on and what it is scrolled by.</summary>
            public Component Entry { get; private set; }

            /// <summary>The full drawn category text ("Crowpoint - Small Settlement").</summary>
            public string Name { get; private set; }

            /// <summary>The drawn upgrade text ("Tier: 2/2"); empty for the catch-all.</summary>
            public string Tier { get; private set; }

            /// <summary>Whether clicking the name has somewhere to move the camera to.</summary>
            public bool HasCameraTarget { get; private set; }

            public Func<bool> MoveCamera { get; private set; }

            public IReadOnlyList<IncomeItem> Incomes { get; private set; }

            public IReadOnlyList<RowItem> Rows { get; private set; }
        }

        /// <summary>One figure of a category's income band: the resource it counts and the text the
        /// game drew for it ("+300", or "+0" greyed out).</summary>
        public sealed class IncomeItem
        {
            public IncomeItem(string resourceName, string text, bool isDrawn)
            {
                ResourceName = resourceName ?? string.Empty;
                Text = text ?? string.Empty;
                IsDrawn = isDrawn;
            }

            public string ResourceName { get; private set; }

            public string Text { get; private set; }

            public bool IsDrawn { get; private set; }
        }

        /// <summary>One building line of a category.</summary>
        public sealed class RowItem
        {
            public RowItem(
                Component entry,
                Component button,
                string amount,
                string name,
                string tier,
                Func<bool> activate,
                Func<bool> focus)
            {
                Entry = entry;
                Button = button;
                Amount = amount ?? string.Empty;
                Name = name ?? string.Empty;
                Tier = tier ?? string.Empty;
                Activate = activate;
                Focus = focus;
            }

            /// <summary>The drawn entry: what the row stands on and what it is scrolled by.</summary>
            public Component Entry { get; private set; }

            /// <summary>The drawn button the row's click runs through.</summary>
            public Component Button { get; private set; }

            /// <summary>How many of this building the team owns ("1", "2").</summary>
            public string Amount { get; private set; }

            /// <summary>The building's localized name.</summary>
            public string Name { get; private set; }

            /// <summary>The drawn level text ("Tier: 1/2"); empty where the game draws none.</summary>
            public string Tier { get; private set; }

            /// <summary>Cycles the camera through this building's instances, as the mouse does.</summary>
            public Func<bool> Activate { get; private set; }

            public Func<bool> Focus { get; private set; }
        }
    }
}
