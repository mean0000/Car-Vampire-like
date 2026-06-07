using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Jorjouto.AnimComposerSystem.Sample
{
    /// <summary>
    /// Represents an action block that plays an audio clip when executed.
    /// Supports both one-shot and looping playback using Unity's <see cref="AudioSource"/>.
    /// </summary>
    [BlockColor("#004b00ff")]
    [System.Serializable]
    public class ActionBlock_PlaySound : ActionBlock_Base
    {
        #region Fields

        /// <summary>
        /// The list of audio clips that can be played. One will be picked at random.
        /// </summary>
        [Tooltip("The list of audio clips that can be played. One will be picked at random.")]
        public AudioClip[] SoundsToPlay;

        /// <summary>
        /// The volume of the sound when played (range: 0.0f to 1.0f).
        /// </summary>
        [Tooltip("The volume of the sound when played (range: 0.0f to 1.0f).")]
        [Range(0.0f, 1.0f)]
        public float SoundVolume = 1.0f;

        /// <summary>
        /// Determines whether the sound should loop continuously.
        /// </summary>
        [Tooltip("Determines whether the sound should loop continuously.")]
        public bool IsLooped = false;

        #endregion

        #region Properties

        /// <summary>
        /// The AudioSource used for playing looped sounds.
        /// </summary>
        private AudioSource audioSource = null;

        #endregion

        #region Standard Functions

        /// <summary>
        /// Called when the action block starts execution.
        /// </summary>
        /// <param name="owner">The GameObject that owns this action block.</param>
        /// <param name="startTime">The start time of the action.</param>
        /// <param name="endTime">The end time of the action.</param>
        /// <param name="rate">The rate of execution.</param>
        public override void OnStart(GameObject owner, float startTime, float endTime, float rate)
        {
            base.OnStart(owner, startTime, endTime, rate);

            if (SoundsToPlay == null || SoundsToPlay.Length == 0 || owner == null)
                return;

            AudioClip soundToPlay = SoundsToPlay[Random.Range(0, SoundsToPlay.Length)];

            if (IsLooped)
            {
                // Create or reuse an AudioSource for looping
                audioSource = owner.GetComponent<AudioSource>();
                audioSource ??= owner.AddComponent<AudioSource>();

                audioSource.clip = soundToPlay;
                audioSource.loop = true;
                audioSource.volume = SoundVolume;
                audioSource.Play();
            }
            else
            {
                // Play one-shot sound at owner's position
                AudioSource.PlayClipAtPoint(soundToPlay, owner.transform.position, SoundVolume);
            }
        }

        /// <summary>
        /// Called when the action block exits execution.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();

            if (IsLooped && audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
                audioSource.clip = null;
            }
        }

        /// <summary>
        /// Checks if the action block can start execution.
        /// </summary>
        /// <returns>True if sounds are available to play, false otherwise.</returns>
        public override bool CheckCanStartActionBlock()
        {
            // Sound to play should not be null
            return SoundsToPlay != null && SoundsToPlay.Length > 0;
        }

        #endregion

        #region Debug

        #if UNITY_EDITOR

        /// <summary>
        /// Called when starting the action block in debug mode.
        /// </summary>
        /// <param name="previewRenderUtility">The preview render utility used for debug visualization.</param>
        /// <param name="previewObject">The preview GameObject used in debugging.</param>
        /// <param name="debugAudioSource">The AudioSource used during debug playback.</param>
        /// <param name="startTime">The debug start time.</param>
        /// <param name="endTime">The debug end time.</param>
        /// <param name="rate">The debug playback rate.</param>
        public override void OnDebugStart(PreviewRenderUtility previewRenderUtility,
                                        GameObject previewObject,
                                        AudioSource debugAudioSource,
                                        float startTime,
                                        float endTime,
                                        float rate)
        {
            base.OnDebugStart(previewRenderUtility, previewObject, debugAudioSource, startTime, endTime, rate);

            if (SoundsToPlay == null || SoundsToPlay.Count() == 0 || debugAudioSource == null)
            {
                return;
            }

            debugAudioSource.loop = IsLooped;
            AudioClip soundToPlay = SoundsToPlay[Random.Range(0, SoundsToPlay.Length)];
            debugAudioSource.clip = soundToPlay;
            debugAudioSource.volume = SoundVolume;
            debugAudioSource.Play();
        }

        /// <summary>
        /// Called when starting the action block in debug mode.
        /// </summary>
        /// <param name="previewRenderUtility">The preview render utility used for debug visualization.</param>
        /// <param name="previewObject">The preview GameObject used in debugging.</param>
        /// <param name="debugAudioSource">The AudioSource used during debug playback.</param>
        /// <param name="startTime">The debug start time.</param>
        /// <param name="endTime">The debug end time.</param>
        /// <param name="rate">The debug playback rate.</param>
        public override void OnDebugExit()
        {
            base.OnDebugExit();

            if (SoundsToPlay == null ||debugAudioSource == null || !IsLooped)
            {
                return;
            }

            debugAudioSource.Stop();
        }

        #endif

        #endregion
    }
}
