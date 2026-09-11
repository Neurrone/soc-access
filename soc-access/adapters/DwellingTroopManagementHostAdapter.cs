using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>The dwelling as a troop-management host: the draft page IS its landing page, so the
    /// back button is drawn only over the upgrade page, and it draws no tutorial button at all.
    /// </summary>
    public sealed class DwellingTroopManagementHostAdapter : TroopManagementHost, ITroopManagementHostAdapter
    {
        private readonly DwellingInteractionMenuAdapter _adapter;

        public DwellingTroopManagementHostAdapter(DwellingInteractionMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public string IdPrefix { get { return "dwelling"; } }
        public string Title { get { return _adapter != null ? _adapter.Title : string.Empty; } }
        public PurchaseTroopsSubMenuAdapter PurchaseTroops { get { return _adapter != null ? _adapter.PurchaseTroops : null; } }
        public UpgradeTroopsSubMenuAdapter UpgradeTroops { get { return _adapter != null ? _adapter.UpgradeTroops : null; } }

        public override bool IsDraftPresent() { return _adapter != null && _adapter.IsDraftPresent(); }
        public override bool IsUpgradePresent() { return _adapter != null && _adapter.IsUpgradePresent(); }

        public Component TutorialButton { get { return null; } }
        public bool IsTutorialVisible() { return false; }
        public string TutorialLabel { get { return string.Empty; } }
        public bool ActivateTutorial() { return false; }

        public override WielderInteract Wielder { get { return _adapter != null ? _adapter.Wielder : null; } }

        public Component BackButton { get { return _adapter != null ? _adapter.BackButton : null; } }
        public string BackLabel { get { return _adapter != null ? _adapter.BackLabel : string.Empty; } }
        public bool IsBackVisible() { return _adapter != null && _adapter.IsBackVisible(); }
        public bool Back() { return _adapter != null && _adapter.BackToTop(); }
    }
}
