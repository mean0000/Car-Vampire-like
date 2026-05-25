using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using DistantLands.Cozy.Data;
using UnityEngine.Audio;
using System.Linq;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    public class ReSoundModule : CozyBiomeModuleBase<ReSoundModule>
    {

        public Transform resoundParent;
        public ReSoundDJ DJ;
        public ReSoundSetlist setlist;
        public Dictionary<ReSoundTrack, MixerChannel> localChannelMixerState = new Dictionary<ReSoundTrack, MixerChannel>();
        public Dictionary<ReSoundTrack, AudioSource> channelMixerOutput = new Dictionary<ReSoundTrack, AudioSource>();
        public ReSoundModule Root => (ReSoundModule)parentModule;

        [System.Serializable]
        public class MixerChannel
        {
            public float volume;
            public bool transitioning;
            public IEnumerator TransitionVolume(float target, float time)
            {
                float timer = time;
                transitioning = true;
                float startingVolume = volume;
                while (timer > 0)
                {
                    timer -= Time.deltaTime;
                    volume = Mathf.Lerp(startingVolume, target, 1 - (timer / time));
                    yield return new WaitForEndOfFrame();
                }
                transitioning = false;
                volume = target;
            }

            public MixerChannel()
            {
                volume = 0;
                transitioning = false;
            }
            public MixerChannel(float _volume, bool _transitioning)
            {
                volume = _volume;
                transitioning = _transitioning;
            }
        }

        //PLAYBACK
        public float songTimer;
        public bool paused;
        public AudioMixerGroup mixerGroup;
        public ReSoundTrack currentTrack;
        public float masterVolume = 1;


        //FX
        public Dictionary<ReSoundFX, float> fXes = new Dictionary<ReSoundFX, float>();
        internal float fxWeight;


        public override void InitializeModule()
        {
            base.InitializeModule();

            if (!Application.isPlaying)
                return;

            isBiomeModule = GetComponent<CozyBiome>();

            SetupMixerChannels();

            if (isBiomeModule)
            {
                AddBiome();
                return;
            }

            fXes = new Dictionary<ReSoundFX, float>();
            parentModule = this;
            masterVolume = 1;

            PlayFromBeginning();
        }

        /// <summary>
        /// Sets up the global mixer state with a channel for every song and adds instances for the audio sources in the scene.
        /// </summary>
        public void SetupMixerChannels()
        {

            if (isBiomeModule)
            {
                if (parentModule == null)
                    parentModule = weatherSphere.GetModule<ReSoundModule>();

                foreach (ReSoundTrack track in Root.DJ.availableTracks)
                {
                    if (!localChannelMixerState.Keys.Contains(track))
                        localChannelMixerState.Add(track, new MixerChannel());
                }
                return;
            }

            SetupParent();

            foreach (ReSoundTrack track in DJ.availableTracks)
            {

                GameObject songObject = new GameObject();
                songObject.transform.parent = resoundParent;
                songObject.name = track.name;

                AudioSource source = songObject.AddComponent<AudioSource>();
                source.volume = 0;
                source.pitch = 1f;
                source.clip = track.clip;
                source.outputAudioMixerGroup = mixerGroup;
                source.playOnAwake = true;
                source.time = 0;
                source.Play();

                if (!localChannelMixerState.Keys.Contains(track))
                    localChannelMixerState.Add(track, new MixerChannel());
                if (!channelMixerOutput.Keys.Contains(track))
                    channelMixerOutput.Add(track, source);

            }
        }

        void SetupParent()
        {
            if (resoundParent == null)
            {
                foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t.name == "ReSound Parent")
                    {
                        resoundParent = t;
                        return;
                    }
                }

                resoundParent = new GameObject().transform;
                resoundParent.name = "ReSound Parent";
            }
        }

        /// <summary>
        /// Update is called every frame, if the MonoBehaviour is enabled.
        /// </summary>
        void Update()
        {

            if (!Application.isPlaying)
                return;

            if (!paused)
                UpdateLocalMixer();

            if (isBiomeModule)
                return;

            ComputeBiomeWeights();
            UpdateGlobalMixer();

            if (!channelMixerOutput[currentTrack].isPlaying && DJ.noSilenceMode)
                channelMixerOutput[currentTrack].Play();

        }


        public void UpdateLocalMixer()
        {

            if (weight == 0)
            {
                return;
            }

            if (songTimer <= (Root.DJ.transitionType != ReSoundDJ.TransitionType.noFade ? Root.DJ.transitionTime : 0))
                StopTrack(currentTrack);

            if (songTimer <= (Root.DJ.transitionType == ReSoundDJ.TransitionType.crossfade ? Root.DJ.transitionTime : 0))
                PlayTrack(RandomTrack());

            // songTimer = currentTrack.clip.length - parentModule.channelMixerOutput[currentTrack].time;
            songTimer -= Time.deltaTime;
            // float totalVolume = 0;

            foreach (var channel in channelMixerOutput)
            {
                // if (totalVolume >= 1)
                // {
                //     channel.Value.volume = 0;
                //     continue;
                // }
                channel.Value.volume = localChannelMixerState[channel.Key].volume;
                // totalVolume += channel.Value.volume;
            }
        }

        public void UpdateGlobalMixer()
        {

            fxWeight = Mathf.Clamp01(fxWeight);

            foreach (var channel in localChannelMixerState)
            {
                channelMixerOutput[channel.Key].volume = localChannelMixerState[channel.Key].volume * channel.Key.volume * weight * masterVolume;
            }

            // foreach (var fx in fXes)
            // {
            //     channelMixerOutput[fx.Key.track].volume += fx.Value * weight;
            // }


            foreach (var biome in biomes)
            {

                foreach (var pair in ((ReSoundModule)biome).localChannelMixerState)
                {
                    var track = pair.Key;
                    var channel = pair.Value;
                    var output = channelMixerOutput[track];

                    output.volume += biome.weight * pair.Key.volume * channel.volume * masterVolume;

                }
            }

            if (DJ.resetOnEntry)
                foreach (var channel in channelMixerOutput)
                {
                    if (channel.Value.volume == 0)
                    {
                        AudioClip clip = channel.Key.clipType == ReSoundTrack.ClipType.singleClip ? channel.Key.clip : channel.Key.playlist[Random.Range(0, channel.Key.playlist.Length - 1)];
                        channel.Value.clip = clip;
                        channel.Value.Play();
                        channel.Value.time = 0;
                    }
                }
        }

        ReSoundTrack RandomTrack()
        {
            ReSoundTrack randomTrack = null;
            List<float> chancePerTrack = new List<float>();
            float totalChance = 0;

            foreach (ReSoundTrack k in setlist.availableTracks)
            {
                if (k == currentTrack && DJ.preventRepeatSongs)
                {
                    chancePerTrack.Add(0);
                    totalChance += 0;
                    continue;
                }

                float chance = k.GetChance(weatherSphere, 0);
                chancePerTrack.Add(chance);
                totalChance += chance;
            }

            float selection = Random.Range(0, totalChance);

            int iterator = 0;
            float iteratedChance = 0;

            while (iteratedChance <= selection)
            {
                if (iterator >= chancePerTrack.Count)
                {
                    randomTrack = setlist.availableTracks[setlist.availableTracks.Count - 1];
                    break;
                }

                if (selection >= iteratedChance && selection < iteratedChance + chancePerTrack[iterator])
                {
                    randomTrack = setlist.availableTracks[iterator];
                    break;
                }
                iteratedChance += chancePerTrack[iterator];
                iterator++;

            }

            if (!randomTrack)
            {
                randomTrack = setlist.availableTracks[0];
            }

            return randomTrack;
        }

        #region Biome Controls


        #endregion


        #region Disc Controls

        public void PlayTrack(ReSoundTrack track)
        {
            Play();
            if (!localChannelMixerState.ContainsKey(track))
                localChannelMixerState.Add(track, new MixerChannel(0, true));

            currentTrack = track;

            songTimer = Root.channelMixerOutput[track].clip.length + (DJ.noSilenceMode ? 0 : Random.Range(setlist.minSilenceTime, setlist.maxSilenceTime));
            if (Root.DJ.transitionType == ReSoundDJ.TransitionType.noFade)
            {
                if (!isBiomeModule)
                {
                    localChannelMixerState[track].volume = 1;
                }
            }
            else
            {
                StartCoroutine(localChannelMixerState[track].TransitionVolume(1, Root.DJ.transitionTime));
            }
        }

        public void StopTrack(ReSoundTrack track)
        {
            if (track == null)
                return;

            if (Root.DJ.transitionType == ReSoundDJ.TransitionType.noFade)
            {
                localChannelMixerState[track].volume = 0;
            }
            else
                StartCoroutine(localChannelMixerState[track].TransitionVolume(0, Root.DJ.transitionTime));
        }

        public void Skip()
        {
            songTimer = 0;
        }

        public void Pause()
        {
            paused = true;
            foreach (var output in channelMixerOutput)
            {
                output.Value.Pause();
            }
        }

        public void Play()
        {
            paused = false;
            foreach (var output in channelMixerOutput)
            {
                output.Value.UnPause();
            }
        }

        public void Shuffle()
        {
            Play();
            PlayTrack(RandomTrack());
        }

        public void PlayFromBeginning()
        {
            if (setlist.initialSong && setlist.startingStyle == ReSoundSetlist.StartingStyle.startWithInitialSong)
            {
                PlayTrack(setlist.initialSong);
            }
            else
            {
                PlayTrack(RandomTrack());
            }
        }

        public IEnumerator FreezeForTime(float freezeTime)
        {

            Pause();

            yield return new WaitForSeconds(freezeTime);

            Play();
        }

        public IEnumerator FadeToVolume(float fadeTime, float targetVolume)
        {

            float currentVolume = masterVolume;

            for (float i = 0; i < fadeTime; i += Time.deltaTime)
            {

                masterVolume = Mathf.Lerp(currentVolume, targetVolume, i);
                yield return new WaitForEndOfFrame();

            }

            masterVolume = targetVolume;

        }

        public IEnumerator FadeOutFadeIn(float fadeTime, float waitTime)
        {

            float currentVolume = masterVolume;

            for (float i = 0; i < fadeTime; i += Time.deltaTime)
            {

                masterVolume = Mathf.Lerp(currentVolume, 0, i);
                yield return new WaitForEndOfFrame();

            }
            masterVolume = 0;

            yield return new WaitForSeconds(waitTime);

            for (float i = 0; i < fadeTime; i += Time.deltaTime)
            {

                masterVolume = Mathf.Lerp(0, currentVolume, i);
                yield return new WaitForEndOfFrame();

            }

            masterVolume = currentVolume;

        }

        public void RunFreezeForTime(float freezeTime)
        {
            StartCoroutine(FreezeForTime(freezeTime));
        }
        public void RunFadeToVolume(float targetVolume)
        {
            StartCoroutine(FadeToVolume(1, targetVolume));
        }
        public void RunFadeOutFadeIn(float waitTime)
        {
            StartCoroutine(FadeOutFadeIn(1f, waitTime));
        }

        #endregion

        #region FX Controls


        public override void FrameReset()
        {

            List<KeyValuePair<ReSoundFX, float>> list = fXes.ToList();

            for (int i = 0; i < fXes.Count; i++)
            {
                var pair = list[i];
                fXes[pair.Key] = 0;
            }
        }


        #endregion

    }
}