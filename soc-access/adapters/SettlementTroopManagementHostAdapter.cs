using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class SettlementTroopManagementHostAdapter : ITroopManagementHostAdapter
    {
        private readonly TownInteractionMenuAdapter _adapter;

        public SettlementTroopManagementHostAdapter(TownInteractionMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public string IdPrefix { get { return "settlement"; } }
        public string Title { get { return _adapter != null ? _adapter.Title : string.Empty; } }
        public string DraftScreenTitle { get { return _adapter != null ? _adapter.DraftLabel : string.Empty; } }
        public string UpgradeScreenTitle { get { return _adapter != null ? _adapter.UpgradeLabel : string.Empty; } }
        public IClientAdventureFacade Facade { get { return _adapter != null ? _adapter.Facade : null; } }
        public PurchaseTroopsSubMenuAdapter PurchaseTroops { get { return _adapter != null ? _adapter.PurchaseTroops : null; } }
        public UpgradeTroopsSubMenuAdapter UpgradeTroops { get { return _adapter != null ? _adapter.UpgradeTroops : null; } }

        public bool IsDraftPresent() { return _adapter != null && _adapter.IsDraftPresent(); }
        public bool IsUpgradePresent() { return _adapter != null && _adapter.IsUpgradePresent(); }
        public void HideNativeTooltip() { _adapter?.HideNativeTooltip(); }

        public bool IsTutorialVisible() { return _adapter != null && _adapter.IsTutorialButtonVisible(); }
        public string TutorialLabel { get { return _adapter != null ? _adapter.GetTutorialButtonLabel() : string.Empty; } }
        public bool ActivateTutorial() { return _adapter != null && _adapter.ActivateTutorial(); }

        public bool IsBackVisible() { return IsDraftPresent() || IsUpgradePresent(); }
        public bool Back() { return _adapter != null && _adapter.BackToTop(); }

        public string CloseLabel { get { return _adapter != null ? _adapter.CloseLabel : string.Empty; } }
        public bool Close() { return _adapter != null && _adapter.Close(); }

        public bool HasWielderArmy { get { return true; } }
        public string WielderName { get { return _adapter != null ? _adapter.VisitingWielderName : string.Empty; } }
        public Tooltip WielderTooltip { get { return _adapter != null ? _adapter.VisitingWielderTooltip : null; } }
        public TroopHudAdapter WielderTroops { get { return _adapter != null ? _adapter.VisitingTroops : null; } }

        public bool ShouldRefreshForTroops(OnTroopsUpdatedPayload payload)
        {
            return payload != null
                && _adapter != null
                && payload.ParentType == TroopParentType.Commander
                && payload.ParentId == _adapter.VisitingCommanderId;
        }

        public bool ShouldRefreshForResource(ResourceUpdatedPayload payload)
        {
            return payload != null;
        }

        public bool ShouldRefreshForRecruitmentPool()
        {
            return true;
        }
    }
}
