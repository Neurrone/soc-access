using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// What every troop-management host does the same way, whatever menu it wraps: a sub-page is
    /// drawn when either of its two pages is, and a host that opens with a wielder closes through
    /// the band's own cross. The defence menu opens without one and overrides the three close
    /// members with its menu's own cross.
    /// </summary>
    public abstract class TroopManagementHost
    {
        /// <summary>The band the host draws for the wielder who walked in, or null where there is no
        /// wielder to draw.</summary>
        public abstract WielderInteract Wielder { get; }

        public abstract bool IsDraftPresent();

        public abstract bool IsUpgradePresent();

        public bool IsPresent()
        {
            return IsDraftPresent() || IsUpgradePresent();
        }

        public virtual Component CloseButton
        {
            get
            {
                WielderInteract wielder = Wielder;
                return wielder != null ? wielder.CloseButton : null;
            }
        }

        public virtual bool IsCloseVisible()
        {
            WielderInteract wielder = Wielder;
            return wielder != null && wielder.IsCloseVisible;
        }

        public virtual bool Close()
        {
            WielderInteract wielder = Wielder;
            return wielder != null && wielder.ActivateClose();
        }
    }
}
