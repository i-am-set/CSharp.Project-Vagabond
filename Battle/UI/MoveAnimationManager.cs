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
                // Cache null for the failed animation to prevent repeated load attempts.
                _animationCache[animationName] = null;
                return null;
            }
        }

        /// <summary>
        /// Starts playing an animation for a given move and its targets.
        /// </summary>
        public void StartAnimation(MoveData move, List<BattleCombatant> targets, BattleRenderer renderer)
        {
            if (string.IsNullOrEmpty(move.AnimationSpriteSheet))
            {
                return;
            }

            var animationData = GetAnimationData(move.AnimationSpriteSheet);

            // If the primary animation failed, try to get the fallback.
            if (animationData == null)
            {
                animationData = GetAnimationData("debug_null_animation");
                if (animationData == null)
                {
                    // If even the fallback fails, there's nothing to play.
                    return;
                }
            }

            if (move.IsAnimationCentralized)
            {
                // Centralized animations are anchored to the player's heart sprite.
                var position = renderer.PlayerSpritePosition;
                var instance = new MoveAnimationInstance(animationData, position, move.AnimationSpeed);
                _activeAnimations.Add(instance);
            }
            else
            {
                foreach (var target in targets)
                {
                    if (target.IsDefeated) continue;

                    var position = renderer.GetCombatantVisualCenterPosition(target, ServiceLocator.Get<BattleManager>().AllCombatants);

                    // If the target is the player, move the animation off-screen.
                    if (target.IsPlayerControlled)
                    {
                        position.Y += 300;
                    }

                    var instance = new MoveAnimationInstance(animationData, position, move.AnimationSpeed);
                    _activeAnimations.Add(instance);
                }
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
    }
}
#nullable restore