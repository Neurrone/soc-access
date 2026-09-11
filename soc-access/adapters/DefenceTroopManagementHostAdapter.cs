using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>The defence menu as a troop-management host: it is opened without a wielder, so it
    /// draws no wielder band, and its close cross is its own rather than a band's.</summary>
    public sealed class DefenceTroopManagementHostAdapter : TroopManagementHost, ITroopManagementHostAdapter
    {
        private readonly DefenceMenuAdapter _adapter;

        public DefenceTroopManagementHostAdapter(DefenceMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public string IdPrefix { get { return "defences"; } }
        public string Title { get { return _adapter != null ? _adapter.Title : string.Empty; } }
        public PurchaseTroopsSubMenuAdapter PurchaseTroops { get { return _adapter != null ? _adapter.PurchaseTroops : null; } }
        public UpgradeTroopsSubMenuAdapter UpgradeTroops { get { return _adapter != null ? _adapter.UpgradeTroops : null; } }

        public override bool IsDraftPresent() { return _adapter != null && _adapter.IsDraftPresent(); }
        public override bool IsUpgradePresent() { return _adapter != null && _adapter.IsUpgradePresent(); }

        public Component TutorialButton { get { return _adapter != null ? _adapter.TutorialButton : null; } }
        public bool IsTutorialVisible() { return _adapter != null && _adapter.IsTutorialButtonVisible(); }
        public string TutorialLabel { get { return _adapter != null ? _adapter.GetTutorialButtonLabel() : string.Empty; } }
        public bool ActivateTutorial() { return _adapter != null && _adapter.ActivateTutorial(); }

        public override WielderInteract Wielder { get { return null; } }

        public Component BackButton { get { return _adapter != null ? _adapter.BackButton : null; } }
        public string BackLabel { get { return _adapter != null ? _adapter.BackLabel : string.Empty; } }
        public bool IsBackVisible() { return _adapter != null && _adapter.IsBackVisible(); }
        public bool Back() { return _adapter != null && _adapter.BackToTop(); }

        public override Component CloseButton { get { return _adapter != null ? _adapter.CloseButton : null; } }
        public override bool IsCloseVisible() { return _adapter != null && _adapter.IsCloseVisible(); }
        public override bool Close() { return _adapter != null && _adapter.ActivateClose(); }
    }
}
