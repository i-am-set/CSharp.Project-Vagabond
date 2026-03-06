using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Animations
{
    public class AnimationData
    {
        public string ID { get; set; }
        public string Type { get; set; }
        public SpriteAnimationData Sprite { get; set; }
        public List<ParticleAnimationData> Particles { get; set; }
    }

    public class SpriteAnimationData
    {
        public string TexturePath { get; set; }
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public float FrameRate { get; set; }
        public float Scale { get; set; }
        public int ImpactFrame { get; set; }
        public bool IsProjectile { get; set; }
        public float ProjectileSpeed { get; set; }
        public bool DrawOnHitboxCenter { get; set; }
    }

    public class ParticleAnimationData
    {
        public string Mode { get; set; }
        public float Duration { get; set; }
        public float ProjectileSpeed { get; set; }
        public bool MaintainVisualSpeed { get; set; }
        public float FixedLobHeight { get; set; }
        public float MinLobHeight { get; set; }
        public float MaxLobHeight { get; set; }
        public float LobScaleMultiplier { get; set; }
        public ParticleEmitterData Emitter { get; set; }
        public ParticleEmitterData Trail { get; set; }
        public List<ParticleEmitterData> Impacts { get; set; }
    }

    public class ParticleEmitterData
    {
        public string Shape { get; set; }
        public string EmitFrom { get; set; }
        public float EmitterSizeX { get; set; }
        public float EmitterSizeY { get; set; }
        public float EmissionRate { get; set; }
        public int BurstCount { get; set; }
        public int MaxParticles { get; set; }
        public string VelocityPattern { get; set; }
        public float ConeSpread { get; set; }
        public float LifetimeMin { get; set; }
        public float LifetimeMax { get; set; }
        public float SpeedMin { get; set; }
        public float SpeedMax { get; set; }
        public float VelocityXMin { get; set; }
        public float VelocityXMax { get; set; }
        public float VelocityYMin { get; set; }
        public float VelocityYMax { get; set; }
        public float SizeMin { get; set; }
        public float SizeMax { get; set; }
        public float EndSizeMin { get; set; }
        public float EndSizeMax { get; set; }
        public bool InterpolateSize { get; set; }
        public float GravityX { get; set; }
        public float GravityY { get; set; }
        public float Drag { get; set; }
        public float Bounciness { get; set; }
        public float FloorScatterY { get; set; }
        public string StartColor { get; set; }
        public string EndColor { get; set; }
        public float StartAlpha { get; set; }
        public float EndAlpha { get; set; }
        public string BlendMode { get; set; }
        public string TexturePath { get; set; }
    }

    public interface IAnimationInstance
    {
        bool IsFinished { get; }
        bool HasTriggeredImpact { get; }
        void Start(ActiveAttack attack, ArenaScene arena);
        void Update(float dt, ArenaScene arena, ActiveAttack attack);
        void Draw(SpriteBatch spriteBatch, ActiveAttack attack);
    }

    public static class AnimationFactory
    {
        public static IAnimationInstance CreateAnimation(string animationId)
        {
            if (string.IsNullOrEmpty(animationId)) return null;

            if (!GameDataCache.Animations.TryGetValue(animationId, out var data))
            {
                GameLogger.Log(LogSeverity.Warning, $"[AnimationSystem] Could not find animation ID '{animationId}' in Animations.json.");
                return null;
            }

            if (data.Type == "Sprite")
            {
                if (data.Sprite == null)
                {
                    GameLogger.Log(LogSeverity.Error, $"[AnimationSystem] Animation '{animationId}' is Type 'Sprite' but is missing the nested 'Sprite' object in JSON.");
                    return null;
                }
                return new SpriteAnimationInstance(data.Sprite);
            }
            else if (data.Type == "Particle")
            {
                if (data.Particles == null)
                {
                    GameLogger.Log(LogSeverity.Error, $"[AnimationSystem] Animation '{animationId}' is Type 'Particle' but is missing the nested 'Particles' array in JSON.");
                    return null;
                }
                return new ParticleAnimationInstance(data.Particles);
            }

            return null;
        }

        public static ParticleEmitterSettings MapEmitterData(ParticleEmitterData data)
        {
            var settings = ParticleEmitterSettings.CreateDefault();

            if (Enum.TryParse<EmitterShape>(data.Shape, true, out var shape)) settings.Shape = shape;
            if (Enum.TryParse<EmissionSource>(data.EmitFrom, true, out var source)) settings.EmitFrom = source;

            settings.EmitterSize = new Vector2(data.EmitterSizeX, data.EmitterSizeY);
            settings.EmissionRate = data.EmissionRate;
            settings.BurstCount = data.BurstCount;
            settings.MaxParticles = data.MaxParticles > 0 ? data.MaxParticles : 100;

            if (Enum.TryParse<EmissionPattern>(data.VelocityPattern, true, out var pat)) settings.VelocityPattern = pat;
            settings.ConeSpread = data.ConeSpread;

            settings.Lifetime = new FloatRange(data.LifetimeMin, data.LifetimeMax);

            if (settings.VelocityPattern == EmissionPattern.Radial || settings.VelocityPattern == EmissionPattern.Cone)
            {
                settings.InitialVelocityX = new FloatRange(data.SpeedMin, data.SpeedMax);
            }
            else
            {
                settings.InitialVelocityX = new FloatRange(data.VelocityXMin, data.VelocityXMax);
                settings.InitialVelocityY = new FloatRange(data.VelocityYMin, data.VelocityYMax);
            }

            settings.InitialSize = new FloatRange(data.SizeMin, data.SizeMax);
            settings.EndSize = new FloatRange(data.EndSizeMin, data.EndSizeMax);
            settings.InterpolateSize = data.InterpolateSize;
            settings.Gravity = new Vector2(data.GravityX, data.GravityY);
            settings.Drag = data.Drag;
            settings.Bounciness = data.Bounciness;
            settings.FloorScatterY = data.FloorScatterY;

            settings.StartColor = ParseColor(data.StartColor);
            settings.EndColor = ParseColor(data.EndColor);
            settings.StartAlpha = data.StartAlpha;
            settings.EndAlpha = data.EndAlpha;

            settings.BlendMode = data.BlendMode == "Additive" ? BlendState.Additive : BlendState.AlphaBlend;

            if (!string.IsNullOrEmpty(data.TexturePath))
            {
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                if (data.TexturePath == "HealParticle") settings.Texture = spriteManager.HealParticleSprite;
                else if (data.TexturePath == "CircleParticle") settings.Texture = spriteManager.CircleParticleSprite;
                else if (data.TexturePath == "SoftParticle") settings.Texture = spriteManager.SoftParticleSprite;
                else if (data.TexturePath == "EmberParticle") settings.Texture = spriteManager.EmberParticleSprite;
                else if (data.TexturePath == "ScratchParticle") settings.Texture = spriteManager.ScratchParticleSprite;
                else
                {
                    try
                    {
                        settings.Texture = ServiceLocator.Get<Core>().Content.Load<Texture2D>(data.TexturePath);
                    }
                    catch
                    {
                        settings.Texture = ServiceLocator.Get<Texture2D>();
                    }
                }
            }

            return settings;
        }

        private static Color ParseColor(string hexOrName)
        {
            if (string.IsNullOrEmpty(hexOrName)) return Color.White;

            var globalProp = typeof(Global).GetProperty(hexOrName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (globalProp != null && globalProp.PropertyType == typeof(Color))
            {
                return (Color)globalProp.GetValue(ServiceLocator.Get<Global>());
            }

            var prop = typeof(Color).GetProperty(hexOrName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase);
            if (prop != null) return (Color)prop.GetValue(null);

            string hex = hexOrName.StartsWith("#") ? hexOrName.Substring(1) : hexOrName;
            if (hex.Length == 6 || hex.Length == 8)
            {
                try
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte a = hex.Length == 8 ? Convert.ToByte(hex.Substring(6, 2), 16) : (byte)255;
                    return new Color(r, g, b, a);
                }
                catch { }
            }

            return Color.White;
        }
    }

    public class SpriteAnimationInstance : IAnimationInstance
    {
        private readonly SpriteAnimationData _data;
        private float _timer;
        private int _currentFrame;
        private List<Vector2> _targetPositions = new List<Vector2>();
        private Texture2D _texture;

        public bool IsFinished { get; private set; }
        public bool HasTriggeredImpact { get; private set; }

        public SpriteAnimationInstance(SpriteAnimationData data)
        {
            _data = data;
        }

        public void Start(ActiveAttack attack, ArenaScene arena)
        {
            try
            {
                _texture = ServiceLocator.Get<Core>().Content.Load<Texture2D>(_data.TexturePath);
            }
            catch
            {
                GameLogger.Log(LogSeverity.Error, $"[AnimationSystem] Failed to load texture '{_data.TexturePath}'. Using debug fallback.");
                _texture = ServiceLocator.Get<TextureFactory>().CreateTwoColorTexture(32, 32, Color.Magenta, Color.Black);
            }

            _targetPositions.Clear();

            if (_data.IsProjectile)
            {
                _targetPositions.Add(attack.Origin);
            }
            else if (_data.DrawOnHitboxCenter)
            {
                if (attack.DeliveryInstance is DashMeleeDelivery dash)
                {
                    _targetPositions.Add(attack.Origin + attack.Direction * (dash.DashDistance / 2f));
                }
                else if (attack.DeliveryInstance is TickingBeamDelivery beam)
                {
                    _targetPositions.Add(attack.Origin + attack.Direction * (beam.Length / 2f));
                }
                else
                {
                    _targetPositions.Add(attack.TargetPosition);
                }
            }
            else
            {
                if (attack.DeliveryInstance is InstantAOEDelivery aoe)
                {
                    var targets = arena.GetWizardsInCircle(attack.TargetPosition, aoe.Radius);
                    foreach (var t in targets)
                    {
                        if (!attack.Move.CanTargetSelf && t == attack.Caster) continue;
                        _targetPositions.Add(t.Position);
                    }
                }
                else if (attack.DeliveryInstance is TickingBeamDelivery beam)
                {
                    var targets = arena.GetWizardsInOBB(attack.Origin, attack.Direction, beam.Width, beam.Length);
                    foreach (var t in targets)
                    {
                        if (!attack.Move.CanTargetSelf && t == attack.Caster) continue;
                        _targetPositions.Add(t.Position);
                    }
                }
                else if (attack.DeliveryInstance is SelfDelivery)
                {
                    _targetPositions.Add(attack.Caster.Position);
                }
                else if (attack.DeliveryInstance is DashMeleeDelivery)
                {
                    // Targets are added dynamically in Update as they are hit
                }
                else
                {
                    _targetPositions.Add(attack.TargetPosition);
                }
            }

            if (_targetPositions.Count == 0 && !(attack.DeliveryInstance is DashMeleeDelivery))
            {
                _targetPositions.Add(attack.TargetPosition);
            }
        }

        public void Update(float dt, ArenaScene arena, ActiveAttack attack)
        {
            if (IsFinished) return;

            if (attack.DeliveryInstance is DashMeleeDelivery dash && !_data.DrawOnHitboxCenter)
            {
                _targetPositions.Clear();
                foreach (var target in dash.HitTargets)
                {
                    _targetPositions.Add(target.Position);
                }
            }

            if (_data.IsProjectile)
            {
                if (_targetPositions.Count > 0)
                {
                    Vector2 dir = attack.TargetPosition - _targetPositions[0];
                    float dist = dir.Length();

                    if (_data.ProjectileSpeed <= 0 || dist < _data.ProjectileSpeed * dt)
                    {
                        _targetPositions[0] = attack.TargetPosition;
                        if (!HasTriggeredImpact) HasTriggeredImpact = true;
                        IsFinished = true;
                    }
                    else
                    {
                        if (dist > 0) _targetPositions[0] += Vector2.Normalize(dir) * _data.ProjectileSpeed * dt;
                    }
                }
            }

            _timer += dt;
            if (_data.FrameRate > 0)
            {
                float frameDuration = 1f / _data.FrameRate;
                while (_timer >= frameDuration)
                {
                    _timer -= frameDuration;
                    _currentFrame++;

                    if (_currentFrame == _data.ImpactFrame && !HasTriggeredImpact)
                    {
                        HasTriggeredImpact = true;
                    }

                    int frameWidth = _data.FrameWidth > 0 ? _data.FrameWidth : (_texture?.Width ?? 32);
                    int maxFrames = (_texture?.Width ?? 32) / frameWidth;

                    if (maxFrames <= 0) maxFrames = 1;

                    if (_currentFrame >= maxFrames)
                    {
                        IsFinished = true;
                        if (!HasTriggeredImpact) HasTriggeredImpact = true;
                        break;
                    }
                }
            }
            else
            {
                IsFinished = true;
                HasTriggeredImpact = true;
            }
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            if (IsFinished || _texture == null) return;

            int frameWidth = _data.FrameWidth > 0 ? _data.FrameWidth : _texture.Width;
            int frameHeight = _data.FrameHeight > 0 ? _data.FrameHeight : _texture.Height;

            if (frameWidth <= 0) frameWidth = 32;
            if (frameHeight <= 0) frameHeight = 32;

            var sourceRect = new Rectangle(_currentFrame * frameWidth, 0, frameWidth, frameHeight);
            var origin = new Vector2(frameWidth / 2f, frameHeight / 2f);

            float rotation = 0f;
            if (_data.IsProjectile || _data.DrawOnHitboxCenter)
            {
                Vector2 dir = attack.Direction;
                if (dir.LengthSquared() > 0) rotation = MathF.Atan2(dir.Y, dir.X);
            }

            float scale = _data.Scale > 0 ? _data.Scale : 1f;

            foreach (var pos in _targetPositions)
            {
                spriteBatch.DrawSnapped(_texture, pos, sourceRect, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
            }
        }
    }

    public class ParticleAnimationInstance : IAnimationInstance
    {
        private readonly List<ParticleAnimationData> _layers;
        private readonly List<ParticleEmitter> _emitters = new();
        private readonly List<ParticleEmitter> _trailEmitters = new();
        private readonly List<FloatRange> _baseEmitterSizes = new();
        private readonly List<FloatRange> _baseTrailSizes = new();

        private float _timer;
        private Vector2 _projectilePos;
        private Vector2 _startPos;
        private Vector2 _targetPos;
        private float _totalDist;

        public bool IsFinished { get; private set; }
        public bool HasTriggeredImpact { get; private set; }

        public ParticleAnimationInstance(List<ParticleAnimationData> layers)
        {
            _layers = layers;
        }

        public void Start(ActiveAttack attack, ArenaScene arena)
        {
            var psm = ServiceLocator.Get<ParticleSystemManager>();
            _startPos = attack.Origin;
            _targetPos = attack.TargetPosition;
            _totalDist = Vector2.Distance(_startPos, _targetPos);
            _projectilePos = _startPos;

            _emitters.Clear();
            _trailEmitters.Clear();
            _baseEmitterSizes.Clear();
            _baseTrailSizes.Clear();

            foreach (var layer in _layers)
            {
                var settings = AnimationFactory.MapEmitterData(layer.Emitter);
                _baseEmitterSizes.Add(settings.InitialSize);

                if (layer.Mode == "Spray")
                {
                    float angle = MathF.Atan2(attack.Direction.Y, attack.Direction.X);
                    settings.VelocityPattern = EmissionPattern.Cone;
                    settings.ConeAngle = angle;
                    settings.InitialRotation = new FloatRange(angle);
                }

                var emitter = psm.CreateEmitter(settings);

                if (layer.Mode == "Spray")
                {
                    emitter.Position = attack.Origin;
                    _trailEmitters.Add(null);
                    _baseTrailSizes.Add(new FloatRange(0));
                }
                else if (layer.Mode == "Volume")
                {
                    if (attack.DeliveryInstance is TickingBeamDelivery beam)
                    {
                        emitter.Settings.Shape = EmitterShape.Rectangle;
                        emitter.Settings.EmitterSize = new Vector2(beam.Length, beam.Width);
                        emitter.Settings.EmitterRotation = MathF.Atan2(attack.Direction.Y, attack.Direction.X);
                        emitter.Position = attack.Origin + attack.Direction * (beam.Length / 2f);
                    }
                    else if (attack.DeliveryInstance is InstantAOEDelivery aoe)
                    {
                        emitter.Settings.Shape = EmitterShape.Circle;
                        emitter.Settings.EmitterSize = new Vector2(aoe.Radius * 2f, aoe.Radius * 2f);
                        emitter.Position = attack.TargetPosition;
                    }
                    else
                    {
                        emitter.Position = attack.TargetPosition;
                    }
                    _trailEmitters.Add(null);
                    _baseTrailSizes.Add(new FloatRange(0));
                }
                else if (layer.Mode == "Projectile")
                {
                    emitter.Position = _projectilePos;
                    if (layer.Trail != null)
                    {
                        var trailSettings = AnimationFactory.MapEmitterData(layer.Trail);
                        _baseTrailSizes.Add(trailSettings.InitialSize);
                        var trail = psm.CreateEmitter(trailSettings);
                        trail.Position = _projectilePos;
                        _trailEmitters.Add(trail);
                    }
                    else
                    {
                        _baseTrailSizes.Add(new FloatRange(0));
                        _trailEmitters.Add(null);
                    }
                }

                // Trigger the initial burst if the emitter relies on one
                if (settings.BurstCount > 0)
                {
                    emitter.EmitBurst(settings.BurstCount);
                }

                _emitters.Add(emitter);
            }
        }

        public void Update(float dt, ArenaScene arena, ActiveAttack attack)
        {
            if (IsFinished) return;
            _timer += dt;

            bool allProjectilesArrived = true;
            bool hasProjectiles = false;

            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer.Mode == "Projectile")
                {
                    hasProjectiles = true;
                    float travelDuration = attack.Move.ChargeTime > 0 ? attack.Move.ChargeTime : 1.0f;
                    float progress = Math.Clamp(_timer / travelDuration, 0f, 1f);

                    if (progress >= 1.0f)
                    {
                        _projectilePos = _targetPos;
                        _emitters[i].IsActive = false;
                        if (i < _trailEmitters.Count && _trailEmitters[i] != null) _trailEmitters[i].IsActive = false;
                    }
                    else
                    {
                        Vector2 basePos = Vector2.Lerp(_startPos, _targetPos, progress);

                        float desiredSpeed = layer.ProjectileSpeed > 0 ? layer.ProjectileSpeed : 250f;
                        float lobHeight = layer.FixedLobHeight;

                        if (layer.MaintainVisualSpeed)
                        {
                            float targetPathLength = desiredSpeed * travelDuration;
                            float requiredExtraLength = Math.Max(0, targetPathLength - _totalDist);
                            float maxLob = layer.MaxLobHeight > 0 ? layer.MaxLobHeight : 150f;
                            lobHeight = Math.Clamp(requiredExtraLength / 2.5f, layer.MinLobHeight, maxLob);
                        }

                        // Calculate the arc (0 to 1 to 0)
                        float arc = 4f * progress * (1f - progress);
                        float yOffset = -lobHeight * arc;

                        _projectilePos = basePos + new Vector2(0, yOffset);
                        allProjectilesArrived = false;

                        // Scale particles based on the height of the lob
                        float scaleMultiplier = 1f + (arc * layer.LobScaleMultiplier);

                        var baseSize = _baseEmitterSizes[i];
                        _emitters[i].Settings.InitialSize = new FloatRange(baseSize.Min * scaleMultiplier, baseSize.Max * scaleMultiplier);

                        if (i < _trailEmitters.Count && _trailEmitters[i] != null)
                        {
                            var baseTrailSize = _baseTrailSizes[i];
                            _trailEmitters[i].Settings.InitialSize = new FloatRange(baseTrailSize.Min * scaleMultiplier, baseTrailSize.Max * scaleMultiplier);
                        }
                    }

                    _emitters[i].Position = _projectilePos;
                    if (i < _trailEmitters.Count && _trailEmitters[i] != null) _trailEmitters[i].Position = _projectilePos;
                }
            }

            if (hasProjectiles && allProjectilesArrived && !HasTriggeredImpact)
            {
                HasTriggeredImpact = true;

                var psm = ServiceLocator.Get<ParticleSystemManager>();
                foreach (var layer in _layers)
                {
                    if (layer.Mode == "Projectile" && layer.Impacts != null)
                    {
                        foreach (var impactData in layer.Impacts)
                        {
                            var impactSettings = AnimationFactory.MapEmitterData(impactData);
                            var impactEmitter = psm.CreateEmitter(impactSettings);
                            impactEmitter.Position = _targetPos;
                            impactEmitter.EmitBurst(impactSettings.BurstCount > 0 ? impactSettings.BurstCount : 30);
                        }
                    }
                }
            }

            if (!hasProjectiles && !HasTriggeredImpact)
            {
                HasTriggeredImpact = true;
            }

            float maxDuration = _layers.Max(l => l.Duration);
            if (hasProjectiles)
            {
                float travelDuration = attack.Move.ChargeTime > 0 ? attack.Move.ChargeTime : 1.0f;
                maxDuration = Math.Max(maxDuration, travelDuration + 0.5f);
            }

            if (_timer >= maxDuration && (allProjectilesArrived || !hasProjectiles))
            {
                IsFinished = true;
                foreach (var e in _emitters) e.IsActive = false;
                foreach (var e in _trailEmitters) if (e != null) e.IsActive = false;
            }
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
        }
    }
}