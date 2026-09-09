using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>The town as a troop-management host: both sub-pages sit over its landing page, which
    /// is what its back button returns to.</summary>
    public sealed class SettlementTroopManagementHostAdapter : ITroopManagementHostAdapter
    {
        private readonly TownInteractionMenuAdapter _adapter;

        public SettlementTroopManagementHostAdapter(TownInteractionMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public string IdPrefix { get { return "settlement"; } }
        public string Title { get { return _adapter != null ? _adapter.Title : string.Empty; } }
        public PurchaseTroopsSubMenuAdapter PurchaseTroops { get { return _adapter != null ? _adapter.PurchaseTroops : null; } }
        public UpgradeTroopsSubMenuAdapter UpgradeTroops { get { return _adapter != null ? _adapter.UpgradeTroops : null; } }

        public bool IsPresent() { return IsDraftPresent() || IsUpgradePresent(); }
        public bool IsDraftPresent() { return _adapter != null && _adapter.IsDraftPresent(); }
        public bool IsUpgradePresent() { return _adapter != null && _adapter.IsUpgradePresent(); }

        public Component TutorialButton { get { return _adapter != null ? _adapter.TutorialButton : null; } }
        public bool IsTutorialVisible() { return _adapter != null && _adapter.IsTutorialButtonVisible(); }
        public string TutorialLabel { get { return _adapter != null ? _adapter.GetTutorialButtonLabel() : string.Empty; } }
        public bool ActivateTutorial() { return _adapter != null && _adapter.ActivateTutorial(); }

        public WielderInteract Wielder { get { return _adapter != null ? _adapter.Wielder : null; } }

        public Component BackButton { get { return _adapter != null ? _adapter.BackButton : null; } }
        public string BackLabel { get { return _adapter != null ? _adapter.BackLabel : string.Empty; } }
        public bool IsBackVisible() { return _adapter != null && _adapter.IsBackVisible(); }
        public bool Back() { return _adapter != null && _adapter.BackToTop(); }

        public Component CloseButton
        {
            get
            {
                WielderInteract wielder = Wielder;
                return wielder != null ? wielder.CloseButton : null;
            }
        }

        public bool IsCloseVisible()
        {
            WielderInteract wielder = Wielder;
            return wielder != null && wielder.IsCloseVisible;
        }

        public bool Close()
        {
            WielderInteract wielder = Wielder;
            return wielder != null && wielder.ActivateClose();
        }
    }
}
