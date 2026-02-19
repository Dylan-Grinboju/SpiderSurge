using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        private readonly Dictionary<string, AudioClip> _loadedClips = new Dictionary<string, AudioClip>();
        private AudioSource _audioSource;

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
            LoadEmbeddedSounds();
        }

        private void LoadEmbeddedSounds()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string[] resourceNames = assembly.GetManifestResourceNames();

            foreach (string resourceName in resourceNames)
            {
                if (resourceName.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase))
                {
                    string soundName = ExtractSoundName(resourceName);

                    try
                    {
                        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (stream != null)
                            {
                                byte[] wavData = new byte[stream.Length];
                                int totalRead = 0;
                                while (totalRead < wavData.Length)
                                {
                                    int bytesRead = stream.Read(wavData, totalRead, wavData.Length - totalRead);
                                    if (bytesRead == 0)
                                    {
                                        Logger.LogWarning($"[SoundManager] Unexpected EOF while reading resource: {resourceName}. Expected {wavData.Length} bytes, got {totalRead}.");
                                        break;
                                    }
                                    totalRead += bytesRead;
                                }

                                AudioClip clip = ParseWavFile(wavData, soundName);
                                if (clip != null)
                                {
                                    _loadedClips[soundName] = clip;
                                }
                            }
                            else
                            {
                                Logger.LogWarning($"[SoundManager] Could not load resource stream: {resourceName}");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError($"[SoundManager] Failed to load embedded sound '{resourceName}': {ex.Message}");
                    }
                }
            }
        }

        private string ExtractSoundName(string resourceName)
        {
            string withoutExtension = resourceName.Substring(0, resourceName.Length - 4);

            int lastDot = withoutExtension.LastIndexOf('.');
            if (lastDot >= 0)
            {
                return withoutExtension.Substring(lastDot + 1);
            }

            return withoutExtension;
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

                    if (audioFormat != 1 && audioFormat != 65534)
                    {
                        Logger.LogWarning($"[SoundManager] Unsupported WAV format {audioFormat}. File: {clipName}");
                        return null;
                    }
                }
                else if (chunkId == "data")
                {
                    int dataStart = pos + 8;
                    int dataSize = System.Math.Min(chunkSize, wavData.Length - dataStart);

                    if (bitsPerSample == 16)
                    {
                        int sampleCount = dataSize / 2;
                        samples = new float[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                        {
                            short sample = System.BitConverter.ToInt16(wavData, dataStart + i * 2);
                            samples[i] = sample / 32768f;
                        }
                    }
                    else if (bitsPerSample == 8)
                    {
                        int sampleCount = dataSize;
                        samples = new float[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                        {
                            samples[i] = (wavData[dataStart + i] - 128) / 128f;
                        }
                    }
                    else if (bitsPerSample == 24)
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
                    else
                    {
                        Logger.LogWarning($"[SoundManager] Unsupported bit depth: {bitsPerSample}. File: {clipName}");
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

        public void PlaySound(string soundName, float volume = 1f)
        {
            if (_loadedClips.TryGetValue(soundName, out AudioClip clip))
            {
                float sfxVolume = 1f;
                try
                {
                    sfxVolume = GameSettings.GetFloat("SFX VOL", 1f);
                }
                catch
                {
                    // Fallback if GameSettings is not ready
                }

                if (sfxVolume <= 0f) return;

                _audioSource.PlayOneShot(clip, volume * sfxVolume);
            }
            else
            {
                Logger.LogWarning($"[SoundManager] Sound not found: {soundName}. Available sounds: {string.Join(", ", _loadedClips.Keys)}");
            }
        }

        public void StopAll()
        {
            if (_audioSource != null)
            {
                _audioSource.Stop();
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
