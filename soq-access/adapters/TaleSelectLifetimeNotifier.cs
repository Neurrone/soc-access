using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class TaleSelectLifetimeNotifier : MonoBehaviour
    {
        private TaleButtonLayoutCoordinator _coordinator;

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
        }

        private void OnDestroy()
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnTaleSelectHidden(_coordinator);
        }
    }
}
