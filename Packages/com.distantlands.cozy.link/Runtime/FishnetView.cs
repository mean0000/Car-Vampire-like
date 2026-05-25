#if LINK_FISHNET
using System.Collections.Generic;
using FishNet.CodeGenerating;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace DistantLands.Cozy
{

    public class FishnetView : NetworkBehaviour
    {

        [HideInInspector]
        [AllowMutableSyncType]
        public SyncVar<float> time;
        [HideInInspector]
        [AllowMutableSyncType]
        public SyncVar<float> serverTime;
        [HideInInspector]
        [AllowMutableSyncType]
        public SyncVar<int> day;
        [HideInInspector]
        [AllowMutableSyncType]
        public SyncVar<int> year;
        [HideInInspector]
        [AllowMutableSyncType]
        public SyncVar<int> ambience;

        [HideInInspector]
        [AllowMutableSyncType]
        public SyncVar<string> weatherCacheString;
        [HideInInspector]
        [AllowMutableSyncType]
        public SyncVar<string> weatherValuesString;

        public bool isMaster { get { return IsHostInitialized || IsServerInitialized; } }

    }
}
#endif