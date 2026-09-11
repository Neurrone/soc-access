using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>The town as a troop-management host: both sub-pages sit over its landing page, which
    /// is what its back button returns to.</summary>
    public sealed class SettlementTroopManagementHostAdapter : TroopManagementHost, ITroopManagementHostAdapter
    {
        private readonly TownInteractionMenuAdapter _adapter;

        public SettlementTroopManagementHostAdapter(TownInteractionMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public TroopManagementHostKind Kind { get { return TroopManagementHostKind.Settlement; } }
        public string Title { get { return _adapter != null ? _adapter.Title : string.Empty; } }
        public PurchaseTroopsSubMenuAdapter PurchaseTroops { get { return _adapter != null ? _adapter.PurchaseTroops : null; } }
        public UpgradeTroopsSubMenuAdapter UpgradeTroops { get { return _adapter != null ? _adapter.UpgradeTroops : null; } }

        public override bool IsDraftPresent() { return _adapter != null && _adapter.IsDraftPresent(); }
        public override bool IsUpgradePresent() { return _adapter != null && _adapter.IsUpgradePresent(); }

        public Component TutorialButton { get { return _adapter != null ? _adapter.TutorialButton : null; } }
        public bool IsTutorialVisible() { return _adapter != null && _adapter.IsTutorialButtonVisible(); }
        public string TutorialLabel { get { return _adapter != null ? _adapter.GetTutorialButtonLabel() : string.Empty; } }
        public bool ActivateTutorial() { return _adapter != null && _adapter.ActivateTutorial(); }

        public override WielderInteract Wielder { get { return _adapter != null ? _adapter.Wielder : null; } }

        public Component BackButton { get { return _adapter != null ? _adapter.BackButton : null; } }
        public string BackLabel { get { return _adapter != null ? _adapter.BackLabel : string.Empty; } }
        public bool IsBackVisible() { return _adapter != null && _adapter.IsBackVisible(); }
        public bool Back() { return _adapter != null && _adapter.BackToTop(); }
    }
}
