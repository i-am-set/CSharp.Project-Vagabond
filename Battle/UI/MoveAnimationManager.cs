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
        private readonly Random _random = new Random();
        private BattleRenderer? _renderer;

        public bool IsAnimating => _activeAnimations.Any();

        public MoveAnimationManager()
        {
            _content = ServiceLocator.Get<Core>().Content;
            EventBus.Subscribe<GameEvents.PlayMoveAnimation>(OnPlayMoveAnimation);
            EventBus.Subscribe<GameEvents.BattleActionExecuted>(OnActionExecuted);
        }

        public void SetRenderer(BattleRenderer renderer)
        {
            _renderer = renderer;
        }

        private void OnPlayMoveAnimation(GameEvents.PlayMoveAnimation e)
        {
            if (_renderer == null)
            {
                Debug.WriteLine("[MoveAnimationManager] WARNING: Renderer not set. Cannot play animation.");
                return;
            }
            StartAnimation(e.Move, e.Targets, _renderer, e.GrazeStatus);
        }

        private void OnActionExecuted(GameEvents.BattleActionExecuted e)
        {
            if (_renderer == null) return;

            var grazeStatus = new Dictionary<BattleCombatant, bool>();
            if (e.Targets != null && e.DamageResults != null)
            {
                for (int i = 0; i < e.Targets.Count; i++)
                {
                    if (i < e.DamageResults.Count)
                    {
                        grazeStatus[e.Targets[i]] = e.DamageResults[i].WasGraze;
                    }
                }
            }

            StartAnimation(e.ChosenMove, e.Targets, _renderer, grazeStatus);
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
            if (string.IsNullOrEmpty(move.AnimationId)) return;

            if (!BattleDataCache.Animations.TryGetValue(move.AnimationId, out var animDef)) return;

            var animationData = GetAnimationData(animDef);
            if (animationData == null) return;

            if (!move.IsAnimationCentralized && (targets == null || !targets.Any())) return;

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
                    float grazeOffsetX = 0f;
                    if (grazeStatus != null && grazeStatus.TryGetValue(target, out bool isGraze) && isGraze)
                    {
                        float offset = (float)_random.Next(16, 33);
                        if (_random.Next(2) == 0) offset *= -1;
                        grazeOffsetX = offset;
                    }
                    Vector2 finalOffset = new Vector2(grazeOffsetX, 0);

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
            // Visual-only trigger (e.g. screen shake could go here if decoupled from BattleManager)
            // Currently empty as BattleManager handles logic immediately.
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

        public void CompleteCurrentAnimation()
        {
            _activeAnimations.Clear();
        }

        public void ForceClear()
        {
            SkipAll();
        }
    }
}