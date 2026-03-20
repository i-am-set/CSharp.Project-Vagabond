using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using static ProjectVagabond.GameEvents;

namespace ProjectVagabond.Audio
{
    public class AudioManager
    {
        private float _masterVolume = 1.0f;
        private float _musicVolume = 1.0f;
        private float _sfxVolume = 1.0f;
        private float _ambientVolume = 1.0f;
        private float _uiVolume = 1.0f;

        public AudioManager()
        {
            EventBus.Subscribe<GameEvents.AlertPublished>(OnAlertPublished);
        }

        private void OnAlertPublished(GameEvents.AlertPublished e)
        {
            PlayUi("ui_alert");
        }

        private class PooledSound
        {
            public SoundEffectInstance[] Instances;
            public float BaseVolume;
            public float MinPitch;
            public float MaxPitch;
            public int CurrentIndex;
        }

        private class MusicTrack
        {
            public SoundEffectInstance[] Stems;
            public float[] TargetStemVolumes;
            public float[] CurrentStemVolumes;
            public float BaseVolume;
        }

        private class AmbientTrack
        {
            public SoundEffectInstance Instance;
            public float BaseVolume;
            public float TargetVolume;
            public float CurrentVolume;
        }

        private readonly Dictionary<string, PooledSound> _sfxPools = new Dictionary<string, PooledSound>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PooledSound> _uiPools = new Dictionary<string, PooledSound>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MusicTrack> _musicTracks = new Dictionary<string, MusicTrack>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AmbientTrack> _ambientTracks = new Dictionary<string, AmbientTrack>(StringComparer.OrdinalIgnoreCase);

        private MusicTrack _currentMusic;
        private MusicTrack _fadingMusic;
        private float _musicCrossfadeTimer;
        private float _musicCrossfadeDuration = 2.0f;
        private float _currentMusicMasterFade = 1.0f;
        private float _fadingMusicMasterFade = 0.0f;

        private readonly Random _random = new Random();

        public void SetVolumes(float master, float music, float sfx, float ambient, float ui)
        {
            _masterVolume = Math.Clamp(master, 0f, 1f);
            _musicVolume = Math.Clamp(music, 0f, 1f);
            _sfxVolume = Math.Clamp(sfx, 0f, 1f);
            _ambientVolume = Math.Clamp(ambient, 0f, 1f);
            _uiVolume = Math.Clamp(ui, 0f, 1f);

            UpdateActiveVolumes();
        }

        public void LoadContent(ContentManager content)
        {
            string manifestPath = Path.Combine(content.RootDirectory, "Data", "AudioManifest.json");
            if (!File.Exists(manifestPath))
            {
                GameLogger.Log(LogSeverity.Warning, "[AudioManager] AudioManifest.json not found. Audio will be disabled.");
                return;
            }

            try
            {
                string json = File.ReadAllText(manifestPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var manifest = JsonSerializer.Deserialize<AudioManifest>(json, options);

                if (manifest != null)
                {
                    LoadPool(content, manifest.Sfx, _sfxPools);
                    LoadPool(content, manifest.Ui, _uiPools);
                    LoadAmbient(content, manifest.Ambient);
                    LoadMusic(content, manifest.Music);
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log(LogSeverity.Error, $"[AudioManager] Failed to load audio manifest: {ex.Message}");
            }
        }

        private void LoadPool(ContentManager content, List<AudioEntry> entries, Dictionary<string, PooledSound> targetDict)
        {
            foreach (var entry in entries)
            {
                try
                {
                    var sfx = content.Load<SoundEffect>(entry.Path);
                    var instances = new SoundEffectInstance[entry.PoolSize];
                    for (int i = 0; i < entry.PoolSize; i++)
                    {
                        instances[i] = sfx.CreateInstance();
                    }

                    targetDict[entry.Id] = new PooledSound
                    {
                        Instances = instances,
                        BaseVolume = entry.DefaultVolume,
                        MinPitch = entry.MinPitch,
                        MaxPitch = entry.MaxPitch,
                        CurrentIndex = 0
                    };
                }
                catch (Exception ex)
                {
                    GameLogger.Log(LogSeverity.Warning, $"[AudioManager] Failed to load audio '{entry.Id}' at '{entry.Path}': {ex.Message}");
                }
            }
        }

        private void LoadAmbient(ContentManager content, List<AudioEntry> entries)
        {
            foreach (var entry in entries)
            {
                try
                {
                    var sfx = content.Load<SoundEffect>(entry.Path);
                    var instance = sfx.CreateInstance();
                    instance.IsLooped = true;

                    _ambientTracks[entry.Id] = new AmbientTrack
                    {
                        Instance = instance,
                        BaseVolume = entry.DefaultVolume,
                        TargetVolume = 0f,
                        CurrentVolume = 0f
                    };
                }
                catch (Exception ex)
                {
                    GameLogger.Log(LogSeverity.Warning, $"[AudioManager] Failed to load ambient '{entry.Id}' at '{entry.Path}': {ex.Message}");
                }
            }
        }

        private void LoadMusic(ContentManager content, List<MusicEntry> entries)
        {
            foreach (var entry in entries)
            {
                try
                {
                    var stems = new SoundEffectInstance[entry.StemPaths.Count];
                    var targetVols = new float[entry.StemPaths.Count];
                    var currentVols = new float[entry.StemPaths.Count];

                    for (int i = 0; i < entry.StemPaths.Count; i++)
                    {
                        var sfx = content.Load<SoundEffect>(entry.StemPaths[i]);
                        stems[i] = sfx.CreateInstance();
                        stems[i].IsLooped = true;
                        targetVols[i] = 0f;
                        currentVols[i] = 0f;
                    }

                    _musicTracks[entry.Id] = new MusicTrack
                    {
                        Stems = stems,
                        TargetStemVolumes = targetVols,
                        CurrentStemVolumes = currentVols,
                        BaseVolume = entry.DefaultVolume
                    };
                }
                catch (Exception ex)
                {
                    GameLogger.Log(LogSeverity.Warning, $"[AudioManager] Failed to load music '{entry.Id}': {ex.Message}");
                }
            }
        }

        public void PlaySfx(string id, float pitchVariance = 0f)
        {
            if (string.IsNullOrEmpty(id) || !_sfxPools.TryGetValue(id, out var pool)) return;

            var instance = GetAvailableInstance(pool);
            if (instance != null)
            {
                instance.Volume = pool.BaseVolume * _sfxVolume * _masterVolume;

                if (pool.MinPitch != 0f || pool.MaxPitch != 0f)
                {
                    instance.Pitch = pool.MinPitch + (float)(_random.NextDouble() * (pool.MaxPitch - pool.MinPitch));
                }
                else if (pitchVariance > 0f)
                {
                    instance.Pitch = (float)(_random.NextDouble() * 2.0 - 1.0) * pitchVariance;
                }
                else
                {
                    instance.Pitch = 0f;
                }

                instance.Play();
            }
        }

        public void PlayUi(string id, float pitchVariance = 0f)
        {
            if (string.IsNullOrEmpty(id) || !_uiPools.TryGetValue(id, out var pool)) return;

            var instance = GetAvailableInstance(pool);
            if (instance != null)
            {
                instance.Volume = pool.BaseVolume * _uiVolume * _masterVolume;

                if (pool.MinPitch != 0f || pool.MaxPitch != 0f)
                {
                    instance.Pitch = pool.MinPitch + (float)(_random.NextDouble() * (pool.MaxPitch - pool.MinPitch));
                }
                else if (pitchVariance > 0f)
                {
                    instance.Pitch = (float)(_random.NextDouble() * 2.0 - 1.0) * pitchVariance;
                }
                else
                {
                    instance.Pitch = 0f;
                }

                instance.Play();
            }
        }

        private SoundEffectInstance GetAvailableInstance(PooledSound pool)
        {
            for (int i = 0; i < pool.Instances.Length; i++)
            {
                int index = (pool.CurrentIndex + i) % pool.Instances.Length;
                if (pool.Instances[index].State == SoundState.Stopped)
                {
                    pool.CurrentIndex = (index + 1) % pool.Instances.Length;
                    return pool.Instances[index];
                }
            }

            int stealIndex = pool.CurrentIndex;
            pool.CurrentIndex = (pool.CurrentIndex + 1) % pool.Instances.Length;
            pool.Instances[stealIndex].Stop();
            return pool.Instances[stealIndex];
        }

        public void PlayMusic(string id, float crossfadeDuration = 2.0f)
        {
            if (string.IsNullOrEmpty(id) || !_musicTracks.TryGetValue(id, out var nextMusic)) return;
            if (_currentMusic == nextMusic) return;

            if (_currentMusic != null)
            {
                _fadingMusic = _currentMusic;
                _fadingMusicMasterFade = _currentMusicMasterFade;
            }

            _currentMusic = nextMusic;
            _currentMusicMasterFade = 0f;
            _musicCrossfadeDuration = crossfadeDuration > 0f ? crossfadeDuration : 0.01f;
            _musicCrossfadeTimer = 0f;

            for (int i = 0; i < _currentMusic.Stems.Length; i++)
            {
                if (_currentMusic.Stems[i].State != SoundState.Playing)
                {
                    _currentMusic.Stems[i].Play();
                }
            }
        }

        public void SetMusicStemVolume(string id, int stemIndex, float targetVolume)
        {
            if (_musicTracks.TryGetValue(id, out var track))
            {
                if (stemIndex >= 0 && stemIndex < track.TargetStemVolumes.Length)
                {
                    track.TargetStemVolumes[stemIndex] = Math.Clamp(targetVolume, 0f, 1f);
                }
            }
        }

        public void PlayAmbient(string id, float targetVolume = 1.0f)
        {
            if (_ambientTracks.TryGetValue(id, out var track))
            {
                track.TargetVolume = Math.Clamp(targetVolume, 0f, 1f);
                if (track.Instance.State != SoundState.Playing)
                {
                    track.Instance.Play();
                }
            }
        }

        public void StopAmbient(string id)
        {
            if (_ambientTracks.TryGetValue(id, out var track))
            {
                track.TargetVolume = 0f;
            }
        }

        public void Update(float dt)
        {
            if (_currentMusic != null)
            {
                if (_musicCrossfadeTimer < _musicCrossfadeDuration)
                {
                    _musicCrossfadeTimer += dt;
                    float progress = Math.Clamp(_musicCrossfadeTimer / _musicCrossfadeDuration, 0f, 1f);
                    _currentMusicMasterFade = MathHelper.Lerp(0f, 1f, progress);

                    if (_fadingMusic != null)
                    {
                        _fadingMusicMasterFade = MathHelper.Lerp(1f, 0f, progress);
                    }
                }
                else if (_fadingMusic != null)
                {
                    foreach (var stem in _fadingMusic.Stems)
                    {
                        stem.Stop();
                    }
                    _fadingMusic = null;
                }

                UpdateMusicTrack(_currentMusic, _currentMusicMasterFade, dt);
            }

            if (_fadingMusic != null)
            {
                UpdateMusicTrack(_fadingMusic, _fadingMusicMasterFade, dt);
            }

            foreach (var track in _ambientTracks.Values)
            {
                if (Math.Abs(track.CurrentVolume - track.TargetVolume) > 0.01f)
                {
                    track.CurrentVolume = MathHelper.Lerp(track.CurrentVolume, track.TargetVolume, dt * 2f);
                }
                else
                {
                    track.CurrentVolume = track.TargetVolume;
                }

                if (track.CurrentVolume <= 0.01f && track.TargetVolume == 0f)
                {
                    if (track.Instance.State == SoundState.Playing) track.Instance.Stop();
                }
                else
                {
                    track.Instance.Volume = track.CurrentVolume * track.BaseVolume * _ambientVolume * _masterVolume;
                }
            }
        }

        private void UpdateMusicTrack(MusicTrack track, float masterFade, float dt)
        {
            for (int i = 0; i < track.Stems.Length; i++)
            {
                if (Math.Abs(track.CurrentStemVolumes[i] - track.TargetStemVolumes[i]) > 0.01f)
                {
                    track.CurrentStemVolumes[i] = MathHelper.Lerp(track.CurrentStemVolumes[i], track.TargetStemVolumes[i], dt * 3f);
                }
                else
                {
                    track.CurrentStemVolumes[i] = track.TargetStemVolumes[i];
                }

                track.Stems[i].Volume = track.CurrentStemVolumes[i] * track.BaseVolume * masterFade * _musicVolume * _masterVolume;
            }
        }

        private void UpdateActiveVolumes()
        {
            foreach (var pool in _sfxPools.Values)
            {
                foreach (var instance in pool.Instances)
                {
                    if (instance.State == SoundState.Playing)
                    {
                        instance.Volume = pool.BaseVolume * _sfxVolume * _masterVolume;
                    }
                }
            }

            foreach (var pool in _uiPools.Values)
            {
                foreach (var instance in pool.Instances)
                {
                    if (instance.State == SoundState.Playing)
                    {
                        instance.Volume = pool.BaseVolume * _uiVolume * _masterVolume;
                    }
                }
            }
        }
    }
}