using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Gamestate.Facade;

namespace SongsOfConquestAccess.Adapters
{
    internal interface ITroopManagementHostAdapter
    {
        string IdPrefix { get; }
        string Title { get; }
        string DraftScreenTitle { get; }
        string UpgradeScreenTitle { get; }
        IClientAdventureFacade Facade { get; }
        PurchaseTroopsSubMenuAdapter PurchaseTroops { get; }
        UpgradeTroopsSubMenuAdapter UpgradeTroops { get; }

        bool IsDraftPresent();
        bool IsUpgradePresent();
        void HideNativeTooltip();

        bool IsTutorialVisible();
        string TutorialLabel { get; }
        bool ActivateTutorial();

        bool IsBackVisible();
        bool Back();

        string CloseLabel { get; }
        bool Close();

        bool HasWielderArmy { get; }
        string WielderName { get; }
        Tooltip WielderTooltip { get; }
        TroopHudAdapter WielderTroops { get; }

        bool ShouldRefreshForTroops(OnTroopsUpdatedPayload payload);
        bool ShouldRefreshForResource(ResourceUpdatedPayload payload);
        bool ShouldRefreshForRecruitmentPool();
    }
}
