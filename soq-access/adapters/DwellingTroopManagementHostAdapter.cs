using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Gamestate.Facade;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class DwellingTroopManagementHostAdapter : ITroopManagementHostAdapter
    {
        private readonly DwellingInteractionMenuAdapter _adapter;

        public DwellingTroopManagementHostAdapter(DwellingInteractionMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public string IdPrefix { get { return "dwelling"; } }
        public string Title { get { return _adapter != null ? _adapter.Title : "Dwelling"; } }
        public string DraftScreenTitle { get { return "Draft troops"; } }
        public string UpgradeScreenTitle { get { return "Upgrade troops"; } }
        public IClientAdventureFacade Facade { get { return _adapter != null ? _adapter.Facade : null; } }
        public PurchaseTroopsSubMenuAdapter PurchaseTroops { get { return _adapter != null ? _adapter.PurchaseTroops : null; } }
        public UpgradeTroopsSubMenuAdapter UpgradeTroops { get { return _adapter != null ? _adapter.UpgradeTroops : null; } }

        public bool IsDraftPresent() { return _adapter != null && _adapter.IsDraftPresent(); }
        public bool IsUpgradePresent() { return _adapter != null && _adapter.IsUpgradePresent(); }
        public void HideNativeTooltip() { _adapter?.HideNativeTooltip(); }

        public bool IsTutorialVisible() { return false; }
        public string TutorialLabel { get { return "Tutorial available"; } }
        public bool ActivateTutorial() { return false; }

        public bool IsBackVisible() { return IsUpgradePresent(); }
        public bool Back() { return _adapter != null && _adapter.BackToTop(); }

        public string CloseLabel { get { return _adapter != null ? _adapter.CloseLabel : "Close"; } }
        public bool Close() { return _adapter != null && _adapter.Close(); }

        public bool HasWielderArmy { get { return true; } }
        public string WielderName { get { return _adapter != null ? _adapter.WielderName : "Wielder"; } }
        public Tooltip WielderTooltip { get { return _adapter != null ? _adapter.WielderTooltip : null; } }
        public TroopHudAdapter WielderTroops { get { return _adapter != null ? _adapter.Troops : null; } }

        public bool ShouldRefreshForTroops(OnTroopsUpdatedPayload payload)
        {
            return payload != null;
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
