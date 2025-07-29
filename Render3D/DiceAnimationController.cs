using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Particles;
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

        // Particle Effects
        private ParticleSystemManager _particleManager;
        private ParticleEmitter _sparkEmitter;
        private ParticleEmitter _sumImpactEmitter;

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
        private int _currentGroupSum;
        private readonly List<FloatingResultText> _floatingResults = new List<FloatingResultText>();
        private readonly List<FloatingResultText> _groupSumResults = new List<FloatingResultText>();
        private readonly List<FloatingResultText> _activeModifiers = new List<FloatingResultText>();

        public bool IsComplete => _currentState == AnimationState.Complete;
        public event Action OnAnimationComplete;

        public DiceAnimationController()
        {
            // Constructor is now safe and does nothing that requires services.
        }

        public void Initialize()
        {
            _global = ServiceLocator.Get<Global>();
            _particleManager = new ParticleSystemManager();

            // Emitter creation is deferred to here, when we know the Texture2D service is available.
            _sparkEmitter = _particleManager.CreateEmitter(ParticleEffects.CreateSparks());
            _sumImpactEmitter = _particleManager.CreateEmitter(ParticleEffects.CreateSumImpact());
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

            StartNextDisplayGroupEnumeration(renderTarget);
        }

        public void Update(GameTime gameTime)
        {
            if (_currentState == AnimationState.Idle || _currentState == AnimationState.Complete) return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _particleManager.Update(gameTime);
            UpdateFloatingResults(gameTime);

            var renderTarget = ServiceLocator.Get<DiceSceneRenderer>().RenderTarget;

            switch (_currentState)
            {
                case AnimationState.Enumerating: UpdateEnumeratingState(deltaTime, renderTarget); break;
                case AnimationState.PostEnumerationDelay: UpdatePostEnumerationDelayState(deltaTime, renderTarget); break;
                case AnimationState.ShiftingSums: UpdateShiftingSumsState(deltaTime); break;
                case AnimationState.GatheringResults: UpdateGatheringState(deltaTime); break;
                case AnimationState.SpawningNewSum: UpdateSpawningNewSumState(deltaTime); break;
                case AnimationState.PostSumDelay: StartNextDisplayGroupEnumeration(renderTarget); break;
                case AnimationState.ApplyingMultipliers: UpdateApplyingMultipliersState(deltaTime); break;
                case AnimationState.FinalSumHold: UpdateFinalSumHoldState(deltaTime); break;
                case AnimationState.SequentialFadeOut: UpdateSequentialFadeOutState(deltaTime); break;
            }
        }

        public void DrawOverlays(SpriteBatch spriteBatch, BitmapFont font)
        {
            _particleManager.Draw(spriteBatch);

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
                            float distance = delta.Length();
                            float angle = (float)Math.Atan2(delta.Y, delta.X);

                            // Taper the thickness and fade the color along the trail's length
                            float progress = (float)i / result.TrailPoints.Count;
                            float thickness = MathHelper.Lerp(1f, 8f, progress);
                            Color trailColor = Color.Lerp(result.TintColor, Color.Transparent, progress);

                            spriteBatch.Draw(
                                pixel,
                                startPoint,
                                null,
                                trailColor,
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
                            currentScale = MathHelper.Lerp(4.0f, 0.0f, Easing.EaseInCubic(result.ShrinkProgress));
                        }

                        if (currentScale <= 0.01f) continue; // Don't draw if invisible

                        Vector2 drawPosition = result.CurrentPosition + result.ShakeOffset;
                        Vector2 textSize = font.MeasureString(result.Text) * currentScale;
                        Vector2 textOrigin = new Vector2(textSize.X / (2 * currentScale), textSize.Y / (2 * currentScale));
                        Color outlineColor = Color.Black;
                        Color mainTextColor = result.TintColor != default ? result.TintColor : result.CurrentColor;
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

        public void HandleDiceCollision(GameEvents.DiceCollisionOccurred e, Matrix view, Matrix projection, RenderTarget2D renderTarget)
        {
            var worldPos = new XnaVector3(e.WorldPosition.X, e.WorldPosition.Y, e.WorldPosition.Z);
            var viewport = new Viewport(renderTarget.Bounds);
            var screenPos3D = viewport.Project(worldPos, projection, view, Matrix.Identity);

            if (screenPos3D.Z < 0 || screenPos3D.Z > 1) return;

            _sparkEmitter.Position = new Vector2(screenPos3D.X, screenPos3D.Y);
            int burstCount = _random.Next(40, 71);
            for (int i = 0; i < burstCount; i++)
            {
                int pIndex = _sparkEmitter.EmitParticleAndGetIndex();
                if (pIndex == -1) break;
                ref var p = ref _sparkEmitter.GetParticle(pIndex);
                float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                float speed = _sparkEmitter.Settings.InitialVelocityX.GetValue(_random);
                p.Velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
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
                _currentGroupSum += item.value;

                var renderer = ServiceLocator.Get<DiceSceneRenderer>();
                var dieWorldPos = _currentlyEnumeratingDie.World.Translation;
                var viewport = new Viewport(renderTarget.Bounds);
                var dieScreenPos = viewport.Project(dieWorldPos, renderer.Projection, renderer.View, Matrix.Identity);
                var dieScreenPos2D = new Vector2(dieScreenPos.X, dieScreenPos.Y);

                _floatingResults.Add(new FloatingResultText
                {
                    Text = item.value.ToString(),
                    StartPosition = dieScreenPos2D,
                    TargetPosition = dieScreenPos2D,
                    CurrentPosition = dieScreenPos2D,
                    Scale = 0.0f,
                    Age = 0f,
                    Lifetime = _global.DiceGatheringDuration,
                    Type = FloatingResultText.TextType.IndividualDie,
                    IsAnimatingScale = true,
                    CurrentColor = Color.White,
                    TintColor = item.die.Tint,
                    IsVisible = true
                });
            }
            else
            {
                _currentlyEnumeratingDie = null;
                _currentState = AnimationState.PostEnumerationDelay;
                _animationTimer = 0f;
            }
        }

        private void UpdateEnumeratingState(float deltaTime, RenderTarget2D renderTarget)
        {
            _animationTimer += deltaTime;

            if (_currentlyEnumeratingDie != null)
            {
                float totalDuration = _global.DiceEnumerationStepDuration;
                float progress = Math.Clamp(_animationTimer / totalDuration, 0f, 1f);
                const float popDuration = 0.15f, shrinkDuration = 0.10f;
                float popPhaseEnd = popDuration / totalDuration, shrinkPhaseEnd = (popDuration + shrinkDuration) / totalDuration;
                var resultText = _floatingResults.LastOrDefault();

                if (progress < popPhaseEnd)
                {
                    float popProgress = progress / popPhaseEnd;
                    float scale = 1.0f + (_global.DiceEnumerationMaxScale - 1.0f) * (popProgress < 0.5f ? Easing.EaseOutCubic(popProgress * 2f) : (1 - Easing.EaseInCubic((popProgress - 0.5f) * 2f)));
                    _currentlyEnumeratingDie.VisualScale = scale;
                    _currentlyEnumeratingDie.IsHighlighted = true;
                    _currentlyEnumeratingDie.HighlightColor = _animationTimer < _global.DiceEnumerationFlashDuration ? Color.White : _currentlyEnumeratingDie.Tint;
                }
                else if (progress < shrinkPhaseEnd)
                {
                    float shrinkProgress = (progress - popPhaseEnd) / (shrinkPhaseEnd - popPhaseEnd);
                    _currentlyEnumeratingDie.IsHighlighted = false;
                    _currentlyEnumeratingDie.VisualScale = 1.0f - Easing.EaseInQuint(shrinkProgress);
                }
                else
                {
                    if (resultText != null && resultText.IsAnimatingScale)
                        resultText.Scale = 4.0f * Easing.EaseOutCubic((progress - shrinkPhaseEnd) / (1.0f - shrinkPhaseEnd));
                }
            }

            if (_animationTimer >= _global.DiceEnumerationStepDuration)
            {
                if (_currentlyEnumeratingDie != null)
                {
                    _currentlyEnumeratingDie.VisualScale = 0f;
                    _currentlyEnumeratingDie.IsDespawned = true;
                    var resultText = _floatingResults.LastOrDefault();
                    if (resultText != null) { resultText.Scale = 4.0f; resultText.IsAnimatingScale = false; }
                }
                _animationTimer = 0f;
                ProcessNextEnumerationStep(renderTarget);
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
            foreach (var sum in _groupSumResults.Where(s => s.IsVisible && s.IsAnimating))
            {
                sum.AnimationProgress = progress;
                if (progress >= 1.0f) sum.IsAnimating = false;
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
            float progress = Math.Clamp(_animationTimer / _global.DiceNewSumAnimationDuration, 0f, 1f);
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
            _animationTimer += deltaTime;
            if (_animationTimer >= _global.DiceFinalSumLifetime)
            {
                _currentState = AnimationState.SequentialFadeOut;
                _animationTimer = 0f;
                _fadingSumIndex = 0;
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
                    if (result.IsFadingOut)
                    {
                        result.FadeOutProgress = Math.Clamp(result.FadeOutProgress + deltaTime / _global.DiceFinalSumFadeOutDuration, 0f, 1f);
                        result.Scale = MathHelper.Lerp(3.5f, 0.0f, Easing.EaseInOutQuint(result.FadeOutProgress));
                        if (result.FadeOutProgress >= 1.0f) { result.IsFadingOut = false; result.IsVisible = false; }
                    }
                    else if (result.IsColliding)
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
                            const float inflateEndTime = 0.2f, holdEndTime = 0.7f;
                            if (popProgress <= inflateEndTime) result.Scale = 3.5f + (5.25f - 3.5f) * Easing.EaseOutCubic(popProgress / inflateEndTime);
                            else if (popProgress <= holdEndTime) { result.Scale = 5.25f; result.Rotation = (float)(_random.NextDouble() * 2 - 1) * 0.05f; }
                            else result.Scale = 5.25f - (5.25f - 3.5f) * Easing.EaseInCubic((popProgress - holdEndTime) / (1.0f - holdEndTime));
                        }
                    }
                    else result.Scale = 3.5f;

                    if (result.ShouldPopOnAnimate && result.AnimationProgress >= 1.0f && !result.ImpactEffectTriggered)
                    {
                        _sumImpactEmitter.Position = result.CurrentPosition;
                        _sumImpactEmitter.EmitBurst(50);
                        result.ImpactEffectTriggered = true;
                    }
                }
            }
        }
    }
}
