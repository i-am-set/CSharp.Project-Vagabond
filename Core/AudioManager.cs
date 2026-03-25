using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
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
            public float[] BasePitches;
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
            public float[] StemFadeSpeeds;
            public float BaseVolume;
            public string NextTrackId;
        }

        private class AmbientTrack
        {
            public SoundEffectInstance Instance;
            public float BaseVolume;
            public float TargetVolume;
            public float CurrentVolume;
            public string NextTrackId;
        }

        private readonly Dictionary<string, PooledSound> _sfxPools = new Dictionary<string, PooledSound>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PooledSound> _uiPools = new Dictionary<string, PooledSound>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MusicTrack> _musicTracks = new Dictionary<string, MusicTrack>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AmbientTrack> _ambientTracks = new Dictionary<string, AmbientTrack>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, SoundEffectInstance> _activeLoops = new Dictionary<Guid, SoundEffectInstance>();

        private MusicTrack _currentMusic;
        private MusicTrack _fadingMusic;
        private float _musicCrossfadeTimer;
        private float _musicCrossfadeDuration = 2.0f;
        private float _currentMusicMasterFade = 1.0f;
        private float _fadingMusicMasterFade = 0.0f;
        private float _fadingMusicStartFade = 1.0f;

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

        private float CalculatePan(Vector2? position)
        {
            if (!position.HasValue) return 0f;
            float pan = (position.Value.X / Global.VIRTUAL_WIDTH) * 2f - 1f;
            return Math.Clamp(pan, -1f, 1f);
        }

        public void PlayRoutedSfx(string id, float pitchVariance = 0f, float? exactPitch = null, Vector2? position = null)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (id.StartsWith("proc:", StringComparison.OrdinalIgnoreCase))
            {
                if (!_sfxPools.ContainsKey(id))
                {
                    try
                    {
                        var sfx = SynthEngine.Generate(id);
                        int poolSize = 3;
                        var instances = new SoundEffectInstance[poolSize];
                        for (int i = 0; i < poolSize; i++)
                        {
                            instances[i] = sfx.CreateInstance();
                        }

                        _sfxPools[id] = new PooledSound
                        {
                            Instances = instances,
                            BasePitches = new float[poolSize],
                            BaseVolume = 1.0f,
                            MinPitch = 0f,
                            MaxPitch = 0f,
                            CurrentIndex = 0
                        };
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Log(LogSeverity.Warning, $"[AudioManager] Failed to generate procedural sound '{id}': {ex.Message}");
                        return;
                    }
                }
            }

            PlaySfx(id, pitchVariance, exactPitch, position);
        }

        public Guid PlayLoopingSfx(string id, Vector2? position = null)
        {
            if (string.IsNullOrEmpty(id)) return Guid.Empty;

            if (id.StartsWith("proc:", StringComparison.OrdinalIgnoreCase) && !_sfxPools.ContainsKey(id))
            {
                PlayRoutedSfx(id); // Forces generation and caching
            }

            if (_sfxPools.TryGetValue(id, out var pool))
            {
                var instance = pool.Instances[0].IsDisposed ? null : pool.Instances[0];
                if (instance == null) return Guid.Empty;

                int index = GetAvailableInstanceIndex(pool);
                var loopInstance = pool.Instances[index];

                loopInstance.IsLooped = true;
                loopInstance.Volume = pool.BaseVolume * _sfxVolume * _masterVolume;
                loopInstance.Pan = CalculatePan(position);
                loopInstance.Play();

                Guid handle = Guid.NewGuid();
                _activeLoops[handle] = loopInstance;
                return handle;
            }

            return Guid.Empty;
        }

        public void UpdateLoopingSfxPosition(Guid handle, Vector2 position)
        {
            if (handle != Guid.Empty && _activeLoops.TryGetValue(handle, out var instance))
            {
                instance.Pan = CalculatePan(position);
            }
        }

        public void StopLoopingSfx(Guid handle)
        {
            if (handle != Guid.Empty && _activeLoops.TryGetValue(handle, out var instance))
            {
                instance.Stop();
                instance.IsLooped = false;
                _activeLoops.Remove(handle);
            }
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
                        BasePitches = new float[entry.PoolSize],
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
                    instance.IsLooped = string.IsNullOrEmpty(entry.NextTrack);

                    _ambientTracks[entry.Id] = new AmbientTrack
                    {
                        Instance = instance,
                        BaseVolume = entry.DefaultVolume,
                        TargetVolume = 0f,
                        CurrentVolume = 0f,
                        NextTrackId = entry.NextTrack
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
                    var stems = new List<SoundEffectInstance>();
                    var targetVols = new List<float>();
                    var currentVols = new List<float>();
                    var fadeSpeeds = new List<float>();

                    for (int i = 0; i < entry.StemPaths.Count; i++)
                    {
                        try
                        {
                            var sfx = content.Load<SoundEffect>(entry.StemPaths[i]);
                            var instance = sfx.CreateInstance();
                            instance.IsLooped = string.IsNullOrEmpty(entry.NextTrack);
                            stems.Add(instance);
                            targetVols.Add(1.0f);
                            currentVols.Add(1.0f);
                            fadeSpeeds.Add(3.0f);
                        }
                        catch (Exception ex)
                        {
                            GameLogger.Log(LogSeverity.Warning, $"[AudioManager] Failed to load stem '{entry.StemPaths[i]}': {ex.Message}");
                        }
                    }

                    if (stems.Count > 0)
                    {
                        _musicTracks[entry.Id] = new MusicTrack
                        {
                            Stems = stems.ToArray(),
                            TargetStemVolumes = targetVols.ToArray(),
                            CurrentStemVolumes = currentVols.ToArray(),
                            StemFadeSpeeds = fadeSpeeds.ToArray(),
                            BaseVolume = entry.DefaultVolume,
                            NextTrackId = entry.NextTrack
                        };
                    }
                    else
                    {
                        GameLogger.Log(LogSeverity.Warning, $"[AudioManager] Music track '{entry.Id}' has no valid stems and was not loaded.");
                    }
                }
                catch (Exception ex)
                {
                    GameLogger.Log(LogSeverity.Warning, $"[AudioManager] Failed to load music '{entry.Id}': {ex.Message}");
                }
            }
        }

        public void PlaySfx(string id, float pitchVariance = 0f, float? exactPitch = null, Vector2? position = null)
        {
            if (string.IsNullOrEmpty(id) || !_sfxPools.TryGetValue(id, out var pool)) return;

            int index = GetAvailableInstanceIndex(pool);
            var instance = pool.Instances[index];

            instance.Volume = pool.BaseVolume * _sfxVolume * _masterVolume;
            instance.Pan = CalculatePan(position);

            float calculatedPitch = 0f;
            if (exactPitch.HasValue)
            {
                calculatedPitch = exactPitch.Value;
            }
            else if (pool.MinPitch != 0f || pool.MaxPitch != 0f)
            {
                calculatedPitch = pool.MinPitch + (float)(_random.NextDouble() * (pool.MaxPitch - pool.MinPitch));
            }
            else if (pitchVariance > 0f)
            {
                calculatedPitch = (float)(_random.NextDouble() * 2.0 - 1.0) * pitchVariance;
            }

            pool.BasePitches[index] = calculatedPitch;

            bool isFF = false;
            try { isFF = ServiceLocator.Get<InputManager>().IsCurrentlyFastForwarding; } catch { }
            instance.Pitch = Math.Clamp(calculatedPitch + (isFF ? 1.0f : 0.0f), -1f, 1f);

            instance.Play();
        }

        public void PlayUi(string id, float pitchVariance = 0f, float? exactPitch = null, Vector2? position = null)
        {
            if (string.IsNullOrEmpty(id) || !_uiPools.TryGetValue(id, out var pool)) return;

            int index = GetAvailableInstanceIndex(pool);
            var instance = pool.Instances[index];

            instance.Volume = pool.BaseVolume * _uiVolume * _masterVolume;
            instance.Pan = CalculatePan(position);

            float calculatedPitch = 0f;
            if (exactPitch.HasValue)
            {
                calculatedPitch = exactPitch.Value;
            }
            else if (pool.MinPitch != 0f || pool.MaxPitch != 0f)
            {
                calculatedPitch = pool.MinPitch + (float)(_random.NextDouble() * (pool.MaxPitch - pool.MinPitch));
            }
            else if (pitchVariance > 0f)
            {
                calculatedPitch = (float)(_random.NextDouble() * 2.0 - 1.0) * pitchVariance;
            }

            pool.BasePitches[index] = calculatedPitch;

            bool isFF = false;
            try { isFF = ServiceLocator.Get<InputManager>().IsCurrentlyFastForwarding; } catch { }
            instance.Pitch = Math.Clamp(calculatedPitch + (isFF ? 1.0f : 0.0f), -1f, 1f);

            instance.Play();
        }

        private int GetAvailableInstanceIndex(PooledSound pool)
        {
            for (int i = 0; i < pool.Instances.Length; i++)
            {
                int index = (pool.CurrentIndex + i) % pool.Instances.Length;
                if (pool.Instances[index].State == SoundState.Stopped)
                {
                    pool.CurrentIndex = (index + 1) % pool.Instances.Length;
                    return index;
                }
            }

            int stealIndex = pool.CurrentIndex;
            pool.CurrentIndex = (pool.CurrentIndex + 1) % pool.Instances.Length;
            pool.Instances[stealIndex].Stop();
            return stealIndex;
        }

        public void PlayMusic(string id, float crossfadeDuration = 2.0f)
        {
            if (string.IsNullOrEmpty(id) || !_musicTracks.TryGetValue(id, out var nextMusic)) return;
            if (_currentMusic == nextMusic) return;

            if (_currentMusic != null)
            {
                if (_fadingMusic != null && _fadingMusic != _currentMusic)
                {
                    foreach (var stem in _fadingMusic.Stems)
                    {
                        stem.Stop();
                    }
                }

                _fadingMusic = _currentMusic;
                _fadingMusicMasterFade = _currentMusicMasterFade;
                _fadingMusicStartFade = _currentMusicMasterFade;
            }
            else if (_fadingMusic != null)
            {
                _fadingMusicStartFade = _fadingMusicMasterFade;
            }

            _currentMusic = nextMusic;
            _currentMusicMasterFade = 0f;
            _musicCrossfadeDuration = crossfadeDuration > 0f ? crossfadeDuration : 0.01f;
            _musicCrossfadeTimer = 0f;

            for (int i = 0; i < _currentMusic.Stems.Length; i++)
            {
                _currentMusic.Stems[i].Stop();
                _currentMusic.Stems[i].Volume = 0f; // Prevent pop
                _currentMusic.Stems[i].Play();
            }
        }

        public void SetMusicStemVolume(string id, int stemIndex, float targetVolume, bool instant = false, float fadeSpeed = 3.0f)
        {
            if (_musicTracks.TryGetValue(id, out var track))
            {
                if (stemIndex >= 0 && stemIndex < track.TargetStemVolumes.Length)
                {
                    track.TargetStemVolumes[stemIndex] = Math.Clamp(targetVolume, 0f, 1f);
                    track.StemFadeSpeeds[stemIndex] = fadeSpeed;
                    if (instant)
                    {
                        track.CurrentStemVolumes[stemIndex] = track.TargetStemVolumes[stemIndex];
                    }
                }
            }
        }

        public void SetCurrentMusicStemVolume(int stemIndex, float targetVolume, bool instant = false, float fadeSpeed = 3.0f)
        {
            if (_currentMusic != null && stemIndex >= 0 && stemIndex < _currentMusic.TargetStemVolumes.Length)
            {
                _currentMusic.TargetStemVolumes[stemIndex] = Math.Clamp(targetVolume, 0f, 1f);
                _currentMusic.StemFadeSpeeds[stemIndex] = fadeSpeed;
                if (instant)
                {
                    _currentMusic.CurrentStemVolumes[stemIndex] = _currentMusic.TargetStemVolumes[stemIndex];
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
                    track.Instance.Volume = 0f; // Prevent pop
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

        public void ForceTransitionAmbient(string currentId)
        {
            if (_ambientTracks.TryGetValue(currentId, out var track))
            {
                if (track.Instance.State == SoundState.Playing)
                {
                    track.Instance.Stop();
                }
                track.TargetVolume = 0f;
                track.CurrentVolume = 0f;

                if (!string.IsNullOrEmpty(track.NextTrackId))
                {
                    PlayAmbient(track.NextTrackId, 1.0f);
                }
            }
        }

        public void StopAmbient(string id, bool instant = false)
        {
            if (_ambientTracks.TryGetValue(id, out var track))
            {
                track.TargetVolume = 0f;
                if (instant)
                {
                    track.CurrentVolume = 0f;
                    if (track.Instance.State == Microsoft.Xna.Framework.Audio.SoundState.Playing)
                    {
                        track.Instance.Stop();
                    }
                }
            }
        }

        public void StopMusic(float fadeDuration = 2.0f)
        {
            if (_currentMusic != null)
            {
                if (_fadingMusic != null && _fadingMusic != _currentMusic)
                {
                    foreach (var stem in _fadingMusic.Stems)
                    {
                        stem.Stop();
                    }
                }

                _fadingMusic = _currentMusic;
                _fadingMusicMasterFade = _currentMusicMasterFade;
                _fadingMusicStartFade = _currentMusicMasterFade;
                _currentMusic = null;
                _musicCrossfadeDuration = fadeDuration > 0f ? fadeDuration : 0.01f;
                _musicCrossfadeTimer = 0f;
            }
            else if (_fadingMusic != null)
            {
                _fadingMusicStartFade = _fadingMusicMasterFade;
                _musicCrossfadeDuration = fadeDuration > 0f ? fadeDuration : 0.01f;
                _musicCrossfadeTimer = 0f;
            }
        }

        public void StopAll()
        {
            StopMusic(0f);

            foreach (var track in _ambientTracks.Values)
            {
                track.TargetVolume = 0f;
                track.CurrentVolume = 0f;
                if (track.Instance.State == SoundState.Playing) track.Instance.Stop();
            }

            foreach (var pool in _sfxPools.Values)
            {
                foreach (var inst in pool.Instances)
                {
                    if (inst.State == SoundState.Playing) inst.Stop();
                }
            }

            foreach (var pool in _uiPools.Values)
            {
                foreach (var inst in pool.Instances)
                {
                    if (inst.State == SoundState.Playing) inst.Stop();
                }
            }

            foreach (var inst in _activeLoops.Values)
            {
                if (inst.State == SoundState.Playing) inst.Stop();
            }
            _activeLoops.Clear();
        }

        public void Update(float dt)
        {
            bool isFF = false;
            try { isFF = ServiceLocator.Get<InputManager>().IsCurrentlyFastForwarding; } catch { }
            float pitchOffset = isFF ? 1.0f : 0.0f;

            if (_musicCrossfadeTimer < _musicCrossfadeDuration)
            {
                _musicCrossfadeTimer += dt;
                float progress = Math.Clamp(_musicCrossfadeTimer / _musicCrossfadeDuration, 0f, 1f);

                if (_currentMusic != null)
                    _currentMusicMasterFade = MathHelper.Lerp(0f, 1f, progress);

                if (_fadingMusic != null)
                    _fadingMusicMasterFade = MathHelper.Lerp(_fadingMusicStartFade, 0f, progress);
            }
            else if (_fadingMusic != null)
            {
                foreach (var stem in _fadingMusic.Stems)
                {
                    stem.Stop();
                }
                _fadingMusic = null;
            }

            if (_currentMusic != null && _musicCrossfadeTimer > 1.0f && !string.IsNullOrEmpty(_currentMusic.NextTrackId))
            {
                bool allStopped = true;
                foreach (var stem in _currentMusic.Stems)
                {
                    if (stem.State == SoundState.Playing)
                    {
                        allStopped = false;
                        break;
                    }
                }

                if (allStopped)
                {
                    var nextTrackId = _currentMusic.NextTrackId;
                    var oldVols = _currentMusic.TargetStemVolumes.ToArray();
                    var oldCurrentVols = _currentMusic.CurrentStemVolumes.ToArray();

                    _currentMusic = null; // Prevent fading it out
                    PlayMusic(nextTrackId, 0f);

                    if (_currentMusic != null)
                    {
                        for (int i = 0; i < Math.Min(oldVols.Length, _currentMusic.TargetStemVolumes.Length); i++)
                        {
                            _currentMusic.TargetStemVolumes[i] = oldVols[i];
                            _currentMusic.CurrentStemVolumes[i] = oldCurrentVols[i];
                        }
                    }
                }
            }

            if (_currentMusic != null)
            {
                UpdateMusicTrack(_currentMusic, _currentMusicMasterFade, dt, pitchOffset);
            }

            if (_fadingMusic != null)
            {
                UpdateMusicTrack(_fadingMusic, _fadingMusicMasterFade, dt, pitchOffset);
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
                    if (track.Instance.State == SoundState.Playing) track.Instance.Pitch = pitchOffset;
                }

                // Check for NextTrack transition
                if (!string.IsNullOrEmpty(track.NextTrackId) && track.TargetVolume > 0f)
                {
                    if (track.Instance.State == SoundState.Stopped && track.CurrentVolume > 0f)
                    {
                        // It finished playing naturally
                        track.TargetVolume = 0f;
                        track.CurrentVolume = 0f;
                        PlayAmbient(track.NextTrackId, 1.0f);
                    }
                }
            }

            foreach (var pool in _sfxPools.Values)
            {
                for (int i = 0; i < pool.Instances.Length; i++)
                {
                    if (pool.Instances[i].State == SoundState.Playing)
                    {
                        pool.Instances[i].Pitch = Math.Clamp(pool.BasePitches[i] + pitchOffset, -1f, 1f);
                    }
                }
            }

            foreach (var pool in _uiPools.Values)
            {
                for (int i = 0; i < pool.Instances.Length; i++)
                {
                    if (pool.Instances[i].State == SoundState.Playing)
                    {
                        pool.Instances[i].Pitch = Math.Clamp(pool.BasePitches[i] + pitchOffset, -1f, 1f);
                    }
                }
            }

            // Update active looping sounds
            foreach (var loopInstance in _activeLoops.Values)
            {
                if (loopInstance.State == SoundState.Playing)
                {
                    loopInstance.Pitch = Math.Clamp(pitchOffset, -1f, 1f);
                }
            }
        }

        private void UpdateMusicTrack(MusicTrack track, float masterFade, float dt, float pitchOffset)
        {
            for (int i = 0; i < track.Stems.Length; i++)
            {
                if (Math.Abs(track.CurrentStemVolumes[i] - track.TargetStemVolumes[i]) > 0.01f)
                {
                    track.CurrentStemVolumes[i] = MathHelper.Lerp(track.CurrentStemVolumes[i], track.TargetStemVolumes[i], dt * track.StemFadeSpeeds[i]);
                }
                else
                {
                    track.CurrentStemVolumes[i] = track.TargetStemVolumes[i];
                }

                track.Stems[i].Volume = track.CurrentStemVolumes[i] * track.BaseVolume * masterFade * _musicVolume * _masterVolume;
                if (track.Stems[i].State == SoundState.Playing) track.Stems[i].Pitch = pitchOffset;
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

            foreach (var loopInstance in _activeLoops.Values)
            {
                if (loopInstance.State == SoundState.Playing)
                {
                    // Assuming base volume of 1.0f for loops for now, can be expanded if needed
                    loopInstance.Volume = 1.0f * _sfxVolume * _masterVolume;
                }
            }
        }
    }
}
