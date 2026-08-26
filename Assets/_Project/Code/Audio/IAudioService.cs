using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Central audio access for the rest of the game. UI and gameplay raise sounds through this
    /// so volume/mixer routing lives in one place. Volumes are linear 0..1.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>The last applied linear volumes — what a settings slider renders on open.</summary>
        float MasterVolume { get; }
        float MusicVolume { get; }
        float SfxVolume { get; }

        void PlayMusic(AudioClip clip, bool loop = true);
        void StopMusic();
        void PlaySfx(AudioClip clip, float volumeScale = 1f);
        void SetVolumes(float master, float music, float sfx);
    }
}
