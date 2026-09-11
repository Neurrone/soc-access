using System;
using System.Collections.Generic;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    // WHAT ONE ROW OF THE BUILD MENU IS, moved out of BuildMenuAdapter.cs unchanged: the read-only
    // facts a category, a building, a tier, a description section or a requirement hands the
    // screen, which words them.

    public sealed partial class BuildMenuAdapter
    {
        public sealed class CategoryItem
        {
            public CategoryItem(
                string label,
                string buildTime,
                int index,
                BuildSiteSize size,
                bool enabled,
                bool isSelected,
                Component button)
            {
                BuildTime = buildTime ?? string.Empty;
                IsSelected = isSelected;
                Button = button;
                Label = label;
                Index = index;
                Size = size;
                Enabled = enabled;
            }

            public string Label { get; private set; }

            /// <summary>How long anything of this size takes to build, in the game's own counted
            /// words ("2 rounds").</summary>
            public string BuildTime { get; private set; }

            public int Index { get; private set; }
            public BuildSiteSize Size { get; private set; }
            public bool Enabled { get; private set; }

            /// <summary>The size the menu is showing.</summary>
            public bool IsSelected { get; private set; }

            /// <summary>The tab's own button - what the tab is drawn by.</summary>
            public Component Button { get; private set; }
        }

        public sealed class BuildingItem
        {
            private readonly Func<bool> _isAvailable;

            public BuildingItem(
                string label,
                int number,
                Func<bool> isAvailable,
                Func<bool> isSelected,
                Component button,
                Func<bool> focus,
                Func<Tooltip> tooltip)
            {
                _isSelected = isSelected;
                Button = button;
                Label = label;
                Number = number;
                _isAvailable = isAvailable;
                Focus = focus;
                Tooltip = tooltip;
            }

            private readonly Func<bool> _isSelected;

            /// <summary>The building's own name, and empty where the game has no blueprint to
            /// name it by.</summary>
            public string Label { get; private set; }

            /// <summary>Which button of the grid this is, counting from one.</summary>
            public int Number { get; private set; }

            public Func<bool> Focus { get; private set; }
            public Func<Tooltip> Tooltip { get; private set; }

            /// <summary>The building's own button - what the row is drawn by.</summary>
            public Component Button { get; private set; }

            /// <summary>The building the details pane is describing.</summary>
            public bool IsSelected
            {
                get { return _isSelected == null || _isSelected(); }
            }

            public bool IsAvailable
            {
                get { return _isAvailable == null || _isAvailable(); }
            }
        }

        public sealed class TierItem
        {
            public TierItem(
                string label,
                int level,
                bool isSelected,
                Component button,
                Func<bool> focus,
                Func<bool> activate,
                Func<Tooltip> tooltip)
            {
                IsSelected = isSelected;
                Button = button;
                Activate = activate;
                Label = label;
                Level = level;
                Focus = focus;
                Tooltip = tooltip;
            }

            public string Label { get; private set; }
            public int Level { get; private set; }
            public Func<bool> Focus { get; private set; }
            public Func<bool> Activate { get; private set; }
            public Func<Tooltip> Tooltip { get; private set; }

            /// <summary>The tier the details pane is showing.</summary>
            public bool IsSelected { get; private set; }

            /// <summary>The tier's own button - what the tab is drawn by.</summary>
            public Component Button { get; private set; }
        }

        public sealed class SectionMenu
        {
            public SectionMenu(string label, IReadOnlyList<SectionItem> items)
            {
                Label = label ?? string.Empty;
                Items = items ?? new SectionItem[0];
            }

            public string Label { get; private set; }
            public IReadOnlyList<SectionItem> Items { get; private set; }
        }

        public sealed class SectionItem
        {
            public SectionItem(string label, Component target, Action focus, Func<Tooltip> tooltip)
            {
                Target = target;
                Label = label ?? string.Empty;
                Focus = focus;
                Tooltip = tooltip;
            }

            public string Label { get; private set; }
            public Action Focus { get; private set; }
            public Func<Tooltip> Tooltip { get; private set; }

            /// <summary>The entry's own background or icon - what the row is drawn by, and what its
            /// tooltip hangs on.</summary>
            public Component Target { get; private set; }
        }

        public sealed class RequirementItem
        {
            public RequirementItem(string label, bool isMet, Tooltip tooltip)
            {
                Label = label ?? string.Empty;
                IsMet = isMet;
                Tooltip = tooltip;
            }

            public string Label { get; private set; }
            public bool IsMet { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }
    }
}
