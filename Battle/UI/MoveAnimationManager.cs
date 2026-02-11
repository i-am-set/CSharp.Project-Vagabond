#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Manages the loading, caching, and playback of move animations during combat.
    /// </summary>
    public class MoveAnimationManager
    {
        private readonly Dictionary<string, MoveAnimation?> _animationCache = new();
        private readonly List<MoveAnimationInstance> _activeAnimations = new();
        private readonly ContentManager _content;
        private bool _impactSignalSentForCurrentBatch = false;
        private readonly Random _random = new Random();

        public bool IsAnimating => _activeAnimations.Any();

        public MoveAnimationManager()
        {
            _content = ServiceLocator.Get<Core>().Content;
        }

        private MoveAnimation? GetAnimationData(AnimationDefinition def)
        {
            if (_animationCache.TryGetValue(def.Id, out var cachedAnimation))
            {
                return cachedAnimation;
            }

            try
            {
                var texture = _content.Load<Texture2D>(def.TexturePath);
                // Pass the explicit dimensions from the JSON definition
                var animationData = new MoveAnimation(texture, def.FrameWidth, def.FrameHeight);
                _animationCache[def.Id] = animationData;
                return animationData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MoveAnimationManager] ERROR: Could not load animation texture '{def.TexturePath}' for ID '{def.Id}'. {ex.Message}");
                _animationCache[def.Id] = null;
                return null;
            }
        }

        /// <summary>
        /// Starts playing an animation for a given move and its targets.
        /// </summary>
        public void StartAnimation(MoveData move, List<BattleCombatant> targets, BattleRenderer renderer, Dictionary<BattleCombatant, bool>? grazeStatus = null)
        {
            _impactSignalSentForCurrentBatch = false;

            // Failsafe 1: No animation ID defined
            if (string.IsNullOrEmpty(move.AnimationId))
            {
                Debug.WriteLine($"[MoveAnimationManager] FAILSAFE: Move '{move.MoveName}' has no AnimationId defined. Firing impact immediately.");
                EventBus.Publish(new GameEvents.MoveImpactOccurred { Move = move });
                EventBus.Publish(new GameEvents.MoveAnimationCompleted());
                return;
            }

            // Lookup Definition
            if (!BattleDataCache.Animations.TryGetValue(move.AnimationId, out var animDef))
            {
                Debug.WriteLine($"[MoveAnimationManager] FAILSAFE: AnimationId '{move.AnimationId}' not found in BattleDataCache. Firing impact immediately.");
                EventBus.Publish(new GameEvents.MoveImpactOccurred { Move = move });
                EventBus.Publish(new GameEvents.MoveAnimationCompleted());
                return;
            }

            var animationData = GetAnimationData(animDef);

            // Failsafe 2: Animation load failed
            if (animationData == null)
            {
                Debug.WriteLine($"[MoveAnimationManager] FAILSAFE: Could not load texture for animation '{move.AnimationId}'. Firing impact immediately.");
                EventBus.Publish(new GameEvents.MoveImpactOccurred { Move = move });
                EventBus.Publish(new GameEvents.MoveAnimationCompleted());
                return;
            }

            // Failsafe 3: No targets and not centralized (would result in 0 instances)
            if (!move.IsAnimationCentralized && (targets == null || !targets.Any()))
            {
                Debug.WriteLine($"[MoveAnimationManager] FAILSAFE: Move '{move.MoveName}' is not centralized but has 0 targets. Firing impact immediately.");
                EventBus.Publish(new GameEvents.MoveImpactOccurred { Move = move });
                EventBus.Publish(new GameEvents.MoveAnimationCompleted());
                return;
            }

            float secondsPerFrame = 1.0f / Math.Max(1f, animDef.FPS);

            if (move.IsAnimationCentralized)
            {
                Func<Vector2> positionProvider = () => new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);

                var instance = new MoveAnimationInstance(animationData, positionProvider, secondsPerFrame, animDef.ImpactFrameIndex);
                instance.OnImpactFrameReached += () => HandleImpactTrigger(move);
                _activeAnimations.Add(instance);
            }
            else
            {
                foreach (var target in targets)
                {
                    // Calculate Graze Offset once per instance
                    float grazeOffsetX = 0f;
                    if (grazeStatus != null && grazeStatus.TryGetValue(target, out bool isGraze) && isGraze)
                    {
                        // Offset between 16 and 32 pixels, left or right
                        float offset = (float)_random.Next(16, 33);
                        if (_random.Next(2) == 0) offset *= -1;
                        grazeOffsetX = offset;
                    }
                    Vector2 finalOffset = new Vector2(grazeOffsetX, 0);

                    // Create a provider that fetches the current position every frame + the static graze offset
                    Func<Vector2> positionProvider = () =>
                        renderer.GetCombatantVisualCenterPosition(target, ServiceLocator.Get<BattleManager>().AllCombatants) + finalOffset;

                    var instance = new MoveAnimationInstance(animationData, positionProvider, secondsPerFrame, animDef.ImpactFrameIndex);
                    instance.OnImpactFrameReached += () => HandleImpactTrigger(move);
                    _activeAnimations.Add(instance);
                }
            }
        }

        private void HandleImpactTrigger(MoveData move)
        {
            // Ensure we only send the impact signal once per move execution, 
            // even if multiple instances (e.g. multi-target) trigger it.
            if (!_impactSignalSentForCurrentBatch)
            {
                EventBus.Publish(new GameEvents.MoveImpactOccurred { Move = move });
                _impactSignalSentForCurrentBatch = true;
            }
        }

        public void Update(GameTime gameTime)
        {
            for (int i = _activeAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeAnimations[i];
                anim.Update(gameTime);
                if (anim.IsFinished)
                {
                    _activeAnimations.RemoveAt(i);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            foreach (var anim in _activeAnimations)
            {
                anim.Draw(spriteBatch);
            }
        }

        public void SkipAll()
        {
            _activeAnimations.Clear();
        }

        /// <summary>
        /// Instantly completes all active animations, ensuring their impact logic fires.
        /// </summary>
        public void CompleteCurrentAnimation()
        {
            if (!_activeAnimations.Any()) return;

            // If impact hasn't fired yet, fire it now.
            if (!_impactSignalSentForCurrentBatch)
            {
                // We rely on the fact that BattleManager has a failsafe for pending impacts
                // if the animation completes without firing.
                // However, to be safe, we can try to trigger it if we had a reference,
                // but since we don't store the MoveData easily here without the closure,
                // we rely on the BattleManager's OnMoveAnimationCompleted check for _pendingImpact.
            }

            _activeAnimations.Clear();
        }

        public void ForceClear()
        {
            SkipAll();
        }
    }
}