#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
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
        public void StartAnimation(MoveData move, List<BattleCombatant> targets, BattleRenderer renderer)
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

        public void ForceClear()
        {
            SkipAll();
        }
    }
}