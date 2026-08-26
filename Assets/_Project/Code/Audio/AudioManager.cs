using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>
    /// MonoBehaviour audio hub. Routes music and SFX through an <see cref="AudioMixer"/> whose
    /// exposed "MasterVolume"/"MusicVolume"/"SfxVolume" parameters are set in dB from linear values.
    /// Register the instance with the app's service locator in Bootstrap.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour, IAudioService
    {
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Exposed mixer parameter names")]
        [SerializeField] private string masterParam = "MasterVolume";
        [SerializeField] private string musicParam = "MusicVolume";
        [SerializeField] private string sfxParam = "SfxVolume";

        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;

        private void Reset()
        {
            // Auto-create sources if added via AddComponent.
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (musicSource == null || clip == null) return;
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.Play();
        }

        public void StopMusic()
        {
            if (musicSource != null) musicSource.Stop();
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (sfxSource == null || clip == null) return;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        public void SetVolumes(float master, float music, float sfx)
        {
            MasterVolume = Mathf.Clamp01(master);
            MusicVolume = Mathf.Clamp01(music);
            SfxVolume = Mathf.Clamp01(sfx);

            if (mixer != null)
            {
                SetMixer(masterParam, MasterVolume);
                SetMixer(musicParam, MusicVolume);
                SetMixer(sfxParam, SfxVolume);
            }
            else
            {
                // No mixer wired (the scaffolded Boot scene adds this component bare) — fold
                // master into each source directly so volume settings still do something.
                // PlayOneShot scales by the source volume, so SFX are covered too.
                if (musicSource != null) musicSource.volume = MasterVolume * MusicVolume;
                if (sfxSource != null) sfxSource.volume = MasterVolume * SfxVolume;
            }
        }

        private void SetMixer(string param, float linear)
        {
            if (string.IsNullOrEmpty(param)) return;
            // Convert linear 0..1 to decibels; -80 dB is effectively silent.
            float dB = linear <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp01(linear)) * 20f;
            mixer.SetFloat(param, dB);
        }
    }
}
