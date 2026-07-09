using UnityEngine;

namespace Sol.Arcade
{
    /// <summary>Persisted player options shared by the main and pause menus.</summary>
    public static class ArcadeOptions
    {
        private const string MasterVolumePlayerPrefsKey = "Options.MasterVolume";

        public static float MasterVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePlayerPrefsKey, 1f));

        public static void ApplyToListener()
        {
            AudioListener.volume = MasterVolume;
        }

        /// <summary>Steps master volume down in 10% increments, wrapping 0 back to 100%.</summary>
        public static float CycleMasterVolume()
        {
            int percent = Mathf.RoundToInt(MasterVolume * 100f) - 10;
            if (percent < 0)
            {
                percent = 100;
            }

            float volume = percent / 100f;
            PlayerPrefs.SetFloat(MasterVolumePlayerPrefsKey, volume);
            PlayerPrefs.Save();
            return volume;
        }
    }
}
