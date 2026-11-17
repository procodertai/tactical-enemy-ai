using UnityEngine;

namespace TacticalGame
{
    // Small synthesized effects keep the starter project self contained and asset-free.
    public sealed class GameAudio : MonoBehaviour
    {
        public AudioClip templateRifle;
        public AudioClip templateFootstep;
        public static GameAudio Instance { get; private set; }
        AudioSource source;
        AudioClip shot;
        AudioClip step;
        AudioClip[] cues;

        void Awake()
        {
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.volume = .3f;
            shot = Make("Rifle report", .14f, 95f, true, 17);
            step = Make("Boot step", .065f, 60f, true, 31);
            cues = new AudioClip[12];
        }

        static AudioClip Make(string name, float seconds, float hz, bool noise, int seed)
        {
            const int sampleRate = 22050;
            var data = new float[Mathf.CeilToInt(sampleRate * seconds)];
            var random = new System.Random(seed);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Pow(1f - i / (float)data.Length, noise ? 3f : 1.5f);
                float tone = Mathf.Sin(2f * Mathf.PI * hz * t);
                data[i] = (noise ? ((float)random.NextDouble() * 2f - 1f) * .6f + tone * .4f : tone * .55f) * envelope;
            }
            var clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Gunshot() => source.PlayOneShot(templateRifle != null ? templateRifle : shot, .8f);
        public void EnemyShot() => source.PlayOneShot(templateRifle != null ? templateRifle : shot, .24f);
        public void Footstep() => source.PlayOneShot(templateFootstep != null ? templateFootstep : step, .2f);

        public void PlayCue(float frequency, float duration)
        {
            // Cache a bounded number of tones; repeated actions do not allocate audio clips.
            int index = Mathf.Clamp(Mathf.RoundToInt(frequency / 80f), 0, cues.Length - 1);
            if (cues[index] == null) cues[index] = Make("Interface tone " + index, duration, frequency, false, 0);
            source.PlayOneShot(cues[index], .35f);
        }

        void OnDestroy()
        {
            if (shot != null) Destroy(shot);
            if (step != null) Destroy(step);
            if (cues != null) foreach (var clip in cues) if (clip != null) Destroy(clip);
            if (Instance == this) Instance = null;
        }
    }
}
