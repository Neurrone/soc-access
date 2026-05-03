using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PostBattleResultScreen : Screen
    {
        private readonly PostBattleResultAdapter _adapter;

        public PostBattleResultScreen(PostBattleResultAdapter adapter)
            : base(PostBattleResultAdapter.SourceKey, BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public PostBattleResultScreen Rebuild()
        {
            return new PostBattleResultScreen(_adapter);
        }

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
            _adapter?.HideNativeTooltip();
        }

        public override void OnPop()
        {
            _adapter?.HideNativeTooltip();
        }

        private static ContainerWidget BuildRoot(PostBattleResultAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("post-battle-result", "Battle result");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "post-battle-result-header",
                () => adapter.HeaderText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "post-battle-attacker-commander",
                () => adapter.AttackerCommanderText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                tooltip: adapter.AttackerCommanderTooltip));

            if (adapter.XpBelongsToAttacker)
            {
                AddXpWidget(root, adapter);
            }

            AddEntryMenu(root, "post-battle-attacker-troops-lost", "Attacker troops lost", adapter.AttackerTroopsLost, adapter, addNoneWhenEmpty: true);

            root.AddChild(new TextWidget(
                "post-battle-attacker-returned-troops",
                () => adapter.AttackerReturnedTroopsText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.AttackerReturnedTroopsVisible));

            root.AddChild(new TextWidget(
                "post-battle-defender-commander",
                () => adapter.DefenderCommanderText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                tooltip: adapter.DefenderCommanderTooltip));

            if (!adapter.XpBelongsToAttacker)
            {
                AddXpWidget(root, adapter);
            }

            AddEntryMenu(root, "post-battle-defender-troops-lost", "Defender troops lost", adapter.DefenderTroopsLost, adapter, addNoneWhenEmpty: true);

            root.AddChild(new TextWidget(
                "post-battle-defender-returned-troops",
                () => adapter.DefenderReturnedTroopsText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.DefenderReturnedTroopsVisible));

            AddEntryMenu(root, "post-battle-loot", "Loot", adapter.Loot, adapter, addNoneWhenEmpty: false);

            root.AddChild(adapter.BuildAcceptButton());
            root.AddChild(adapter.BuildRedoManualBattleButton());
            return root;
        }

        private static void AddXpWidget(ContainerWidget root, PostBattleResultAdapter adapter)
        {
            root.AddChild(new TextWidget(
                "post-battle-xp",
                () => adapter.XpText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.XpVisible));
        }

        private static void AddEntryMenu(
            ContainerWidget root,
            string id,
            string label,
            IReadOnlyList<PostBattleResultAdapter.ResultEntry> entries,
            PostBattleResultAdapter adapter,
            bool addNoneWhenEmpty)
        {
            if (root == null)
            {
                return;
            }

            MenuWidget menu = new MenuWidget(id, label);
            if (entries == null || entries.Count == 0)
            {
                if (!addNoneWhenEmpty)
                {
                    return;
                }

                menu.AddItem(new MenuItemWidget(
                    id + "-none",
                    () => "None",
                    getStatus: null,
                    activate: null,
                    onFocus: adapter.HideNativeTooltip,
                    isVisible: () => true));
                root.AddChild(menu);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                PostBattleResultAdapter.ResultEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    entry.Id,
                    () => entry.Label,
                    getStatus: null,
                    activate: null,
                    onFocus: adapter.HideNativeTooltip,
                    isVisible: () => entry.IsVisible,
                    tooltip: entry.Tooltip));
            }

            root.AddChild(menu);
        }
    }
}
