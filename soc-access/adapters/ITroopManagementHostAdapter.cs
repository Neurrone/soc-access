using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// What the four menus that draw a troop sub-page have in common, read as the game draws them:
    /// the dwelling, the town and the defence menu all hang a <c>PurchaseTroopsSubMenu</c> and an
    /// <c>UpgradeTroopsSubMenu</c> inside their own window, with their own tutorial button, their own
    /// wielder band and their own way back to whatever they show when no sub-page is open.
    ///
    /// <c>IsPresent</c> (from <c>IPresent</c>) is "one of my troop sub-pages is drawn" - which one
    /// is the two predicates below it. The host's own landing page is a different screen's page.
    ///
    /// Nothing here composes anything: the pages are named, grouped and worded by
    /// <c>screens/TroopManagementScreenBase.cs</c> and its two subclasses.
    /// </summary>
    public interface ITroopManagementHostAdapter : IPresent
    {
        /// <summary>Which host this is, for control keys and for the detector's own bookkeeping.
        /// </summary>
        string IdPrefix { get; }

        /// <summary>The title the host draws over the page - the building's name.</summary>
        string Title { get; }

        PurchaseTroopsSubMenuAdapter PurchaseTroops { get; }
        UpgradeTroopsSubMenuAdapter UpgradeTroops { get; }

        bool IsDraftPresent();
        bool IsUpgradePresent();

        /// <summary>The button the host draws in the corner until the player has seen its tutorial.
        /// Null on a host that draws none.</summary>
        Component TutorialButton { get; }
        bool IsTutorialVisible();
        string TutorialLabel { get; }
        bool ActivateTutorial();

        /// <summary>The band the host draws for the wielder who walked in, or null where there is no
        /// wielder to draw (the defence menu, opened without one).</summary>
        WielderInteract Wielder { get; }

        /// <summary>The button the host draws over a sub-page to go back to its landing page. Null,
        /// or not drawn, where this page IS the host's landing page.</summary>
        Component BackButton { get; }
        string BackLabel { get; }
        bool IsBackVisible();
        bool Back();

        /// <summary>The cross that shuts the whole menu.</summary>
        Component CloseButton { get; }
        bool IsCloseVisible();
        bool Close();
    }
}
