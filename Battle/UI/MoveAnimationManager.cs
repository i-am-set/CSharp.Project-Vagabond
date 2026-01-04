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

        private MoveAnimation? GetAnimationData(string animationName)
        {
            if (_animationCache.TryGetValue(animationName, out var cachedAnimation))
            {
                return cachedAnimation;
            }

            try
            {
                var texture = _content.Load<Texture2D>($"Sprites/MoveAnimationSpriteSheets/{animationName}");
                var animationData = new MoveAnimation(texture);
                _animationCache[animationName] = animationData;
                return animationData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MoveAnimationManager] ERROR: Could not load animation sprite sheet '{animationName}'. {ex.Message}");
                _animationCache[animationName] = null;
                return null;
            }
        }

        /// <summary>
        /// Starts playing an animation for a given move and its targets.
        /// </summary>
        public void StartAnimation(MoveData move, List<BattleCombatant> targets, BattleRenderer renderer, Dictionary<BattleCombatant, bool>? grazeStatus = null)
        {
            _impactSignalSentForCurrentBatch = false;

            if (string.IsNullOrEmpty(move.AnimationSpriteSheet))
            {
                // If no animation, fire impact immediately
                EventBus.Publish(new GameEvents.MoveImpactOccurred { Move = move });
                return;
            }

            var animationData = GetAnimationData(move.AnimationSpriteSheet);

            if (animationData == null)
            {
                animationData = GetAnimationData("debug_null_animation");
                if (animationData == null)
                {
                    // Fallback if even debug animation fails
                    EventBus.Publish(new GameEvents.MoveImpactOccurred { Move = move });
                    return;
                }
            }

            if (move.IsAnimationCentralized)
            {
                var position = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);
                var instance = new MoveAnimationInstance(animationData, position, move.AnimationSpeed, move.DamageFrameIndex);
                instance.OnImpactFrameReached += () => HandleImpactTrigger(move);
                _activeAnimations.Add(instance);
            }
            else
            {
                foreach (var target in targets)
                {
                    var position = renderer.GetCombatantVisualCenterPosition(target, ServiceLocator.Get<BattleManager>().AllCombatants);

                    // Apply Graze Offset if applicable
                    if (grazeStatus != null && grazeStatus.TryGetValue(target, out bool isGraze) && isGraze)
                    {
                        // Offset between 16 and 32 pixels, left or right
                        float offset = (float)_random.Next(16, 33);
                        if (_random.Next(2) == 0) offset *= -1;
                        position.X += offset;
                    }

                    var instance = new MoveAnimationInstance(animationData, position, move.AnimationSpeed, move.DamageFrameIndex);
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
            // We check the flag because multiple instances might exist for one move.
            if (!_impactSignalSentForCurrentBatch)
            {
                // We need the MoveData to fire the event.
                // Since MoveAnimationInstance doesn't store MoveData directly, we rely on the closure
                // created in StartAnimation. However, we can't access that here easily.
                // BUT, the instances have an event OnImpactFrameReached.
                // We can just trigger that on the first active instance.

                var firstInstance = _activeAnimations.First();
                // This will invoke HandleImpactTrigger via the event subscription
                // We simulate reaching the frame.
                // Note: This relies on the event handler being robust (which it is, checking _impactSignalSentForCurrentBatch).

                // Force update the instance to a state where it triggers? 
                // Or just clear them and assume the BattleManager handles the state transition?
                // Actually, BattleManager waits for MoveAnimationCompleted.
                // If we clear the list, BattleScene fires MoveAnimationCompleted.
                // BUT BattleManager expects MoveImpactOccurred to have happened first to apply damage.

                // So we MUST trigger the impact logic.
                // Since we can't easily invoke the private event from outside without reflection or changing the class,
                // we will iterate and force update them with a huge delta time? No, that might overshoot.

                // Better approach: Just clear the list. 
                // Wait, if we clear the list, BattleScene fires MoveAnimationCompleted.
                // In BattleManager.OnMoveAnimationCompleted:
                // if (_pendingImpact != null) ApplyPendingImpact();

                // So BattleManager ALREADY has a failsafe!
                // If the animation completes (or is skipped), it checks for pending impact.
                // So we just need to clear the animations.
            }

            _activeAnimations.Clear();
        }

        public void ForceClear()
        {
            SkipAll();
        }
    }
}
