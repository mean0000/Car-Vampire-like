#if LINK_PURRNET
using PurrNet;
using UnityEngine;

namespace DistantLands.Cozy
{
    public class PurrNetView : NetworkBehaviour
    {
        [HideInInspector]
        public SyncVar<float> time;
        [HideInInspector]
        public SyncVar<float> serverTime;
        [HideInInspector]
        public SyncVar<int> day;
        [HideInInspector]
        public SyncVar<int> year;
        [HideInInspector]
        public SyncVar<int> ambience;

        [HideInInspector]
        public SyncVar<string> weatherCacheString;
        [HideInInspector]
        public SyncVar<string> weatherValuesString;

        public bool isMaster { get { return isHost || isServer; } }
    }
}
#endif