using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Animations;
using ProjectVagabond.Battle;
using ProjectVagabond.Particles;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class ArenaScene : GameScene
    {
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly GameState _gameState;
        private readonly InputManager _inputManager;
        private readonly SceneManager _sceneManager;
        private readonly TransitionManager _transitionManager;

        private readonly List<ArenaWizard> _wizards = new List<ArenaWizard>();
        private readonly List<ActiveAttack> _activeAttacks = new List<ActiveAttack>();
        private readonly List<ArenaWizard> _queryResults = new List<ArenaWizard>();
        private readonly Random _random = new Random();

        private float _stateTimer = 0f;
        private const float ARENA_RADIUS = 85f;
        private int _arenaEdges = 8;
        private Vector2 _arenaCenter;
        private Texture2D _arenaTexture;
        private Texture2D _arenaOutlineTexture;

        private bool _isMatchOver = false;
        private float _matchOverTimer = 0f;
        private string _matchResultText = "";
        private Color _matchResultColor = Color.White;
        private int _goldWon = 0;

        private float _hudBaseX;
        private float _hudNameX;
        private float _hudMultCenterX;

        // Multiplier Tuning
        private const float MULT_MIN = 2.0f;
        private const float MULT_MAX = 12.0f;
        private const int MULT_DEFAULT_MIN_STEP = 9; // Index 9 is Palette_Rust

        private readonly Dictionary<ArenaWizard, int> _multSteps = new Dictionary<ArenaWizard, int>();
        private readonly Dictionary<ArenaWizard, PlinkAnimator> _multPlinks = new Dictionary<ArenaWizard, PlinkAnimator>();
        private readonly Dictionary<ArenaWizard, float> _winProbabilities = new Dictionary<ArenaWizard, float>();

        public IReadOnlyList<ArenaWizard> Wizards => _wizards;

        public ArenaScene()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public float GetMaxRadiusAtAngle(float angle, float margin)
        {
            if (angle < 0) angle += MathHelper.TwoPi;
            float sectorAngle = MathHelper.TwoPi / _arenaEdges;
            float effectiveRadius = ARENA_RADIUS - margin;

            float apothem = effectiveRadius * MathF.Cos(sectorAngle / 2f);
            float localAngleEdge = Math.Abs((angle % sectorAngle) - (sectorAngle / 2f));
            float maxDistEdge = apothem / MathF.Cos(localAngleEdge);

            return maxDistEdge;
        }

        public Vector2 GetRandomArenaPoint()
        {
            Vector2 target;
            do
            {
                float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                float radius = ARENA_RADIUS * (float)Math.Sqrt(_random.NextDouble());
                target = _arenaCenter + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            }
            while (Vector2.Distance(_arenaCenter, target) > GetMaxRadiusAtAngle(MathF.Atan2(target.Y - _arenaCenter.Y, target.X - _arenaCenter.X), 12f));

            return target;
        }

        public List<ArenaWizard> GetWizardsInCircle(Vector2 center, float radius)
        {
            _queryResults.Clear();
            foreach (var w in _wizards)
            {
                if (w.State != WizardState.Dead && CollisionMath.RectangleIntersectsCircle(w.GetHitbox(_spriteManager), center, radius))
                {
                    _queryResults.Add(w);
                }
            }
            return _queryResults;
        }

        public List<ArenaWizard> GetWizardsInOBB(Vector2 origin, Vector2 direction, float width, float length)
        {
            _queryResults.Clear();
            foreach (var w in _wizards)
            {
                if (w.State != WizardState.Dead && CollisionMath.AABBIntersectsOBB(w.GetHitbox(_spriteManager), origin, direction, width, length))
                {
                    _queryResults.Add(w);
                }
            }
            return _queryResults;
        }

        public void SpawnAttack(ActiveAttack attack)
        {
            attack.DeliveryInstance.Start(attack);
            _activeAttacks.Add(attack);
        }

        public override void Enter()
        {
            base.Enter();
            _wizards.Clear();
            _activeAttacks.Clear();
            _multSteps.Clear();
            _multPlinks.Clear();
            _winProbabilities.Clear();

            _stateTimer = 0f;
            _isMatchOver = false;
            _matchOverTimer = 0f;
            _matchResultText = "";
            _goldWon = 0;

            _arenaCenter = new Vector2(Global.VIRTUAL_WIDTH - 4 - ARENA_RADIUS, Global.VIRTUAL_HEIGHT / 2f);
            _arenaEdges = Math.Max(3, _arenaEdges);

            _arenaTexture?.Dispose();
            _arenaOutlineTexture?.Dispose();

            _arenaOutlineTexture = ServiceLocator.Get<TextureFactory>().CreatePolygonTexture((int)ARENA_RADIUS + 2, _arenaEdges);
            _arenaTexture = ServiceLocator.Get<TextureFactory>().CreatePolygonTexture((int)ARENA_RADIUS, _arenaEdges);

            var playerLeader = _gameState.PlayerState.Leader;
            if (playerLeader == null) return;

            var availableIds = GameDataCache.WizardCats.Keys.ToList();

            var playerEntry = GameDataCache.WizardCats.FirstOrDefault(kvp => kvp.Value.Name == playerLeader.Name);
            if (playerEntry.Key != null)
            {
                availableIds.Remove(playerEntry.Key);
            }

            var selectedIds = availableIds.OrderBy(x => _random.Next()).Take(5).ToList();
            selectedIds.Insert(0, playerEntry.Key ?? "0");

            for (int i = 0; i < 6; i++)
            {
                string id = selectedIds[i];
                if (!GameDataCache.WizardCats.TryGetValue(id, out var data)) continue;

                float angle = (i / 6f) * MathHelper.TwoPi;
                float spawnRadius = GetMaxRadiusAtAngle(angle, 16f);
                Vector2 spawnPos = _arenaCenter + new Vector2(MathF.Cos(angle) * spawnRadius, MathF.Sin(angle) * spawnRadius);

                var wizard = new ArenaWizard();
                wizard.Initialize(data, spawnPos, i == 0);
                _wizards.Add(wizard);
            }

            int totalRating = _wizards.Sum(w => w.Rating);
            foreach (var w in _wizards)
            {
                if (totalRating > 0 && w.Rating > 0)
                {
                    float winProb = (float)w.Rating / totalRating;
                    _winProbabilities[w] = winProb;
                    float rawOdds = 1.0f / winProb;
                    w.PayoutMultiplier = (float)Math.Round(rawOdds * 0.9f, 1);
                }
                else
                {
                    _winProbabilities[w] = 0f;
                    w.PayoutMultiplier = 1.0f;
                }

                _multSteps[w] = GetMultiplierStep(w.PayoutMultiplier);
                _multPlinks[w] = new PlinkAnimator { MaxScale = 1.5f, PlinkTriggerThreshold = 0f };
            }

            CalculateHUDLayout();
        }

        private void CalculateHUDLayout()
        {
            var defaultFont = ServiceLocator.Get<Core>().DefaultFont;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

            int totalWizards = _wizards.Count;
            if (totalWizards == 0) return;

            int spacingY = 20;
            int itemHeight = (int)defaultFont.LineHeight + 7;

            int totalBlockHeight = (totalWizards - 1) * spacingY + itemHeight;
            int startY = (Global.VIRTUAL_HEIGHT - totalBlockHeight) / 2;

            float uiAreaWidth = Global.VIRTUAL_WIDTH - 4 - ARENA_RADIUS * 2;

            float maxNameWidth = 0f;
            foreach (var w in _wizards)
            {
                w.HudNameSize = defaultFont.MeasureString(w.Name.ToUpper());
                if (w.HudNameSize.X > maxNameWidth) maxNameWidth = w.HudNameSize.X;
            }

            int statBlockWidth = 19;
            float nameXOffset = statBlockWidth + 4;
            float multXOffset = nameXOffset + maxNameWidth + 8;

            float standardMultWidth = secondaryFont.MeasureString("9.9").Width + 1f + tertiaryFont.MeasureString("x").Width;
            float totalWidth = multXOffset + standardMultWidth + 10f;

            _hudBaseX = (uiAreaWidth - totalWidth) / 2f;
            if (_hudBaseX < 4) _hudBaseX = 4;

            _hudBaseX -= 5f;
            _hudNameX = _hudBaseX + nameXOffset;
            _hudMultCenterX = _hudBaseX + multXOffset + 7f + (standardMultWidth / 2f);

            for (int i = 0; i < totalWizards; i++)
            {
                var w = _wizards[i];
                w.HudIsLeft = true;

                float currentY = startY + i * spacingY;

                w.HudNamePos = new Vector2(_hudNameX, currentY);
                w.HudHeartStartPos = new Vector2(_hudNameX, currentY + w.HudNameSize.Y + 2);
            }
        }

        public override void Exit()
        {
            base.Exit();
            _arenaTexture?.Dispose();
            _arenaOutlineTexture?.Dispose();
        }

        public Vector2 ClampToArena(Vector2 point, float margin = 4f)
        {
            Vector2 fromCenter = point - _arenaCenter;
            if (fromCenter.LengthSquared() == 0) return point;

            float angle = MathF.Atan2(fromCenter.Y, fromCenter.X);
            float maxRadius = GetMaxRadiusAtAngle(angle, margin);

            if (fromCenter.Length() > maxRadius)
            {
                return _arenaCenter + Vector2.Normalize(fromCenter) * maxRadius;
            }

            return point;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _stateTimer += dt;

            foreach (var w in _wizards)
            {
                if (_multPlinks.TryGetValue(w, out var plink) && plink.IsActive)
                {
                    plink.Update(gameTime, Vector2.Zero);
                }
            }

            if (_stateTimer >= 3.0f)
            {
                Vector2 virtualMousePos = Core.TransformMouse(_inputManager.GetEffectiveMouseState().Position);

                foreach (var wizard in _wizards)
                {
                    wizard.IsHovered = wizard.State != WizardState.Dead && wizard.GetHitbox(_spriteManager).Contains(virtualMousePos);
                    wizard.Update(dt, this);
                }

                for (int i = 0; i < _wizards.Count; i++)
                {
                    var w1 = _wizards[i];
                    if (w1.State == WizardState.Dead) continue;

                    for (int j = i + 1; j < _wizards.Count; j++)
                    {
                        var w2 = _wizards[j];
                        if (w2.State == WizardState.Dead) continue;

                        var box1 = w1.GetHitbox(_spriteManager);
                        var box2 = w2.GetHitbox(_spriteManager);

                        if (box1.Intersects(box2))
                        {
                            Vector2 center1 = new Vector2(box1.Center.X, box1.Center.Y);
                            Vector2 center2 = new Vector2(box2.Center.X, box2.Center.Y);

                            Vector2 pushDir = center1 - center2;
                            if (pushDir.LengthSquared() == 0)
                            {
                                pushDir = new Vector2((float)_random.NextDouble() - 0.5f, (float)_random.NextDouble() - 0.5f);
                                if (pushDir.LengthSquared() == 0) pushDir = new Vector2(1, 0);
                            }
                            pushDir.Normalize();

                            if (w1.State == WizardState.Moving)
                            {
                                w1.TargetPosition = ClampToArena(w1.Position + pushDir * 50f, 12f);
                            }
                            if (w2.State == WizardState.Moving)
                            {
                                w2.TargetPosition = ClampToArena(w2.Position - pushDir * 50f, 12f);
                            }
                        }
                    }
                }

                for (int i = _activeAttacks.Count - 1; i >= 0; i--)
                {
                    var attack = _activeAttacks[i];
                    attack.Update(dt, this);

                    if (attack.IsFinished)
                    {
                        _activeAttacks.RemoveAt(i);
                    }
                }

                if (!_isMatchOver)
                {
                    UpdateDynamicOdds();

                    int aliveCount = _wizards.Count(w => w.State != WizardState.Dead);
                    if (aliveCount <= 1)
                    {
                        _isMatchOver = true;
                        _matchOverTimer = 4.0f;

                        var winner = _wizards.FirstOrDefault(w => w.State != WizardState.Dead);
                        if (winner != null && winner.IsPlayer)
                        {
                            _matchResultText = "VICTORY!";
                            _matchResultColor = _global.Palette_Sky;
                            _goldWon = (int)(_gameState.CurrentEntryFee * winner.PayoutMultiplier);
                            _gameState.PlayerState.Gold += _goldWon;
                        }
                        else
                        {
                            _matchResultText = "DEFEAT";
                            _matchResultColor = _global.Palette_Rust;
                            _goldWon = 0;
                        }

                        _gameState.AdvanceDay();
                    }
                }
                else
                {
                    _matchOverTimer -= dt;
                    if (_matchOverTimer <= 0 && !_transitionManager.IsTransitioning)
                    {
                        _sceneManager.ChangeScene(GameSceneState.DayPrep, TransitionType.FadeOff, TransitionType.FadeOff);
                    }
                }
            }

            if (_inputManager.Back)
            {
                _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff);
            }
        }

        private int GetMultiplierStep(float mult)
        {
            if (mult < 2.0f)
            {
                // Map 1.0 -> 1.99 to steps 0 -> 3
                float t = Math.Clamp((mult - 1.0f) / 1.0f, 0f, 1f);
                return Math.Clamp((int)(t * 4), 0, 3);
            }
            else
            {
                // Map 2.0 -> 12.0 to steps 4 -> 12
                float t = Math.Clamp((mult - 2.0f) / 10.0f, 0f, 1f);
                return 4 + Math.Clamp((int)(t * 9), 0, 8);
            }
        }

        private void UpdateDynamicOdds()
        {
            float totalDynamicRating = 0f;

            foreach (var w in _wizards)
            {
                if (w.State != WizardState.Dead && w.CurrentHP > 0)
                {
                    totalDynamicRating += w.Rating * ((float)w.CurrentHP / w.MaxHP);
                }
            }

            foreach (var w in _wizards)
            {
                if (w.State == WizardState.Dead || w.CurrentHP <= 0)
                {
                    w.PayoutMultiplier = 0.0f;
                    _winProbabilities[w] = 0f;
                }
                else if (totalDynamicRating > 0)
                {
                    float dynamicRating = w.Rating * ((float)w.CurrentHP / w.MaxHP);
                    if (dynamicRating > 0)
                    {
                        float winProb = dynamicRating / totalDynamicRating;
                        _winProbabilities[w] = winProb;
                        float rawOdds = 1.0f / winProb;

                        w.PayoutMultiplier = (float)Math.Round(Math.Max(1.1f, rawOdds * 0.65f), 1);
                    }
                    else
                    {
                        _winProbabilities[w] = 0f;
                    }
                }

                int step = GetMultiplierStep(w.PayoutMultiplier);

                if (_multSteps.TryGetValue(w, out int currentStep) && currentStep != step)
                {
                    if (currentStep != -1)
                    {
                        _multPlinks[w].Start(0f, 0.3f);
                    }
                    _multSteps[w] = step;
                }
            }
        }

        private Color GetMultiplierColor(int step)
        {
            switch (step)
            {
                case 0: return _global.Palette_Black;
                case 1: return _global.Palette_DarkShadow;
                case 2: return _global.Palette_DarkestPale;
                case 3: return _global.Palette_DarkPale;
                case 4: return _global.Palette_Pale;
                case 5: return _global.Palette_LightPale;
                case 6: return _global.Palette_Sun;
                case 7: return _global.Palette_DarkSun;
                case 8: return _global.Palette_Fruit;
                case 9: return _global.Palette_Rust;
                case 10: return _global.Palette_DarkRust;
                case 11: return _global.Palette_Sea;
                case 12: return _global.Palette_Sky;
                default: return _global.Palette_Sun;
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.GameBg);

            if (_arenaOutlineTexture != null)
            {
                Vector2 outlineOrigin = new Vector2(_arenaOutlineTexture.Width / 2f, _arenaOutlineTexture.Height / 2f);
                spriteBatch.DrawSnapped(_arenaOutlineTexture, _arenaCenter, null, _global.Palette_Black, 0f, outlineOrigin, 1f, SpriteEffects.None, 0f);
            }

            if (_arenaTexture != null)
            {
                Vector2 origin = new Vector2(_arenaTexture.Width / 2f, _arenaTexture.Height / 2f);
                spriteBatch.DrawSnapped(_arenaTexture, _arenaCenter, null, _global.GameBg, 0f, origin, 1f, SpriteEffects.None, 0f);
            }

            foreach (var attack in _activeAttacks)
            {
                attack.DeliveryInstance.Draw(spriteBatch, attack);
            }

            _wizards.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));

            foreach (var wizard in _wizards)
            {
                if (wizard.State == WizardState.Dead) DrawWizard(spriteBatch, wizard);
            }

            foreach (var wizard in _wizards)
            {
                if (wizard.State != WizardState.Dead) DrawWizard(spriteBatch, wizard);
            }

            foreach (var attack in _activeAttacks)
            {
                attack.Animation?.Draw(spriteBatch, attack);
            }

            spriteBatch.End();
            ServiceLocator.Get<ParticleSystemManager>().Draw(spriteBatch, transform);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            foreach (var wizard in _wizards)
            {
                wizard.DrawUI(spriteBatch, _spriteManager, gameTime);
                wizard.DrawDebug(spriteBatch, _spriteManager);
            }

            DrawSideHUD(spriteBatch);
            DrawTopHUD(spriteBatch);

            string text = "";
            Color textColor = _global.Palette_Sun;

            if (_stateTimer < 1f) text = "3";
            else if (_stateTimer < 2f) text = "2";
            else if (_stateTimer < 3f) text = "1";
            else if (_stateTimer < 4f) text = "FIGHT!";
            else if (_isMatchOver)
            {
                text = _matchResultText;
                textColor = _matchResultColor;
            }

            if (!string.IsNullOrEmpty(text))
            {
                var mainFont = ServiceLocator.Get<Core>().DefaultFont;
                Vector2 size = mainFont.MeasureString(text);
                Vector2 pos = _arenaCenter - (size / 2f);

                if (_isMatchOver) pos.Y -= 10;

                spriteBatch.DrawStringSnapped(mainFont, text, pos, textColor);

                if (_isMatchOver && _goldWon > 0)
                {
                    var secFont = ServiceLocator.Get<Core>().SecondaryFont;
                    string goldText = $"+{_goldWon}G";
                    Vector2 goldSize = secFont.MeasureString(goldText);
                    Vector2 goldPos = new Vector2(_arenaCenter.X - goldSize.X / 2f, pos.Y + size.Y + 4);
                    spriteBatch.DrawStringSnapped(secFont, goldText, goldPos, _global.Palette_Sun);
                }
            }
        }

        private void DrawTopHUD(SpriteBatch spriteBatch)
        {
            var core = ServiceLocator.Get<Core>();
            var tertiaryFont = core.TertiaryFont;
            var defaultFont = core.DefaultFont;

            string amountText = _gameState.PlayerState.Gold.ToString();
            string gText = "G";

            Vector2 amountPos = new Vector2(18, 12);
            spriteBatch.DrawStringSnapped(defaultFont, amountText, amountPos, _global.Palette_Sun);

            float amountWidth = defaultFont.MeasureString(amountText).Width;
            float yOffset = MathF.Max(0, defaultFont.LineHeight - tertiaryFont.LineHeight);

            Vector2 gPos = new Vector2(amountPos.X + amountWidth + 2, 11 + yOffset);
            spriteBatch.DrawStringSnapped(tertiaryFont, gText, gPos, _global.Palette_DarkSun);
        }

        private void DrawSideHUD(SpriteBatch spriteBatch)
        {
            var defaultFont = ServiceLocator.Get<Core>().DefaultFont;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
            var sheet = _spriteManager.HealthHeartsSpriteSheet;
            var pixel = ServiceLocator.Get<Texture2D>();
            if (sheet == null || _wizards.Count == 0) return;

            int heartWidth = 5;
            int heartSpacing = 1;

            foreach (var w in _wizards)
            {
                float shakeX = 0f;
                float shakeY = 0f;
                if (w.HudShakeTimer > 0)
                {
                    float shakeMag = (w.HudShakeTimer / 0.4f) * 3f;
                    shakeX = (float)(_random.NextDouble() * 2 - 1) * shakeMag;
                    shakeY = (float)(_random.NextDouble() * 2 - 1) * shakeMag;
                }

                Color baseNameColor = w.IsPlayer ? _global.Palette_Sky : _global.Palette_Sun;
                Color nameColor = baseNameColor;

                if (w.State == WizardState.Dead)
                {
                    float fadeProgress = Math.Clamp(w.TimeSinceDeath / 0.5f, 0f, 1f);
                    nameColor = Color.Lerp(baseNameColor, _global.Palette_Black, fadeProgress);
                }

                Vector2 finalNamePos = new Vector2(MathF.Round(w.HudNamePos.X + shakeX), MathF.Round(w.HudNamePos.Y + shakeY));
                spriteBatch.DrawStringSnapped(defaultFont, w.Name.ToUpper(), finalNamePos, nameColor);

                int statBlockWidth = 19; // 10 pips * 1px + 9 gaps * 1px
                int statBlockHeight = 5; // 3 rows * 1px + 2 gaps * 1px
                float statBlockX = MathF.Round(_hudBaseX + shakeX);
                float statBlockY = finalNamePos.Y + MathF.Max(0, (defaultFont.LineHeight - statBlockHeight) / 2f);

                int[] stats = { w.Power, w.Tenacity, w.Agility };
                for (int row = 0; row < 3; row++)
                {
                    int statVal = Math.Clamp(stats[row], 0, 10);
                    Color filledColor = statVal >= 8 ? _global.StatColor_High : (statVal >= 4 ? _global.StatColor_Average : _global.StatColor_Low);

                    float currentY = statBlockY + row * 2;

                    for (int col = 0; col < 10; col++)
                    {
                        float currentX = statBlockX + col * 2;
                        Color pipColor = col < statVal ? filledColor : _global.Palette_DarkShadow;

                        if (w.State == WizardState.Dead)
                        {
                            float fadeProgress = Math.Clamp(w.TimeSinceDeath / 0.5f, 0f, 1f);
                            pipColor = Color.Lerp(pipColor, _global.Palette_Black, fadeProgress);
                        }

                        spriteBatch.Draw(pixel, new Rectangle((int)currentX, (int)currentY, 1, 1), pipColor);
                    }
                }

                if (w.State != WizardState.Dead)
                {
                    int step = _multSteps.TryGetValue(w, out var s) ? s : 0;
                    Color multColor = GetMultiplierColor(step);
                    BitmapFont multFont = secondaryFont;

                    if (w.PayoutMultiplier < 2.0f) multFont = tertiaryFont;
                    else if (step >= MULT_DEFAULT_MIN_STEP) multFont = defaultFont;

                    string numText = $"{w.PayoutMultiplier:F1}";
                    string xText = "x";

                    var plink = _multPlinks[w];
                    float pScale = plink.IsActive ? plink.Scale : 1f;
                    float pRot = plink.IsActive ? plink.Rotation : 0f;

                    if (plink.IsActive && plink.FlashTint.HasValue)
                    {
                        float flashAmount = plink.FlashTint.Value.A / 255f;
                        multColor = Color.Lerp(multColor, Color.White, flashAmount);
                    }

                    Vector2 numSize = multFont.MeasureString(numText);
                    Vector2 xSize = tertiaryFont.MeasureString(xText);

                    float totalWidth = numSize.X + 1f + xSize.X;
                    Vector2 pivot = new Vector2(totalWidth / 2f, numSize.Y / 2f);

                    float numYAdjust = (multFont == defaultFont) ? 1f : 0f;
                    Vector2 numOrigin = pivot - new Vector2(0, numYAdjust);
                    Vector2 xOrigin = pivot - new Vector2(numSize.X + 1f, numSize.Y - xSize.Y);

                    float multYOffset = MathF.Max(0, (defaultFont.LineHeight - numSize.Y) / 2f);
                    Vector2 drawPos = new Vector2(MathF.Round(_hudMultCenterX + shakeX), MathF.Round(finalNamePos.Y + multYOffset + pivot.Y));

                    spriteBatch.DrawStringSnapped(multFont, numText, drawPos, multColor, pRot, numOrigin, pScale, SpriteEffects.None, 0f);
                    spriteBatch.DrawStringSnapped(tertiaryFont, xText, drawPos, multColor, pRot, xOrigin, pScale, SpriteEffects.None, 0f);

                    // Draw Win Probability Percentage
                    float prob = _winProbabilities.TryGetValue(w, out var p) ? p : 0f;
                    string probText = $"{(prob * 100f):F0}%";
                    Vector2 probSize = tertiaryFont.MeasureString(probText);
                    Vector2 probOrigin = new Vector2(probSize.X / 2f, 0);
                    Vector2 probPos = new Vector2(MathF.Round(_hudMultCenterX + shakeX), MathF.Round(w.HudHeartStartPos.Y + shakeY));

                    spriteBatch.DrawStringSnapped(tertiaryFont, probText, probPos, _global.Palette_Black, 0f, probOrigin, 1f, SpriteEffects.None, 0f);
                }

                if (w.State == WizardState.Dead)
                {
                    float fadeProgress = Math.Clamp(w.TimeSinceDeath / 0.5f, 0f, 1f);
                    int currentLineWidth = (int)(w.HudNameSize.X * fadeProgress);
                    if (currentLineWidth > 0)
                    {
                        int lineY = (int)MathF.Round(finalNamePos.Y + w.HudNameSize.Y / 2f);
                        spriteBatch.Draw(pixel, new Rectangle((int)finalNamePos.X, lineY, currentLineWidth, 1), _global.Palette_Black);
                    }
                }

                int maxHearts = (w.MaxHP + 2) / 3;
                for (int h = 0; h < maxHearts; h++)
                {
                    int heartVal = Math.Clamp(w.CurrentHP - h * 3, 0, 3);
                    int frameIndex = 3; // 0/3
                    if (heartVal == 3) frameIndex = 0; // 3/3
                    else if (heartVal == 2) frameIndex = 1; // 2/3
                    else if (heartVal == 1) frameIndex = 2; // 1/3

                    int flashFrame = w.GetHeartFlashFrame(h);
                    if (flashFrame != -1) frameIndex = flashFrame;

                    var sourceRect = new Rectangle(frameIndex * heartWidth, 0, heartWidth, 5);
                    Color heartColor = w.State == WizardState.Dead ? Color.Gray : Color.White;

                    int yOffset = 0;
                    if (w.CurrentHP > 0)
                    {
                        float localWaveTime = w.HudHeartWaveTimer - w.HudHeartWaveInterval - (h * 0.08f);
                        if (localWaveTime > 0 && localWaveTime < 0.15f)
                        {
                            yOffset = -1;
                        }
                    }

                    Vector2 finalHeartPos = new Vector2(MathF.Round(w.HudHeartStartPos.X + h * (heartWidth + heartSpacing) + shakeX), MathF.Round(w.HudHeartStartPos.Y + shakeY) + yOffset);
                    spriteBatch.DrawSnapped(sheet, finalHeartPos, sourceRect, heartColor);
                }
            }
        }

        private void DrawWizard(SpriteBatch spriteBatch, ArenaWizard wizard)
        {
            var sheet = _spriteManager.PlayerMasterSpriteSheet;
            if (sheet == null) return;

            Vector2 origin = new Vector2(16, 16);
            var sourceRect = _spriteManager.GetPlayerSourceRect(wizard.PortraitIndex, PlayerSpriteType.Portrait5x5);

            bool isDead = wizard.State == WizardState.Dead;
            float hopOffset = isDead ? 0f : -MathF.Abs(MathF.Sin(wizard.HopTimer)) * 4f;
            Vector2 drawPos = new Vector2(MathF.Round(wizard.Position.X), MathF.Round(wizard.Position.Y + hopOffset));
            float rotation = isDead ? MathHelper.PiOver2 : 0f;

            float alpha = wizard.GetDeathAlpha();
            Color color = isDead ? (Color.Gray * alpha) : Color.White;

            bool drawSilhouette = false;
            bool skipDraw = false;

            if (wizard.InvincibilityTimer > 0 && !isDead)
            {
                float timeActive = wizard.InvincibilityDuration - wizard.InvincibilityTimer;
                int state = (int)(timeActive / 0.05f) % 3;

                if (state == 0) drawSilhouette = true;
                else if (state == 1) skipDraw = true;
            }

            SpriteEffects spriteEffects = wizard.IsFacingRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            if (!skipDraw)
            {
                if (wizard.IsPlayer && !isDead)
                {
                    var silhouette = _spriteManager.PlayerMasterSpriteSheetSilhouette;
                    if (silhouette != null)
                    {
                        spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(-1, 0), sourceRect, _global.Palette_Sun, rotation, origin, 1f, spriteEffects, 0f);
                        spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(1, 0), sourceRect, _global.Palette_Sun, rotation, origin, 1f, spriteEffects, 0f);
                        spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(0, -1), sourceRect, _global.Palette_Sun, rotation, origin, 1f, spriteEffects, 0f);
                        spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(0, 1), sourceRect, _global.Palette_Sun, rotation, origin, 1f, spriteEffects, 0f);
                    }
                }

                if (drawSilhouette)
                {
                    var silhouette = _spriteManager.PlayerMasterSpriteSheetSilhouette;
                    spriteBatch.DrawSnapped(silhouette ?? sheet, drawPos, sourceRect, _global.Palette_Sun, rotation, origin, 1f, spriteEffects, 0f);
                }
                else
                {
                    spriteBatch.DrawSnapped(sheet, drawPos, sourceRect, color, rotation, origin, 1f, spriteEffects, 0f);
                }
            }
        }
    }
}