using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using DistantLands.Cozy.Data;
#if LINK_NETCODE
using Unity.Netcode;
#endif
#if LINK_PUN
using Photon.Pun;
using Photon.Realtime;
#endif
#if LINK_FISHNET
using FishNet.Object;
#endif
#if LINK_MIRROR
#endif
#if LINK_PURRNET
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
#if LINK_NETCODE
    [RequireComponent(typeof(NetcodeView))]
    [RequireComponent(typeof(NetworkObject))]
#endif
#if LINK_PUN
    [RequireComponent(typeof(PhotonView))]
#endif
    // #if LINK_FISHNET
    //     [RequireComponent(typeof(FishnetView))]
    //     [RequireComponent(typeof(NetworkObject))]
    // #endif
#if LINK_PURRNET
    [RequireComponent(typeof(PurrNetView))]
#endif
#if LINK_MIRROR
#endif
    [ExecuteAlways]
    public class LinkModule : CozyModule
    {
        static readonly string[] Integrations = new string[6] { "None", "Netcode for Gameobjects", "PUN", "Mirror", "FishNetworking", "PurrNet" };
        public static string SelectedIntegrationName => Integrations[SelectedIntegration];
        public static int SelectedIntegration
        {
            get
            {
#if LINK_NETCODE
                return 1;
#elif LINK_PUN
                return 2;
#elif LINK_MIRROR
                return 3;
#elif LINK_FISHNET
                return 4;
#elif LINK_PURRNET
                return 5;
#else
                return 0;
#endif
            }
        }


        public bool linkTime = true;
        public bool linkWeather = true;
        public bool linkAmbience = true;
        [Tooltip("Controls the amount of time (in seconds) before an RPC is sent to the server to sync the COZY systems.")]
        [Range(0, 6)]
        public float updateDelay = 0.5f;
        [Tooltip("Controls the amount of ticks away from the main server a client has to be before resyncing with the server. (Default: 0.05)")]
        [Range(0, 0.5f)]
        public float timeSettingSensitivity = 0.05f;
        float currentDelay;

        public Dictionary<WeatherProfile, int> weatherHashes = new Dictionary<WeatherProfile, int>();
        public Dictionary<AmbienceProfile, int> ambienceHashes = new Dictionary<AmbienceProfile, int>();
        public CozyAmbienceModule ambienceManager;

#if LINK_NETCODE
        public bool isMaster { get { return netcodeView.NetworkManager.IsServer || netcodeView.NetworkManager.IsHost; } }

        //VARIABLES______________________________________________________________________________________________________

        private NetcodeView nvCache;

        //FUNCTIONS_______________________________________________________________________________________________________
        public void Update()
        {
            if (!Application.isPlaying)
                return;
            if (currentDelay <= 0)
            {
                if (isMaster)
                    MasterCommunication();
                else
                    ClientCommunication();
                currentDelay = updateDelay;
            }
            else
                currentDelay -= Time.deltaTime;

        }

        public NetcodeView netcodeView
        {
            get
            {
                if (nvCache)
                {

                    return nvCache;

                }
                else
                {
                    nvCache = GetComponent<NetcodeView>();
                    return nvCache;
                }

            }
        }


        public void SyncCozyTime()
        {

            if (Mathf.Abs(netcodeView.ticks.Value - weatherSphere.timeModule.currentTime) > timeSettingSensitivity)
                weatherSphere.timeModule.currentTime = netcodeView.ticks.Value;
            weatherSphere.timeModule.currentDay = netcodeView.day.Value;
            weatherSphere.timeModule.currentYear = netcodeView.year.Value;

        }


        public void SyncCozyAmbience()
        {

            ambienceManager.currentAmbienceProfile = ambienceManager.ambienceProfiles[netcodeView.ambience.Value];

        }


        public void SyncCozyWeather()
        {

            List<WeatherRelation> i = new List<WeatherRelation>();
            int l = 0;

            foreach (int j in netcodeView.weatherCache)
            {

                WeatherRelation k = new WeatherRelation();
                k.profile = weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast[j];
                k.weight = netcodeView.weatherValues[l];
                i.Add(k);
                l++;

            }
            weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles = i;

        }

        public void SyncCozyTimeMaster()
        {

            netcodeView.ticks.Value = weatherSphere.timeModule.currentTime;
            netcodeView.day.Value = weatherSphere.timeModule.currentDay;
            netcodeView.year.Value = weatherSphere.timeModule.currentYear;

        }

        public void SyncCozyAmbienceMaster()
        {

            netcodeView.ambience.Value = ambienceHashes[ambienceManager.currentAmbienceProfile];

        }

        public void SyncCozyWeatherMaster()
        {

            netcodeView.weatherCache.Clear();

            foreach (int i in GetWeatherIDs())
                netcodeView.weatherCache.Add(i);

            netcodeView.weatherValues.Clear();

            foreach (float i in GetWeatherIntensities())
                netcodeView.weatherValues.Add(i);

        }

        private void MasterCommunication()
        {


            if (linkTime) SyncCozyTimeMaster();
            if (linkAmbience) SyncCozyAmbienceMaster();
            if (linkWeather) SyncCozyWeatherMaster();

        }

        private void ClientCommunication()
        {


            if (linkTime) SyncCozyTime();
            if (linkAmbience) SyncCozyAmbience();
            if (linkWeather) SyncCozyWeather();

        }
        
        public override void InitializeModule()
        {
            if (!Application.isPlaying)
                return;

            base.InitializeModule();

            for (int i = 0; i < weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast.Count; i++)
            {
                weatherHashes.Add(weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast[i], i);
            }


            if (linkAmbience)
            {

                if (weatherSphere.GetModule<CozyAmbienceModule>() == null)
                {
                    linkAmbience = false;
                }
                else
                {
                    ambienceManager = weatherSphere.GetModule<CozyAmbienceModule>();

                    for (int i = 0; i < ambienceManager.ambienceProfiles.Length; i++)
                    {
                        ambienceHashes.Add(ambienceManager.ambienceProfiles[i], i);
                    }

                }


            }

        }

#endif

#if LINK_PUN
        public bool isMaster => PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient;
        public enum LinkType { server, client }
        public LinkType linkType;

        private float m_Ticks;

        //VARIABLES______________________________________________________________________________________________________
        private PhotonView pvCache;


        //FUNCTIONS_______________________________________________________________________________________________________
        public void Update()
        {
            if (!Application.isPlaying)
                return;

            if (isMaster)
            {

                if (currentDelay <= 0)
                {
                    MasterSendRPC();
                    currentDelay = updateDelay;
                }
                else
                    currentDelay -= Time.deltaTime;

            }

        }

        public PhotonView photonView
        {
            get
            {
                if (pvCache)
                {

                    return pvCache;

                }
                else
                {
                    pvCache = GetComponent<PhotonView>();
                    return pvCache;
                }

            }
        }

        [PunRPC]
        public void SyncCozyTime(float currentTicks, int currentDay, int currentYear)
        {

            if (Mathf.Abs(currentTicks - weatherSphere.timeModule.currentTime) > timeSettingSensitivity)
                weatherSphere.timeModule.currentTime = currentTicks;
            weatherSphere.timeModule.currentYear = currentYear;
            weatherSphere.timeModule.currentDay = currentDay;

        }
        [PunRPC]
        public void SyncCozyAmbience(int ambience)
        {

            ambienceManager.currentAmbienceProfile = ambienceManager.ambienceProfiles[ambience];

        }

        [PunRPC]
        public void SyncCozyWeather(int[] cache, float[] values)
        {

            List<WeatherRelation> i = new List<WeatherRelation>();
            int l = 0;

            foreach (int j in cache)
            {

                WeatherRelation k = new WeatherRelation();
                k.profile = weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast[j];
                k.weight = values[l];
                i.Add(k);
                l++;

            }
            weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles = i;

        }

        public int[] GetWeatherIDs()
        {

            List<int> i = new List<int>();

            foreach (WeatherRelation j in weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles)
            {
                i.Add(weatherHashes[j.profile]);
            }

            return i.ToArray();

        }

        public float[] GetWeatherIntensities()
        {

            List<float> i = new List<float>();

            foreach (WeatherRelation j in weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles)
            {
                i.Add(j.weight);
            }

            return i.ToArray();

        }



        private void MasterSendRPC()
        {

            if (linkTime) photonView.RPC(nameof(SyncCozyTime), RpcTarget.Others, (float)weatherSphere.timeModule.currentTime, weatherSphere.timeModule.currentDay, weatherSphere.timeModule.currentYear);
            if (linkAmbience) photonView.RPC(nameof(SyncCozyAmbience), RpcTarget.Others, ambienceHashes[ambienceManager.currentAmbienceProfile]);
            if (linkWeather) photonView.RPC(nameof(SyncCozyWeather), RpcTarget.Others, GetWeatherIDs(), GetWeatherIntensities());

        }



        public override void InitializeModule()
        {
            if (!Application.isPlaying)
                return;

            base.InitializeModule();

            weatherHashes.Clear();

            for (int i = 0; i < weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast.Count; i++)
            {
                weatherHashes.Add(weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast[i], i);
            }


            if (linkAmbience)
            {

                if (weatherSphere.GetModule<CozyAmbienceModule>() == null)
                {
                    linkAmbience = false;
                }
                else
                {
                    ambienceManager = weatherSphere.GetModule<CozyAmbienceModule>();

                    for (int i = 0; i < ambienceManager.ambienceProfiles.Length; i++)
                    {
                        ambienceHashes.Add(ambienceManager.ambienceProfiles[i], i);
                    }

                }


            }

            PhotonNetwork.AddCallbackTarget(this);
            photonView.AddCallbackTarget(this);

        }

        public void OnDisable()
        {


            PhotonNetwork.RemoveCallbackTarget(this);
            photonView.RemoveCallback<LinkPhotonModule>(this);


        }

#endif

#if LINK_MIRROR
        public bool isMaster { get { return mirrorView.isMaster; } }

        //VARIABLES______________________________________________________________________________________________________
        private MirrorView nvCache;


        //FUNCTIONS_______________________________________________________________________________________________________
        public void Update()
        {
            if (!Application.isPlaying)
                return;

            if (currentDelay <= 0)
            {
                try
                {

                    if (isMaster)
                        MasterCommunication();
                    else
                        ClientCommunication();
                }
                catch
                {

                }
                currentDelay = updateDelay;
            }
            else
                currentDelay -= Time.deltaTime;

        }

        public MirrorView mirrorView
        {
            get
            {
                if (nvCache)
                {

                    return nvCache;

                }
                else
                {
                    nvCache = GetComponentInParent<MirrorView>();
                    if (nvCache == null)
                    {
                        Debug.LogError("Make sure to add the Mirror View component to your weather sphere!");
                        return null;
                    }
                    return nvCache;
                }

            }
        }




        public void SyncCozyTime()
        {

            if (Mathf.Abs(mirrorView.time - weatherSphere.timeModule.currentTime) > timeSettingSensitivity)
                weatherSphere.timeModule.currentTime = mirrorView.time;
            weatherSphere.timeModule.currentDay = mirrorView.day;
            weatherSphere.timeModule.currentYear = mirrorView.year;

        }


        public void SyncCozyAmbience()
        {

            ambienceManager.currentAmbienceProfile = ambienceManager.ambienceProfiles[mirrorView.ambience];

        }


        public void SyncCozyWeather()
        {

            weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles.Clear();
            int l = 0;
            int[] weatherCache = PollValues(mirrorView.weatherCacheString);

            foreach (int j in weatherCache)
            {

                WeatherRelation k = new WeatherRelation();
                k.profile = weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast[j];
                k.weight = PollWeightValues(mirrorView.weatherValuesString)[l];
                weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles.Add(k);
                l++;

            }
        }

        public void SyncCozyTimeMaster()
        {

            mirrorView.time = weatherSphere.timeModule.currentTime;
            mirrorView.day = weatherSphere.timeModule.currentDay;
            mirrorView.year = weatherSphere.timeModule.currentYear;

        }

        public void SyncCozyAmbienceMaster()
        {

            List<AmbienceProfile> ambiences = ambienceManager.ambienceProfiles.ToList();
            mirrorView.ambience = ambiences.IndexOf(ambienceManager.currentAmbienceProfile);

        }

        public void SyncCozyWeatherMaster()
        {

            string weatherIDString = "";

            foreach (int i in GetWeatherIDs())
                weatherIDString = $"{weatherIDString}{i},";

            mirrorView.weatherCacheString = weatherIDString;

            weatherIDString = "";

            foreach (float i in GetWeatherIntensities())
                weatherIDString = $"{weatherIDString}{i},";

            mirrorView.weatherValuesString = weatherIDString;

        }

        private void MasterCommunication()
        {


            if (linkTime) SyncCozyTimeMaster();
            if (linkAmbience) SyncCozyAmbienceMaster();
            if (linkWeather) SyncCozyWeatherMaster();


        }

        private void ClientCommunication()
        {

            if (linkTime) SyncCozyTime();
            if (linkAmbience) SyncCozyAmbience();
            if (linkWeather) SyncCozyWeather();

        }

        public override void InitializeModule()
        {
            if (!Application.isPlaying)
                return;

            base.InitializeModule();

            weatherHashes.Clear();

            for (int i = 0; i < weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast.Count; i++)
            {
                if (!weatherHashes.ContainsKey(weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast[i]))
                    weatherHashes.Add(weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast[i], i);
            }

            if (linkAmbience)
            {
                if (weatherSphere.GetModule<CozyAmbienceModule>() == null)
                {
                    linkAmbience = false;
                }
                else
                {
                    ambienceManager = weatherSphere.GetModule<CozyAmbienceModule>();
                }
            }
        }
#endif

#if LINK_PURRNET
        public bool isMaster { get { return purrNetView.isMaster; } }

        private PurrNetView nvCache;

        public void Update()
        {
            if (!Application.isPlaying)
                return;
            if (currentDelay <= 0)
            {
                if (isMaster)
                    MasterCommunication();
                else
                    ClientCommunication();
                currentDelay = updateDelay;
            }
            else
                currentDelay -= Time.deltaTime;
        }

        public PurrNetView purrNetView
        {
            get
            {
                if (nvCache != null)
                {
                    return nvCache;
                }
                else
                {
                    nvCache = GetComponent<PurrNetView>();
                    return nvCache;
                }

            }
        }

        public void SyncCozyTime()
        {
            if (Mathf.Abs(purrNetView.time.value - weatherSphere.dayPercentage) > timeSettingSensitivity)
                weatherSphere.timeModule.currentTime = purrNetView.time.value;
            weatherSphere.timeModule.currentDay = purrNetView.day.value;
            weatherSphere.timeModule.currentYear = purrNetView.year.value;
        }

        public void SyncCozyAmbience()
        {
            ambienceManager.currentAmbienceProfile = ambienceManager.ambienceProfiles[purrNetView.ambience.value];
        }

        public void SyncCozyWeather()
        {
            weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles.Clear();
            int l = 0;
            int[] weatherCache = PollValues(purrNetView.weatherCacheString.value);
            float[] weights = PollWeightValues(purrNetView.weatherValuesString.value);

            int count = Mathf.Min(weatherCache.Length, weights.Length);
            for (int i = 0; i < count; i++)
            {
                WeatherRelation relation = new WeatherRelation();
                relation.profile = weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast[weatherCache[i]];
                relation.weight = weights[i];
                weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles.Add(relation);
            }
        }

        public void SyncCozyTimeMaster()
        {
            purrNetView.time.value = weatherSphere.timeModule.currentTime;
            purrNetView.day.value = weatherSphere.timeModule.currentDay;
            purrNetView.year.value = weatherSphere.timeModule.currentYear;
        }

        public void SyncCozyAmbienceMaster()
        {
            List<AmbienceProfile> ambiences = ambienceManager.ambienceProfiles.ToList();
            purrNetView.ambience.value = ambiences.IndexOf(ambienceManager.currentAmbienceProfile);
        }

        public void SyncCozyWeatherMaster()
        {
            string weatherIDString = "";

            foreach (int i in GetWeatherIDs())
                weatherIDString = $"{weatherIDString}{i},";

            purrNetView.weatherCacheString.value = weatherIDString;

            weatherIDString = "";

            foreach (float i in GetWeatherIntensities())
                weatherIDString = $"{weatherIDString}{i},";

            purrNetView.weatherValuesString.value = weatherIDString;
        }

        private void MasterCommunication()
        {
            if (weatherSphere == null)
                weatherSphere = CozyWeather.instance;

            if (linkTime) SyncCozyTimeMaster();
            if (linkAmbience) SyncCozyAmbienceMaster();
            if (linkWeather) SyncCozyWeatherMaster();
        }

        private void ClientCommunication()
        {
            if (weatherSphere == null)
                weatherSphere = CozyWeather.instance;

            if (linkTime) SyncCozyTime();
            if (linkAmbience) SyncCozyAmbience();
            if (linkWeather) SyncCozyWeather();
        }

        public override void InitializeModule()
        {
            if (!Application.isPlaying)
                return;
            base.InitializeModule();
            weatherHashes.Clear();

            for (int i = 0; i < weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast.Count; i++)
            {
                weatherHashes.Add(weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast[i], i);
            }


            if (linkAmbience)
            {

                if (weatherSphere.GetModule<CozyAmbienceModule>() == null)
                {
                    linkAmbience = false;
                }
                else
                {
                    ambienceManager = weatherSphere.GetModule<CozyAmbienceModule>();
                    ambienceHashes.Clear();
                    for (int i = 0; i < ambienceManager.ambienceProfiles.Length; i++)
                    {
                        if (!ambienceHashes.ContainsKey(ambienceManager.ambienceProfiles[i]))
                            ambienceHashes.Add(ambienceManager.ambienceProfiles[i], i);
                    }

                }


            }
        }
#endif

#if LINK_FISHNET
        public bool isMaster
        {
            get
            {
                return fishnetView.isMaster;
            }
        }

        //VARIABLES______________________________________________________________________________________________________
        private FishnetView nvCache;


        //FUNCTIONS_______________________________________________________________________________________________________

        public void Update()
        {
            if (!Application.isPlaying)
                return;


            if (currentDelay <= 0)
            {
                if (isMaster)
                    MasterCommunication();
                else
                    ClientCommunication();
                currentDelay = updateDelay;
            }
            else
                currentDelay -= Time.deltaTime;

        }

        public FishnetView fishnetView
        {
            get
            {
                if (nvCache != null)
                {

                    return nvCache;

                }
                else
                {
                    nvCache = GetComponentInParent<FishnetView>();

                    return nvCache;
                }

            }
        }

        public void SyncCozyTime()
        {

            if (Mathf.Abs(fishnetView.time.Value - weatherSphere.dayPercentage) > timeSettingSensitivity)
                weatherSphere.timeModule.currentTime = fishnetView.time.Value;
            weatherSphere.timeModule.currentDay = fishnetView.day.Value;
            weatherSphere.timeModule.currentYear = fishnetView.year.Value;

        }

        public void SyncCozyAmbience()
        {

            ambienceManager.currentAmbienceProfile = ambienceManager.ambienceProfiles[fishnetView.ambience.Value];

        }

        public void SyncCozyWeather()
        {

            weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles.Clear();
            int l = 0;
            int[] weatherCache = PollValues(fishnetView.weatherCacheString.Value);

            foreach (int j in weatherCache)
            {

                WeatherRelation k = new WeatherRelation();
                k.profile = weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast[j];
                k.weight = PollWeightValues(fishnetView.weatherValuesString.Value)[l];
                weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles.Add(k);
                l++;

            }
        }

        public void SyncCozyTimeMaster()
        {
            fishnetView.time.Value = weatherSphere.timeModule.currentTime;
            fishnetView.day.Value = weatherSphere.timeModule.currentDay;
            fishnetView.year.Value = weatherSphere.timeModule.currentYear;

        }

        public void SyncCozyAmbienceMaster()
        {

            List<AmbienceProfile> ambiences = ambienceManager.ambienceProfiles.ToList();
            fishnetView.ambience.Value = ambiences.IndexOf(ambienceManager.currentAmbienceProfile);

        }

        public void SyncCozyWeatherMaster()
        {

            string weatherIDString = "";

            foreach (int i in GetWeatherIDs())
                weatherIDString = $"{weatherIDString}{i},";

            fishnetView.weatherCacheString.Value = weatherIDString;

            weatherIDString = "";

            foreach (float i in GetWeatherIntensities())
                weatherIDString = $"{weatherIDString}{i},";

            fishnetView.weatherValuesString.Value = weatherIDString;

        }

        private void MasterCommunication()
        {


            if (linkTime) SyncCozyTimeMaster();
            if (linkAmbience) SyncCozyAmbienceMaster();
            if (linkWeather) SyncCozyWeatherMaster();


        }

        private void ClientCommunication()
        {

            if (linkTime) SyncCozyTime();
            if (linkAmbience) SyncCozyAmbience();
            if (linkWeather) SyncCozyWeather();

        }

        public override void InitializeModule()
        {
            if (!weatherSphere.gameObject.GetComponent<FishnetView>())
                weatherSphere.gameObject.AddComponent<FishnetView>();

            if (!Application.isPlaying)
                return;

            base.InitializeModule();

            if (linkAmbience)
            {
                if (weatherSphere.GetModule<CozyAmbienceModule>() == null)
                {
                    linkAmbience = false;
                }
                else
                {
                    ambienceManager = weatherSphere.GetModule<CozyAmbienceModule>();
                }
            }

            weatherHashes.Clear();

            for (int i = 0; i < weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast.Count; i++)
            {
                weatherHashes.Add(weatherSphere.weatherModule.ecosystem.forecastProfile.profilesToForecast[i], i);
            }
        }
#endif

        public int[] GetWeatherIDs()
        {

            List<int> i = new List<int>();

            foreach (WeatherRelation j in weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles)
            {
                i.Add(weatherHashes[j.profile]);
            }

            return i.ToArray();

        }

        public float[] GetWeatherIntensities()
        {

            List<float> i = new List<float>();

            foreach (WeatherRelation j in weatherSphere.weatherModule.ecosystem.weightedWeatherProfiles)
            {
                i.Add(j.weight);
            }

            return i.ToArray();

        }

        public int[] PollValues(string key)
        {

            char comma = ",".ToCharArray()[0];
            string[] valuesInStringFormat = key.Split(comma);
            List<int> i = new List<int>();

            foreach (string k in valuesInStringFormat)
            {

                if (k == "")
                    continue;

                string trimmed = k.TrimEnd(comma);
                int parsed = int.Parse(trimmed);
                i.Add(parsed);
            }

            return i.ToArray();

        }

        public float[] PollWeightValues(string key)
        {

            char comma = ",".ToCharArray()[0];
            string[] valuesInStringFormat = key.Split(comma);
            List<float> i = new List<float>();

            foreach (string k in valuesInStringFormat)
            {
                if (k == "")
                    continue;
                i.Add(float.Parse(k.TrimEnd(comma)));
            }

            return i.ToArray();

        }

    }

}
