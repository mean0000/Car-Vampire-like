#if LINK_NETCODE
using Unity.Netcode;
using UnityEngine;

namespace DistantLands.Cozy
{

    public class NetcodeView : NetworkBehaviour
    {

        [HideInInspector]
        public NetworkVariable<float> ticks = new NetworkVariable<float>();
        [HideInInspector]
        public NetworkVariable<int> day = new NetworkVariable<int>();
        [HideInInspector]
        public NetworkVariable<int> year = new NetworkVariable<int>();
        [HideInInspector]
        public NetworkVariable<int> ambience = new NetworkVariable<int>();
        [HideInInspector]
        public NetworkList<int> weatherCache;
        [HideInInspector]
        public NetworkList<float> weatherValues;


        void Awake()
        {

            weatherCache = new NetworkList<int>();
            weatherValues = new NetworkList<float>();

        }


    }
}
#endif