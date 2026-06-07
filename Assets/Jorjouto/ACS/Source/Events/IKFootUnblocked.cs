using UnityEngine;

namespace Jorjouto.AnimComposerSystem
{
    [CreateAssetMenu(menuName = "Event Channels/IKFootUnblocked")]
    public class IKFootUnblockedChannel : ScriptableObject
    {
        public delegate void IKFootUnblockedHandler(AnimCoordinatorComponent coordinator);
        private event IKFootUnblockedHandler OnEvent;

        /// <summary>
        /// Subscribe to this event
        /// </summary>
        public void Register(IKFootUnblockedHandler listener) => OnEvent += listener;

        /// <summary>
        /// Unsubscribe from this event
        /// </summary>
        public void Unregister(IKFootUnblockedHandler listener) => OnEvent -= listener;

        /// <summary>
        /// Raise the event
        /// </summary>
        public void Raise(AnimCoordinatorComponent coordinator) => OnEvent?.Invoke(coordinator);
    }
}
