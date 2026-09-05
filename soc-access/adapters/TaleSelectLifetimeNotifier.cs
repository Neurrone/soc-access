using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class TaleSelectLifetimeNotifier : MonoBehaviour
    {
        private TaleButtonLayoutCoordinator _coordinator;
        private static readonly List<TaleSelectLifetimeNotifier> Instances =
            new List<TaleSelectLifetimeNotifier>();

        // Components live on game objects, so a reload must take them down itself or the old
        // assembly's copies stay attached and keep firing into a mod that no longer exists.
        internal static void DetachAll()
        {
            foreach (TaleSelectLifetimeNotifier notifier in Instances.ToArray())
            {
                if (notifier == null) continue;
                notifier._coordinator = null;
                Destroy(notifier);
            }
            Instances.Clear();
        }

        public static void Attach(TaleButtonLayoutCoordinator coordinator)
        {
            if (coordinator == null)
            {
                return;
            }

            GameObject gameObject = ((Component)coordinator).gameObject;
            if (gameObject == null || gameObject.GetComponent<TaleSelectLifetimeNotifier>() != null)
            {
                return;
            }

            TaleSelectLifetimeNotifier notifier = gameObject.AddComponent<TaleSelectLifetimeNotifier>();
            notifier._coordinator = coordinator;
            Instances.Add(notifier);
        }

        private void OnDestroy()
        {
            Instances.Remove(this);
            if (_coordinator != null)
                SocAccessMod.Instance?.ScreenDetector?.OnTaleSelectClosed(_coordinator);
        }
    }
}
