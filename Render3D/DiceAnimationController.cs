using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Dice;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// Manages the entire visual animation sequence that occurs after dice have settled,
    /// including text, particles, and state transitions.
    /// </summary>
    public class DiceAnimationController
    {
        private enum AnimationState
        {
            Idle,
            PreEnumerationDelay,
            Enumerating,
            PostEnumerationDelay,
            ShiftingSums,
            GatheringResults,
            SpawningNewSum,
            PostSumDelay,
            ApplyingMultipliers,
            FinalSumHold,
            SequentialFadeOut,
            Complete
        }

        private class FloatingResultText
        {
            public enum TextType { IndividualDie, GroupSum, Multiplier, Modifier }

            public string Text;
            public Vector2 StartPosition;
            public Vector2 TargetPosition;
            public Vector2 CurrentPosition;
            public Vector2 ShakeOffset;
            public Color CurrentColor;
            public Color TintColor;
            public float Scale;
            public float Rotation;
            public TextType Type;
            public string GroupId;
            public float Age;
            public float Lifetime;
            public float AnimationProgress;
            public bool IsAnimating;
            public bool ShouldPopOnAnimate;
            public bool ImpactEffectTriggered;
            public bool IsAnimatingScale;
            public bool IsVisible;
            public bool IsFadingOut;
            public float FadeOutProgress;
            public bool IsAwaitingCollision;
            public bool IsColliding;
            public float CollisionProgress;

            // Properties for the box and trail animation
            public bool IsBox;
            public float ShrinkProgress;
            public float BoxRotation;
            public List<Vector2> TrailPoints = new List<Vector2>();
            public const int MaxTrailPoints = 15;
        }

        // Dependencies
        private Global _global;
        private readonly Random _random = new Random();
        private HapticsManager _hapticsManager;

        // State
        private AnimationState _currentState = AnimationState.Idle;
        private List<DiceGroup> _currentRollGroups;
        private float _animationTimer;
        private int _fadingSumIndex;
        private List<(RenderableDie die, int value)> _allSettledDice; // Stores the full list of dice for the entire animation sequence.

        // Animation Queues & Data
        private readonly Queue<string> _displayGroupQueue = new Queue<string>();
        private string _currentDisplayGroupId;
        private List<DiceGroup> _currentGroupsForDisplay;
        private readonly Queue<(RenderableDie die, int value)> _enumerationQueue = new Queue<(RenderableDie, int)>();
        private RenderableDie _currentlyEnumeratingDie;
        private int _currentlyEnumeratingDieValue;
        private int _currentGroupSum;
        private readonly List<FloatingResultText> _floatingResults = new List<FloatingResultText>();
        private readonly List<FloatingResultText> _groupSumResults = new List<FloatingResultText>();
        private readonly List<FloatingResultText> _activeModifiers = new List<FloatingResultText>();

        private const float INDIVIDUAL_DIE_TEXT_SCALE = 1.0f;
        private const float PRE_ENUMERATION_DELAY = 1.0f;

        // --- Animation Tuning ---
        private const float BAD_ROLL_ANIM_DURATION = 0.5f;
        private const float GOOD_ROLL_ANIM_DURATION = 0.5f;
        private const float NORMAL_ROLL_ANIM_DURATION = 0.25f;
        private const float SHRINK_AND_TEXT_DURATION = 0.15f;
        private const float GOOD_ROLL_WOBBLE_FREQUENCY = 40f;
        private const float GOOD_ROLL_WOBBLE_MAGNITUDE = 0.15f; // Radians


        public bool IsComplete => _currentState == AnimationState.Complete;
        public event Action OnAnimationComplete;

        public DiceAnimationController()
        {
            // Constructor is now safe and does nothing that requires services.
        }

        public void Initialize()
        {
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
        }

        public void Start(List<(RenderableDie die, int value)> settledDice, List<DiceGroup> rollGroups, RenderTarget2D renderTarget)
        {
            _currentRollGroups = rollGroups;
            _floatingResults.Clear();
            _groupSumResults.Clear();
            _activeModifiers.Clear();
            _enumerationQueue.Clear();
            _displayGroupQueue.Clear();
            _currentlyEnumeratingDie = null;
            _currentDisplayGroupId = null;
            _currentGroupsForDisplay = null;

            // Store the complete list of dice results. This is the key fix.
            _allSettledDice = new List<(RenderableDie die, int value)>(settledDice);

            var uniqueDisplayGroupIds = rollGroups
                .Select(g => g.DisplayGroupId ?? g.GroupId)
                .Distinct()
                .ToList();

            foreach (var displayId in uniqueDisplayGroupIds)
            {
                _displayGroupQueue.Enqueue(displayId);
            }

            _currentState = AnimationState.PreEnumerationDelay;
            _animationTimer = 0f;
        }

        public void Update(GameTime gameTime)
        {
            if (_currentState == AnimationState.Idle || _currentState == AnimationState.Complete) return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            UpdateFloatingResults(gameTime);

            var renderTarget = ServiceLocator.Get<DiceSceneRenderer>().RenderTarget;

            switch (_currentState)
            {
                case AnimationState.PreEnumerationDelay:
                    _animationTimer += deltaTime;
                    if (_animationTimer >= PRE_ENUMERATION_DELAY)
                    {
                        StartNextDisplayGroupEnumeration(renderTarget);
                    }
                    break;
                case AnimationState.Enumerating: UpdateEnumeratingState(deltaTime, renderTarget); break;
                case AnimationState.PostEnumerationDelay: UpdatePostEnumerationDelayState(deltaTime, renderTarget); break;
                case AnimationState.ShiftingSums: UpdateShiftingSumsState(deltaTime); break;
                case AnimationState.GatheringResults: UpdateGatheringState(deltaTime); break;
                case AnimationState.SpawningNewSum: UpdateSpawningNewSumState(deltaTime); break;
                case AnimationState.PostSumDelay:
                    _animationTimer += deltaTime;
                    if (_animationTimer >= _global.DicePostSumDelayDuration)
                    {
                        StartNextDisplayGroupEnumeration(renderTarget);
                    }
                    break;
                case AnimationState.ApplyingMultipliers: UpdateApplyingMultipliersState(deltaTime); break;
                case AnimationState.FinalSumHold: UpdateFinalSumHoldState(deltaTime); break;
                case AnimationState.SequentialFadeOut: UpdateSequentialFadeOutState(deltaTime); break;
            }
        }

        public void DrawOverlays(SpriteBatch spriteBatch, BitmapFont font)
        {
            var allFloatingText = _floatingResults.Concat(_groupSumResults).Concat(_activeModifiers).ToList();
            if (allFloatingText.Any() && font != null)
            {
                spriteBatch.Begin(sortMode: SpriteSortMode.BackToFront, blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
                var pixel = ServiceLocator.Get<Texture2D>();

                foreach (var result in allFloatingText)
                {
                    if (!result.IsVisible) continue;

                    // --- NEW: Trail Drawing Logic ---
                    if (result.IsBox && result.TrailPoints.Count > 1)
                    {
                        for (int i = 0; i < result.TrailPoints.Count - 1; i++)
                        {
                            Vector2 startPoint = result.TrailPoints[i];
                            Vector2 endPoint = result.TrailPoints[i + 1];

                            Vector2 delta = endPoint - startPoint;
                            if (delta.LengthSquared() < 0.01f) continue; // Skip zero-length segments

                            float distance = delta.Length();
                            float angle = (float)Math.Atan2(delta.Y, delta.X);

                            // Taper the thickness and fade the color along the trail's length
                            float progress = (float)i / result.TrailPoints.Count;
                            float thickness = MathHelper.Lerp(1f, 8f, progress); // Reversed taper as requested
                            Color trailColor = Color.Lerp(result.TintColor, Color.Transparent, progress * progress); // Fade faster

                            spriteBatch.Draw(
                                pixel,
                                startPoint,
                                null,
                                trailColor * 0.7f, // Make trail slightly transparent
                                angle,
                                new Vector2(0, 0.5f), // Origin at the start-center of the line
                                new Vector2(distance, thickness),
                                SpriteEffects.None,
                                0.2f // Draw trail behind the box
                            );
                        }
                    }

                    if (result.Type == FloatingResultText.TextType.IndividualDie && result.IsBox)
                    {
                        // Draw the rotating 8x8 box
                        spriteBatch.Draw(
                            pixel,
                            result.CurrentPosition,
                            null,
                            result.TintColor,
                            result.BoxRotation,
                            new Vector2(0.5f), // Center of the 1x1 pixel
                            8f, // Scale to 8x8
                            SpriteEffects.None,
                            0.1f // Draw box on top of trail
                        );
                    }
                    else
                    {
                        // Draw as text (handles shrinking text and other text types)
                        float currentScale = result.Scale;
                        if (result.Type == FloatingResultText.TextType.IndividualDie)
                        {
                            currentScale = MathHelper.Lerp(INDIVIDUAL_DIE_TEXT_SCALE, 0.0f, Easing.EaseInCubic(result.ShrinkProgress));
                        }

                        Color mainTextColor = result.TintColor != default ? result.TintColor : result.CurrentColor;

                        if (result.IsFadingOut)
                        {
                            float easedProgress = Easing.EaseInQuint(result.FadeOutProgress);
                            currentScale = MathHelper.Lerp(currentScale, 0f, easedProgress);
                            mainTextColor *= (1f - easedProgress);
                        }

                        if (currentScale <= 0.01f) continue; // Don't draw if invisible

                        Vector2 drawPosition = result.CurrentPosition + result.ShakeOffset;
                        Vector2 textSize = font.MeasureString(result.Text) * currentScale;
                        Vector2 textOrigin = new Vector2(textSize.X / (2 * currentScale), textSize.Y / (2 * currentScale));
                        Color outlineColor = Color.Black * mainTextColor.A;
                        float rotation = result.Rotation;
                        int outlineOffset = 1;

                        // Outline
                        for (int x = -1; x <= 1; x++)
                            for (int y = -1; y <= 1; y++)
                                if (x != 0 || y != 0)
                                    spriteBatch.DrawString(font, result.Text, drawPosition + new Vector2(x * outlineOffset, y * outlineOffset), outlineColor, rotation, textOrigin, currentScale, SpriteEffects.None, 0.1f);

                        // Main text
                        spriteBatch.DrawString(font, result.Text, drawPosition, mainTextColor, rotation, textOrigin, currentScale, SpriteEffects.None, 0f);
                    }
                }
                spriteBatch.End();
            }
        }

        private void StartNextDisplayGroupEnumeration(RenderTarget2D renderTarget)
        {
            if (_displayGroupQueue.TryDequeue(out _currentDisplayGroupId))
            {
                _currentState = AnimationState.Enumerating;
                _currentGroupSum = 0;
                _animationTimer = 0f;
                _enumerationQueue.Clear();

                _currentGroupsForDisplay = _currentRollGroups
                    .Where(g => (g.DisplayGroupId ?? g.GroupId) == _currentDisplayGroupId)
                    .ToList();

                var groupIds = _currentGroupsForDisplay.Select(g => g.GroupId).ToHashSet();

                // Always filter from the original, complete list of settled dice.
                var diceInDisplayGroup = _allSettledDice
                    .Where(item => groupIds.Contains(item.die.GroupId))
                    .OrderBy(item => item.die.World.Translation.X)
                    .ToList();

                foreach (var item in diceInDisplayGroup)
                {
                    _enumerationQueue.Enqueue(item);
                }

                ProcessNextEnumerationStep(renderTarget);
            }
            else
            {
                _currentState = AnimationState.ApplyingMultipliers;
                _animationTimer = 0f;
                PrepareMultipliers();
            }
        }

        private void ProcessNextEnumerationStep(RenderTarget2D renderTarget)
        {
            if (_enumerationQueue.TryDequeue(out var item))
            {
                _currentlyEnumeratingDie = item.die;
                _currentlyEnumeratingDieValue = item.value;
                _currentlyEnumeratingDie.VisualOffset = Vector3.Zero; // Reset offset for the new die
                _currentGroupSum += item.value;

                var group = _currentGroupsForDisplay.First(g => g.GroupId == item.die.GroupId);

                var renderer = ServiceLocator.Get<DiceSceneRenderer>();
                var dieWorldPos = _currentlyEnumeratingDie.World.Translation;
                var viewport = new Viewport(renderTarget.Bounds);
                var dieScreenPos = viewport.Project(dieWorldPos, renderer.Projection, renderer.View, Matrix.Identity);
                var dieScreenPos2D = new Vector2(dieScreenPos.X, dieScreenPos.Y);

                // --- Trigger Particle Effect based on Roll Quality ---
                bool isNarrativeRoll = _currentlyEnumeratingDie.GroupId == "narrative_check" && _currentlyEnumeratingDie.DieType == DieType.D6;
                string effectToPlay;

                if (isNarrativeRoll)
                {
                    if (_currentlyEnumeratingDieValue >= 5) // Good
                    {
                        effectToPlay = "CreateGoodRollParticles";
                    }
                    else if (_currentlyEnumeratingDieValue <= 2) // Bad
                    {
                        effectToPlay = "CreateBadRollParticles";
                    }
                    else // Neutral
                    {
                        effectToPlay = "CreateNeutralRollParticles";
                    }
                }
                else // Default for non-narrative rolls
                {
                    effectToPlay = "CreateNeutralRollParticles";
                }

                FXManager.Play(effectToPlay, dieScreenPos2D);

                if (group.ShowResultText)
                {
                    bool shouldAnimateSum = _currentGroupsForDisplay.Any(g => g.AnimateSum);
                    float lifetime = shouldAnimateSum ? _global.DiceGatheringDuration : 1.5f;

                    _floatingResults.Add(new FloatingResultText
                    {
                        Text = item.value.ToString(),
                        StartPosition = dieScreenPos2D,
                        TargetPosition = dieScreenPos2D,
                        CurrentPosition = dieScreenPos2D,
                        Scale = 0.0f,
                        Age = 0f,
                        Lifetime = lifetime,
                        Type = FloatingResultText.TextType.IndividualDie,
                        IsAnimatingScale = true,
                        CurrentColor = Color.White,
                        TintColor = item.die.Tint,
                        IsVisible = true
                    });
                }
            }
            else
            {
                _currentlyEnumeratingDie = null;
                bool shouldAnimateSum = _currentGroupsForDisplay.Any(g => g.AnimateSum);

                if (shouldAnimateSum)
                {
                    _currentState = AnimationState.PostEnumerationDelay;
                    _animationTimer = 0f;
                }
                else
                {
                    if (_displayGroupQueue.Any())
                    {
                        StartNextDisplayGroupEnumeration(renderTarget);
                    }
                    else
                    {
                        if (!_floatingResults.Any())
                        {
                            _currentState = AnimationState.Complete;
                            OnAnimationComplete?.Invoke();
                        }
                        else
                        {
                            _currentState = AnimationState.FinalSumHold;
                            _animationTimer = 0f;
                        }
                    }
                }
            }
        }

        private void UpdateEnumeratingState(float deltaTime, RenderTarget2D renderTarget)
        {
            _animationTimer += deltaTime;

            if (_currentlyEnumeratingDie != null)
            {
                // Determine animation properties based on roll quality
                bool isNarrativeRoll = _currentlyEnumeratingDie.GroupId == "narrative_check" && _currentlyEnumeratingDie.DieType == DieType.D6;
                float mainAnimDuration;
                Color flashColor = Color.White;

                if (isNarrativeRoll)
                {
                    if (_currentlyEnumeratingDieValue >= 5) // Good
                    {
                        mainAnimDuration = GOOD_ROLL_ANIM_DURATION;
                        flashColor = _global.Palette_LightGreen;
                    }
                    else if (_currentlyEnumeratingDieValue <= 2) // Bad
                    {
                        mainAnimDuration = BAD_ROLL_ANIM_DURATION;
                        flashColor = _global.Palette_Red;
                    }
                    else // Neutral
                    {
                        mainAnimDuration = NORMAL_ROLL_ANIM_DURATION;
                        flashColor = _global.Palette_Yellow;
                    }
                }
                else // Not a narrative roll
                {
                    mainAnimDuration = NORMAL_ROLL_ANIM_DURATION;
                }

                float totalStepDuration = mainAnimDuration + SHRINK_AND_TEXT_DURATION;
                var resultText = _floatingResults.LastOrDefault(fr => fr.IsAnimatingScale);

                // --- Main Animation Phase (Pop, Shake, Pulse) ---
                if (_animationTimer < mainAnimDuration)
                {
                    float progress = _animationTimer / mainAnimDuration;
                    _currentlyEnumeratingDie.IsHighlighted = true;

                    // Color decays over the main animation duration
                    _currentlyEnumeratingDie.HighlightColor = Color.Lerp(flashColor, _currentlyEnumeratingDie.Tint, Easing.EaseInQuad(progress));

                    // Reset visual modifiers before applying new ones for the current frame
                    _currentlyEnumeratingDie.VisualWobbleRotation = 0f;
                    _currentlyEnumeratingDie.VisualOffset = Vector3.Zero;

                    if (isNarrativeRoll && _currentlyEnumeratingDieValue >= 5) // Good Roll
                    {
                        const float inflateDuration = 0.15f;
                        const float holdDuration = 0.2f;
                        const float deflateDuration = 0.15f;

                        if (progress < inflateDuration / mainAnimDuration)
                        {
                            float phaseProgress = progress / (inflateDuration / mainAnimDuration);
                            _currentlyEnumeratingDie.VisualScale = 1.0f + (_global.DiceEnumerationMaxScale - 1.0f) * Easing.EaseOutCubic(phaseProgress);
                        }
                        else if (progress < (inflateDuration + holdDuration) / mainAnimDuration)
                        {
                            _currentlyEnumeratingDie.VisualScale = _global.DiceEnumerationMaxScale;
                            float phaseProgress = (progress - (inflateDuration / mainAnimDuration)) / (holdDuration / mainAnimDuration);
                            float wobbleDecay = 1.0f - Easing.EaseInQuad(phaseProgress); // Wobble fades out during the hold
                            _currentlyEnumeratingDie.VisualWobbleRotation = MathF.Sin(phaseProgress * GOOD_ROLL_WOBBLE_FREQUENCY) * GOOD_ROLL_WOBBLE_MAGNITUDE * wobbleDecay;
                        }
                        else
                        {
                            float phaseProgress = (progress - ((inflateDuration + holdDuration) / mainAnimDuration)) / (deflateDuration / mainAnimDuration);
                            _currentlyEnumeratingDie.VisualScale = 1.0f + (_global.DiceEnumerationMaxScale - 1.0f) * (1.0f - Easing.EaseInCubic(phaseProgress));
                        }
                    }
                    else if (isNarrativeRoll && _currentlyEnumeratingDieValue <= 2) // Bad Roll Shake
                    {
                        const float holdStart = 0.2f;
                        const float holdEnd = 0.8f;
                        const float shakeFrequency = 50f;
                        const float shakeMagnitude = 0.1f;
                        float scale;

                        if (progress < holdStart) // Inflate
                        {
                            scale = 1.0f + (_global.DiceEnumerationMaxScale - 1.0f) * Easing.EaseOutCubic(progress / holdStart);
                        }
                        else if (progress < holdEnd) // Hold and Shake
                        {
                            scale = _global.DiceEnumerationMaxScale;
                            float holdProgress = (progress - holdStart) / (holdEnd - holdStart);
                            float currentShakeMagnitude = shakeMagnitude * (1.0f - Easing.EaseInQuad(holdProgress)); // Shake decays
                            _currentlyEnumeratingDie.VisualOffset = new Vector3(
                                (float)(_random.NextDouble() * 2 - 1) * currentShakeMagnitude,
                                (float)(_random.NextDouble() * 2 - 1) * currentShakeMagnitude,
                                0);
                        }
                        else // Deflate
                        {
                            scale = 1.0f + (_global.DiceEnumerationMaxScale - 1.0f) * (1.0f - Easing.EaseInCubic((progress - holdEnd) / (1.0f - holdEnd)));
                        }
                        _currentlyEnumeratingDie.VisualScale = scale;
                    }
                    else // Neutral / Normal Roll Pop
                    {
                        float scale = 1.0f + (_global.DiceEnumerationMaxScale - 1.0f) * (progress < 0.5f ? Easing.EaseOutCubic(progress * 2f) : (1 - Easing.EaseInCubic((progress - 0.5f) * 2f)));
                        _currentlyEnumeratingDie.VisualScale = scale;
                    }
                }
                // --- Shrink & Text Appear Phase ---
                else if (_animationTimer < totalStepDuration)
                {
                    _currentlyEnumeratingDie.IsHighlighted = false;
                    _currentlyEnumeratingDie.VisualOffset = Vector3.Zero;
                    _currentlyEnumeratingDie.VisualWobbleRotation = 0f;

                    float outroProgress = (_animationTimer - mainAnimDuration) / SHRINK_AND_TEXT_DURATION;

                    // Shrink die
                    _currentlyEnumeratingDie.VisualScale = 1.0f - Easing.EaseInQuint(outroProgress);

                    // Scale up text
                    if (resultText != null)
                    {
                        resultText.Scale = INDIVIDUAL_DIE_TEXT_SCALE * Easing.EaseOutBack(outroProgress);
                    }
                }
                // --- Step Complete ---
                else
                {
                    if (_currentlyEnumeratingDie != null)
                    {
                        _currentlyEnumeratingDie.VisualScale = 0f;
                        _currentlyEnumeratingDie.IsDespawned = true;
                        if (resultText != null)
                        {
                            resultText.Scale = INDIVIDUAL_DIE_TEXT_SCALE;
                            resultText.IsAnimatingScale = false;
                        }
                    }
                    _animationTimer = 0f;
                    ProcessNextEnumerationStep(renderTarget);
                }
            }
        }

        private void UpdatePostEnumerationDelayState(float deltaTime, RenderTarget2D renderTarget)
        {
            _animationTimer += deltaTime;
            float shrinkDuration = 0.3f; // Give it a bit of time to shrink
            float progress = Math.Clamp(_animationTimer / shrinkDuration, 0f, 1f);

            bool allShrunk = progress >= 1.0f;

            foreach (var result in _floatingResults)
            {
                result.ShrinkProgress = progress;
                if (allShrunk)
                {
                    result.IsBox = true;
                }
            }

            if (allShrunk)
            {
                var font = ServiceLocator.Get<BitmapFont>();
                int totalModifier = _currentGroupsForDisplay.Sum(g => g.Modifier);
                string newSumText = (_currentGroupSum + totalModifier).ToString();
                var newSum = new FloatingResultText { Text = newSumText, Type = FloatingResultText.TextType.GroupSum, GroupId = _currentDisplayGroupId, TintColor = _currentGroupsForDisplay.First().Tint, Scale = 3.5f, IsVisible = false };
                var allSumsForLayout = _groupSumResults.Concat(new[] { newSum }).ToList();
                float totalWidth = allSumsForLayout.Sum(s => (font.MeasureString(s.Text).Width * s.Scale) + 50f) - 50f;
                float currentX = (renderTarget.Width / 2f) - (totalWidth / 2f);

                foreach (var sum in allSumsForLayout)
                {
                    float textWidth = font.MeasureString(sum.Text).Width * sum.Scale;
                    sum.StartPosition = sum.CurrentPosition;
                    sum.TargetPosition = new Vector2(currentX + textWidth / 2f, renderTarget.Height / 2f);
                    sum.IsAnimating = true;
                    sum.AnimationProgress = 0f;
                    sum.ShouldPopOnAnimate = false;
                    currentX += textWidth + 50f;
                }

                _groupSumResults.Add(newSum);
                _animationTimer = 0f;
                _currentState = AnimationState.ShiftingSums;
            }
        }

        private void UpdateShiftingSumsState(float deltaTime)
        {
            _animationTimer += deltaTime;
            float progress = Math.Clamp(_animationTimer / _global.DiceSumShiftDuration, 0f, 1f);

            // Animate existing sums shifting
            foreach (var sum in _groupSumResults.Where(s => s.IsVisible && s.IsAnimating))
            {
                sum.AnimationProgress = progress;
                if (progress >= 1.0f) sum.IsAnimating = false;
            }

            // Add slow spin to the new boxes while they wait
            foreach (var result in _floatingResults.Where(r => r.IsBox))
            {
                result.BoxRotation += 5f * deltaTime; // Half speed spin
            }

            if (progress >= 1.0f) { _animationTimer = 0f; _currentState = AnimationState.GatheringResults; }
        }

        private void UpdateGatheringState(float deltaTime)
        {
            _animationTimer += deltaTime;
            float progress = Math.Clamp(_animationTimer / _global.DiceGatheringDuration, 0f, 1f);
            float easedProgress = Easing.EaseInQuint(progress);
            var targetPosition = _groupSumResults.Last().TargetPosition;

            foreach (var result in _floatingResults)
            {
                result.CurrentPosition = Vector2.Lerp(result.StartPosition, targetPosition, easedProgress);
                result.BoxRotation += 10f * deltaTime; // Spin the box

                // Update the trail
                result.TrailPoints.Add(result.CurrentPosition);
                if (result.TrailPoints.Count > FloatingResultText.MaxTrailPoints)
                {
                    result.TrailPoints.RemoveAt(0);
                }
            }

            if (progress >= 1.0f)
            {
                // Clear trails and results
                foreach (var result in _floatingResults)
                {
                    result.TrailPoints.Clear();
                }
                _floatingResults.Clear();
                _animationTimer = 0f;
                _currentState = AnimationState.SpawningNewSum;
            }
        }

        private void UpdateSpawningNewSumState(float deltaTime)
        {
            _animationTimer += deltaTime;
            float totalDuration = _global.DiceNewSumInflateDuration + _global.DiceNewSumHoldDuration + _global.DiceNewSumDeflateDuration;
            float progress = Math.Clamp(_animationTimer / totalDuration, 0f, 1f);
            var newSum = _groupSumResults.Last();
            if (!newSum.IsVisible)
            {
                newSum.IsVisible = true;
                newSum.CurrentPosition = newSum.TargetPosition;
                newSum.StartPosition = newSum.TargetPosition;
                newSum.IsAnimating = true;
                newSum.ShouldPopOnAnimate = true;
                newSum.AnimationProgress = 0f;
            }
            if (newSum.IsAnimating)
            {
                newSum.AnimationProgress = progress;
                if (progress >= 1.0f) newSum.IsAnimating = false;
            }
            if (progress >= 1.0f) { _animationTimer = 0f; _currentState = AnimationState.PostSumDelay; }
        }

        private void PrepareMultipliers()
        {
            _activeModifiers.Clear();
            foreach (var sum in _groupSumResults)
            {
                var groupWithMultiplier = _currentRollGroups.FirstOrDefault(g => (g.DisplayGroupId ?? g.GroupId) == sum.GroupId && g.Multiplier != 1.0f);
                if (groupWithMultiplier != null)
                {
                    _activeModifiers.Add(new FloatingResultText { Text = $"x{groupWithMultiplier.Multiplier:0.0#}", Type = FloatingResultText.TextType.Multiplier, GroupId = sum.GroupId, Scale = 0f, TintColor = _global.Palette_Red, IsVisible = true, StartPosition = sum.CurrentPosition + new Vector2(0, -60), TargetPosition = sum.CurrentPosition, CurrentPosition = sum.CurrentPosition + new Vector2(0, -60), IsAnimating = true, AnimationProgress = 0f });
                    sum.IsAwaitingCollision = true;
                }
            }
            if (!_activeModifiers.Any()) { _currentState = AnimationState.FinalSumHold; _animationTimer = 0f; }
        }

        private void UpdateApplyingMultipliersState(float deltaTime)
        {
            _animationTimer += deltaTime;
            float progress = Math.Clamp(_animationTimer / _global.DiceMultiplierAnimationDuration, 0f, 1f);
            bool allDone = true;
            foreach (var multiplier in _activeModifiers.Where(m => m.Type == FloatingResultText.TextType.Multiplier && m.IsVisible))
            {
                allDone = false;
                multiplier.AnimationProgress = progress;
            }
            if (progress >= 1.0f && !allDone)
            {
                foreach (var multiplier in _activeModifiers.Where(m => m.Type == FloatingResultText.TextType.Multiplier && m.IsVisible))
                {
                    multiplier.IsVisible = false;
                    var targetSum = _groupSumResults.FirstOrDefault(s => s.GroupId == multiplier.GroupId);
                    if (targetSum != null) { targetSum.IsColliding = true; targetSum.CollisionProgress = 0f; }
                }
            }
            if (allDone && _groupSumResults.All(s => !s.IsColliding)) { _currentState = AnimationState.FinalSumHold; _animationTimer = 0f; }
        }

        private void UpdateFinalSumHoldState(float deltaTime)
        {
            // If there are group sums, we do the timed hold and then fade them.
            if (_groupSumResults.Any())
            {
                _animationTimer += deltaTime;
                if (_animationTimer >= _global.DiceFinalSumLifetime)
                {
                    _currentState = AnimationState.SequentialFadeOut;
                    _animationTimer = 0f;
                    _fadingSumIndex = 0;
                }
            }
            // If there are no group sums, it means we are in a "no-sum" animation.
            // We just wait for all the individual floating results to fade out on their own.
            else
            {
                if (!_floatingResults.Any(r => r.IsVisible))
                {
                    _currentState = AnimationState.Complete;
                    OnAnimationComplete?.Invoke();
                }
            }
        }

        private void UpdateSequentialFadeOutState(float deltaTime)
        {
            if (_fadingSumIndex >= _groupSumResults.Count)
            {
                if (!_groupSumResults.Any(s => s.IsFadingOut))
                {
                    _currentState = AnimationState.Complete;
                    OnAnimationComplete?.Invoke();
                }
                return;
            }
            _animationTimer += deltaTime;
            if (_animationTimer >= _global.DiceFinalSumSequentialFadeDelay)
            {
                if (_fadingSumIndex < _groupSumResults.Count)
                {
                    _groupSumResults[_fadingSumIndex].IsFadingOut = true;
                    _fadingSumIndex++;
                    _animationTimer = 0f;
                }
            }
        }

        private void UpdateFloatingResults(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var allResults = _floatingResults.Concat(_groupSumResults).Concat(_activeModifiers).ToList();
            foreach (var result in allResults)
            {
                result.Age += deltaTime;
                result.ShakeOffset = Vector2.Zero;
                result.Rotation = 0f;

                if (result.Lifetime > 0 && result.Age > result.Lifetime && !result.IsFadingOut)
                {
                    result.IsFadingOut = true;
                    result.FadeOutProgress = 0f;
                }

                if (result.IsFadingOut)
                {
                    result.FadeOutProgress = Math.Clamp(result.FadeOutProgress + deltaTime / _global.DiceFinalSumFadeOutDuration, 0f, 1f);
                    if (result.FadeOutProgress >= 1.0f)
                    {
                        result.IsVisible = false;
                    }
                }

                if (result.Type == FloatingResultText.TextType.Multiplier && result.IsAnimating)
                {
                    const float popInDuration = 0.3f, holdDuration = 0.5f;
                    float popInEnd = popInDuration / _global.DiceMultiplierAnimationDuration, holdEnd = (popInDuration + holdDuration) / _global.DiceMultiplierAnimationDuration;
                    if (result.AnimationProgress < popInEnd) result.Scale = 2.5f * Easing.EaseOutBack(result.AnimationProgress / popInEnd);
                    else if (result.AnimationProgress < holdEnd) result.Scale = 2.5f;
                    else
                    {
                        float phaseProgress = (result.AnimationProgress - holdEnd) / (1.0f - holdEnd);
                        result.CurrentPosition = Vector2.Lerp(result.StartPosition, result.TargetPosition, Easing.EaseInQuint(phaseProgress));
                        result.Scale = MathHelper.Lerp(2.5f, 0f, Easing.EaseInQuint(phaseProgress));
                    }
                }
                else if (result.Type == FloatingResultText.TextType.GroupSum)
                {
                    if (result.IsColliding)
                    {
                        result.CollisionProgress = Math.Clamp(result.CollisionProgress + deltaTime / 0.4f, 0f, 1f);
                        float popProgress = result.CollisionProgress;
                        if (popProgress < 0.5f) result.Scale = MathHelper.Lerp(3.5f, 1.75f, Easing.EaseInCubic(popProgress * 2f));
                        else
                        {
                            if (result.IsAwaitingCollision)
                            {
                                result.IsAwaitingCollision = false;
                                var groupWithMultiplier = _currentRollGroups.FirstOrDefault(g => (g.DisplayGroupId ?? g.GroupId) == result.GroupId && g.Multiplier != 1.0f);
                                if (groupWithMultiplier != null)
                                {
                                    float multipliedValue = int.Parse(result.Text) * groupWithMultiplier.Multiplier;
                                    result.Text = (groupWithMultiplier.Multiplier < 1.0f ? Math.Floor(multipliedValue) : Math.Ceiling(multipliedValue)).ToString();
                                }
                            }
                            result.Scale = MathHelper.Lerp(1.75f, 3.5f, Easing.EaseOutBack((popProgress - 0.5f) * 2f));
                        }
                        if (result.CollisionProgress >= 1.0f) { result.IsColliding = false; result.Scale = 3.5f; }
                    }
                    else if (result.IsAnimating)
                    {
                        result.CurrentPosition = Vector2.Lerp(result.StartPosition, result.TargetPosition, Easing.EaseOutCirc(result.AnimationProgress));
                        if (result.ShouldPopOnAnimate)
                        {
                            float popProgress = result.AnimationProgress;
                            float totalDuration = _global.DiceNewSumInflateDuration + _global.DiceNewSumHoldDuration + _global.DiceNewSumDeflateDuration;
                            float inflateEndTime = _global.DiceNewSumInflateDuration / totalDuration;
                            float holdEndTime = (_global.DiceNewSumInflateDuration + _global.DiceNewSumHoldDuration) / totalDuration;

                            if (popProgress <= inflateEndTime) result.Scale = 3.5f + (5.25f - 3.5f) * Easing.EaseOutCubic(popProgress / inflateEndTime);
                            else if (popProgress <= holdEndTime) { result.Scale = 5.25f; result.Rotation = (float)(_random.NextDouble() * 2 - 1) * 0.05f; }
                            else result.Scale = 5.25f - (5.25f - 3.5f) * Easing.EaseInCubic((popProgress - holdEndTime) / (1.0f - holdEndTime));
                        }
                    }
                    else result.Scale = 3.5f;

                    if (result.ShouldPopOnAnimate && result.AnimationProgress >= 1.0f && !result.ImpactEffectTriggered)
                    {
                        result.ImpactEffectTriggered = true;
                        _hapticsManager.TriggerZoomPulse(0.98f, 0.1f);
                    }
                }
            }

            // Cleanup invisible results
            _floatingResults.RemoveAll(r => !r.IsVisible);
            _groupSumResults.RemoveAll(r => !r.IsVisible);
            _activeModifiers.RemoveAll(r => !r.IsVisible);
        }
    }
}