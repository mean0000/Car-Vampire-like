using UnityEngine;

namespace Jorjouto.AnimComposerSystem
{
    [CreateAssetMenu(menuName = "Event Channels/IKFootBlocked")]
    public class IKFootUnBlockedChannel : ScriptableObject
    {
        public delegate void IKFootBlockedHandler(AnimCoordinatorComponent coordinator);
        private event IKFootBlockedHandler OnEvent;

        /// <summary>
        /// Subscribe to this event
        /// </summary>
        public void Register(IKFootBlockedHandler listener) => OnEvent += listener;

        /// <summary>
        /// Unsubscribe from this event
        /// </summary>
        public void Unregister(IKFootBlockedHandler listener) => OnEvent -= listener;

        /// <summary>
        /// Raise the event
        /// </summary>
        public void Raise(AnimCoordinatorComponent coordinator) => OnEvent?.Invoke(coordinator);
    }
}
