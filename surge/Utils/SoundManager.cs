using System;
using UnityEngine;
using System.Collections.Generic;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }
        private static readonly Guid PcmSubFormatGuid = new Guid("00000001-0000-0010-8000-00AA00389B71");
        private static readonly Guid IeeeFloatSubFormatGuid = new Guid("00000003-0000-0010-8000-00AA00389B71");

        private readonly Dictionary<string, AudioClip> _loadedClips = new Dictionary<string, AudioClip>();
        private AudioSource _audioSource;
        private AudioSource _feedbackSource;
        private AudioSource _notificationSource;
        private readonly Queue<FeedbackCue> _notifications = new Queue<FeedbackCue>();
        private readonly Dictionary<FeedbackCue, float> _lastFeedback = new Dictionary<FeedbackCue, float>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _feedbackSource = gameObject.AddComponent<AudioSource>();
            _notificationSource = gameObject.AddComponent<AudioSource>();
            _feedbackSource.playOnAwake = _notificationSource.playOnAwake = false;
            _feedbackSource.spatialBlend = _notificationSource.spatialBlend = 0f;
            LoadEmbeddedSounds();
        }

        internal void PlayFeedback(FeedbackCue cue)
        {
            if (ModConfig.AbilityVolume <= 0f || SfxVolume <= 0f) return;
            if (_lastFeedback.TryGetValue(cue, out float last) && Time.unscaledTime - last < .1f) return;
            _lastFeedback[cue] = Time.unscaledTime;
            if (FeedbackSounds.Clip(cue).queued)
            {
                if (_notifications.Count < 8 && !_notifications.Contains(cue)) _notifications.Enqueue(cue);
            }
            else PlayFeedbackClip(cue, _feedbackSource);
        }

        private void Update()
        {
            if (!_notificationSource.isPlaying && _notifications.Count > 0)
                PlayFeedbackClip(_notifications.Dequeue(), _notificationSource);
        }

        internal void CancelFeedback(FeedbackCue cue)
        {
            int pending = _notifications.Count;
            for (int i = 0; i < pending; i++)
            {
                var next = _notifications.Dequeue();
                if (next != cue) _notifications.Enqueue(next);
            }
            if (_notificationSource.clip != null && _notificationSource.clip.name == FeedbackSounds.Clip(cue).name)
                _notificationSource.Stop();
        }

        private static float SfxVolume
        {
            get
            {
                try { return GameSettings.GetFloat("SFX VOL", 1f); }
                catch { return 1f; }
            }
        }

        private void PlayFeedbackClip(FeedbackCue cue, AudioSource source)
        {
            string name = FeedbackSounds.Clip(cue).name;
            if (!_loadedClips.TryGetValue(name, out var clip))
            {
                Logger.LogWarning($"[SoundManager] Feedback sound not found: {name}");
                return;
            }
            source.Stop();
            source.clip = clip;
            source.PlayOneShot(clip, ModConfig.AbilityVolume * SfxVolume);
        }

        private void LoadEmbeddedSounds()
        {
            EmbeddedResources.Load(".wav", "SoundManager", "sound", nameof(ParseWavFile), (soundName, wavData) =>
            {
                AudioClip clip = ParseWavFile(wavData, soundName);
                if (clip != null)
                {
                    _loadedClips[soundName] = clip;
                }
            });
        }

        private AudioClip ParseWavFile(byte[] wavData, string clipName)
        {
            if (wavData.Length < 44)
            {
                Logger.LogWarning($"[SoundManager] WAV file too small: {clipName}");
                return null;
            }

            int pos = 12;

            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            bool decodeAsFloat = false;
            bool formatValidated = false;
            int bytesPerSample = 0;
            float[] samples = null;

            while (pos < wavData.Length - 8)
            {
                string chunkId = System.Text.Encoding.ASCII.GetString(wavData, pos, 4);
                int chunkSize = System.BitConverter.ToInt32(wavData, pos + 4);

                // Validate chunkSize to prevent infinite loops or out-of-bounds access
                if (chunkSize < 0 || chunkSize > wavData.Length - (pos + 8))
                {
                    Logger.LogWarning($"[SoundManager] Invalid chunk size {chunkSize} at position {pos} in file: {clipName}");
                    return null;
                }

                if (chunkId == "fmt ")
                {
                    // Format chunk
                    ushort audioFormat = System.BitConverter.ToUInt16(wavData, pos + 8);
                    channels = System.BitConverter.ToInt16(wavData, pos + 10);
                    sampleRate = System.BitConverter.ToInt32(wavData, pos + 12);
                    bitsPerSample = System.BitConverter.ToInt16(wavData, pos + 22);
                    bytesPerSample = bitsPerSample / 8;

                    if (bitsPerSample <= 0 || bitsPerSample % 8 != 0)
                    {
                        Logger.LogWarning($"[SoundManager] Invalid bit depth: {bitsPerSample}. File: {clipName}");
                        return null;
                    }

                    if (audioFormat == 1)
                    {
                        formatValidated = true;
                        decodeAsFloat = false;
                    }
                    else if (audioFormat == 3)
                    {
                        formatValidated = true;
                        decodeAsFloat = true;
                    }
                    else if (audioFormat == 65534)
                    {
                        if (chunkSize < 40)
                        {
                            Logger.LogWarning($"[SoundManager] Invalid extensible fmt chunk size {chunkSize}. File: {clipName}");
                            return null;
                        }

                        byte[] subFormatBytes = new byte[16];
                        System.Buffer.BlockCopy(wavData, pos + 32, subFormatBytes, 0, 16);
                        Guid subFormatGuid = new Guid(subFormatBytes);

                        if (subFormatGuid == PcmSubFormatGuid)
                        {
                            formatValidated = true;
                            decodeAsFloat = false;
                        }
                        else if (subFormatGuid == IeeeFloatSubFormatGuid)
                        {
                            formatValidated = true;
                            decodeAsFloat = true;
                        }
                        else
                        {
                            Logger.LogWarning($"[SoundManager] Unsupported WAV extensible SubFormat GUID {subFormatGuid}. File: {clipName}");
                            return null;
                        }
                    }
                    else
                    {
                        Logger.LogWarning($"[SoundManager] Unsupported WAV format {audioFormat}. File: {clipName}");
                        return null;
                    }
                }
                else if (chunkId == "data")
                {
                    if (!formatValidated)
                    {
                        Logger.LogWarning($"[SoundManager] WAV data chunk encountered before valid fmt chunk: {clipName}");
                        return null;
                    }

                    int dataStart = pos + 8;
                    int dataSize = System.Math.Min(chunkSize, wavData.Length - dataStart);

                    if (!decodeAsFloat && bitsPerSample == 16)
                    {
                        int sampleCount = dataSize / 2;
                        samples = new float[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                        {
                            short sample = System.BitConverter.ToInt16(wavData, dataStart + i * 2);
                            samples[i] = sample / 32768f;
                        }
                    }
                    else if (!decodeAsFloat && bitsPerSample == 8)
                    {
                        int sampleCount = dataSize;
                        samples = new float[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                        {
                            samples[i] = (wavData[dataStart + i] - 128) / 128f;
                        }
                    }
                    else if (!decodeAsFloat && bitsPerSample == 24)
                    {
                        int sampleCount = dataSize / 3;
                        samples = new float[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                        {
                            int offset = dataStart + i * 3;
                            int sample = wavData[offset] | (wavData[offset + 1] << 8) | (wavData[offset + 2] << 16);
                            if ((sample & 0x800000) != 0)
                                sample |= unchecked((int)0xFF000000);
                            samples[i] = sample / 8388608f;
                        }
                    }
                    else if (decodeAsFloat && bitsPerSample == 32)
                    {
                        int sampleCount = dataSize / bytesPerSample;
                        samples = new float[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                        {
                            samples[i] = System.BitConverter.ToSingle(wavData, dataStart + i * bytesPerSample);
                        }
                    }
                    else
                    {
                        string encoding = decodeAsFloat ? "float" : "integer PCM";
                        Logger.LogWarning($"[SoundManager] Unsupported {encoding} bit depth: {bitsPerSample}. File: {clipName}");
                        return null;
                    }
                }

                pos += chunkSize + 8;
                if (chunkSize % 2 != 0)
                    pos++;
            }

            if (samples == null || channels == 0 || sampleRate == 0)
            {
                Logger.LogWarning($"[SoundManager] Invalid WAV file structure: {clipName}");
                return null;
            }

            int sampleLength = samples.Length / channels;
            AudioClip clip = AudioClip.Create(clipName, sampleLength, channels, sampleRate, false);
            clip.SetData(samples, 0);

            return clip;
        }

        public static void Play(string soundName, float volume)
        {
            if (Instance != null) Instance.PlaySound(soundName, volume);
        }

        public void PlaySound(string soundName, float volume = 1f)
        {
            if (_loadedClips.TryGetValue(soundName, out AudioClip clip))
            {
                float abilityVolume = ModConfig.AbilityVolume;
                float sfxVolume = SfxVolume;
                if (sfxVolume <= 0f || abilityVolume <= 0f) return;

                _audioSource.PlayOneShot(clip, volume * sfxVolume * abilityVolume);
            }
            else
            {
                Logger.LogWarning($"[SoundManager] Sound not found: {soundName}. Available sounds: {string.Join(", ", _loadedClips.Keys)}");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
