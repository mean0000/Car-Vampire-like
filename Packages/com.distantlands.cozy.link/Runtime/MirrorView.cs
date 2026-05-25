#if LINK_MIRROR
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace DistantLands.Cozy
{

    public class MirrorView : NetworkBehaviour
    {

        [SyncVar]
        [HideInInspector]
        public float time;
        [SyncVar]
        [HideInInspector]
        public float serverTime;
        [HideInInspector]
        [SyncVar]
        public int day;
        [HideInInspector]
        [SyncVar]
        public int year;
        [HideInInspector]
        [SyncVar]
        public int ambience;

        [HideInInspector]
        [SyncVar]
        public string weatherCacheString;
        [HideInInspector]
        [SyncVar]
        public string weatherValuesString;

        public bool isMaster { get { return isServer; } }

    }
}
#endif