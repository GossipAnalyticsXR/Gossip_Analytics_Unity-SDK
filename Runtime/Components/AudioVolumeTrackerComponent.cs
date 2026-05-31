using System;
using UnityEngine;
using UnityEngine.Audio;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class AudioVolumeTrackerComponent : MonoBehaviour
    {
        public AudioMixer audioMixer;
        public string masterParam = "MasterVolume";
        public string musicParam = "MusicVolume";
        public string sfxParam = "SfxVolume";
        public float dbMin = -80f;
        public float dbMax = 0f;

        [Header("Behaviour")]
        public bool autoReportOnStart = true;
        public bool reportOnChange = true;
        public float changeThreshold = 0.05f;

        private float lastMaster, lastMusic, lastSfx;

        private void Start()
        {
            if (autoReportOnStart)
                Report("Init");
        }

        private void Update()
        {
            if (!reportOnChange) return;

            ReadVolumes(out float m, out float mu, out float s);

            if (Mathf.Abs(m - lastMaster) > changeThreshold ||
                Mathf.Abs(mu - lastMusic) > changeThreshold ||
                Mathf.Abs(s - lastSfx) > changeThreshold)
            {
                Report("Change");
            }
        }

        public void Report(string source)
        {
            try
            {
                var tracker = Gossip.Instance?.AudioVolumeTracker;
                if (tracker == null) return;

                ReadVolumes(out float master, out float music, out float sfx);

                lastMaster = master;
                lastMusic = music;
                lastSfx = sfx;

                var data = new AudioVolumeTracker.EntityData
                {
                    MasterVolume = master,
                    MusicVolume = music,
                    SfxVolume = sfx,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    Source = source,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                tracker.CapSession(data);

                if (Gossip.Instance.Settings?.EnableDebug == true)
                {
                    Debug.Log(
                        $"[AudioVolume] Master={master:F2} Music={music:F2} SFX={sfx:F2} ({source})"
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void ReadVolumes(out float master, out float music, out float sfx)
        {
            if (audioMixer == null)
            {
                master = AudioListener.volume;
                music = AudioListener.volume;
                sfx = AudioListener.volume;
                return;
            }

            master = GetMixerVolume(masterParam);
            music = GetMixerVolume(musicParam);
            sfx = GetMixerVolume(sfxParam);
        }

        private float GetMixerVolume(string param)
        {
            if (audioMixer.GetFloat(param, out float db))
            {
                return Mathf.InverseLerp(dbMin, dbMax, db);
            }
            return 1f;
        }
    }
}
