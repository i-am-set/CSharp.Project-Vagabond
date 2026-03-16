using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Animations;
using ProjectVagabond.Battle;
using ProjectVagabond.Deliveries;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class ArenaScene : GameScene
    {
        private enum ArenaState
        {
            Countdown,
            Fighting,
            MatchOver
        }

        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly GameState _gameState;
        private readonly InputManager _inputManager;
        private readonly SceneManager _sceneManager;
        private readonly TransitionManager _transitionManager;
        private readonly Texture2D _pixel;
        private readonly Core _core;
        private readonly ParticleSystemManager _particleSystemManager;
        private readonly TextureFactory _textureFactory;
        private readonly CursorManager _cursorManager;

        private readonly List<ArenaWizard> _wizards = new List<ArenaWizard>();
        private List<ArenaWizard> _wizardsByHudOrder = new List<ArenaWizard>();
        private readonly List<ActiveAttack> _activeAttacks = new List<ActiveAttack>();
        private readonly List<ArenaWizard> _queryResults = new List<ArenaWizard>();
        private readonly Random _random = new Random();

        private readonly TicketManager _ticketManager = new TicketManager();
        private readonly WizardRenderer _wizardRenderer = new WizardRenderer();

        private ArenaState _arenaState;
        private float _phaseTimer = 0f;
        private int _lastCountdownSecond = 0;

        private PlinkAnimator _plinkBetCountdown;
        private PlinkAnimator _plinkFight;

        private MouseState _lastMouseState;

        private const float ARENA_RADIUS = 85f;
        private int _arenaEdges = 8;
        private Vector2 _arenaCenter;
        private Texture2D _arenaTexture;
        private Texture2D _arenaOutlineTexture;

        private float _matchOverTimer = 0f;
        private string _matchResultText = "";
        private Color _matchResultColor = Color.White;

        private float _hudBaseX;
        private float _hudNameX;
        private float _hudMultCenterX;
        private ArenaWizard _hoveredHudWizard;

        private bool _playerTicketPrinted = false;

        private const int MULT_DEFAULT_MIN_STEP = 9; // Index 9 is Palette_Rust

        private readonly Dictionary<ArenaWizard, int> _probSteps = new Dictionary<ArenaWizard, int>();
        private readonly Dictionary<ArenaWizard, PlinkAnimator> _probPlinks = new Dictionary<ArenaWizard, PlinkAnimator>();
        private readonly Dictionary<ArenaWizard, float> _winProbabilities = new Dictionary<ArenaWizard, float>();

        private BattleContext _battleContext;

        public IReadOnlyList<ArenaWizard> Wizards => _wizards;
        public IReadOnlyList<ActiveAttack> ActiveAttacks => _activeAttacks;

        public bool IsOvertime { get; private set; }
        private float _matchTimer;
        private int _lastMatchSecond;
        private PlinkAnimator _plinkTimerText;
        private PlinkAnimator _plinkSuddenDeath;
        private float _suddenDeathTimer;

        public ArenaScene()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _pixel = ServiceLocator.Get<Texture2D>();
            _core = ServiceLocator.Get<Core>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _textureFactory = ServiceLocator.Get<TextureFactory>();
            _cursorManager = ServiceLocator.Get<CursorManager>();
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
                if (w.Data.Combat.State != WizardState.Dead && CollisionMath.RectangleIntersectsCircle(w.Controller.GetHitbox(_spriteManager), center, radius))
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
                if (w.Data.Combat.State != WizardState.Dead && CollisionMath.AABBIntersectsOBB(w.Controller.GetHitbox(_spriteManager), origin, direction, width, length))
                {
                    _queryResults.Add(w);
                }
            }
            return _queryResults;
        }

        public void DebugPrintTicket()
        {
            _ticketManager.DebugPrintTicket();
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
            _wizardsByHudOrder.Clear();
            _activeAttacks.Clear();
            _ticketManager.Clear();
            _probSteps.Clear();
            _probPlinks.Clear();
            _winProbabilities.Clear();

            _battleContext = new BattleContext
            {
                Arena = this,
                Global = _global,
                SpriteManager = _spriteManager,
                Core = _core,
                ParticleSystemManager = _particleSystemManager,
                TextureFactory = _textureFactory,
                Pixel = _pixel
            };

            _arenaState = ArenaState.Countdown;
            _phaseTimer = 5.0f;
            _lastCountdownSecond = 5;
            _playerTicketPrinted = false;

            IsOvertime = false;
            _matchTimer = 60f;
            _lastMatchSecond = 60;
            _plinkTimerText = new PlinkAnimator();
            _plinkSuddenDeath = new PlinkAnimator();
            _suddenDeathTimer = 0f;

            _plinkBetCountdown = new PlinkAnimator();
            _plinkFight = new PlinkAnimator();

            _plinkBetCountdown.Start(0f, 0.3f);

            _matchOverTimer = 0f;
            _matchResultText = "";

            _arenaCenter = new Vector2(Global.VIRTUAL_WIDTH - 4 - ARENA_RADIUS, Global.VIRTUAL_HEIGHT / 2f);
            _arenaEdges = Math.Max(3, _arenaEdges);

            _arenaTexture?.Dispose();
            _arenaOutlineTexture?.Dispose();

            _arenaOutlineTexture = _textureFactory.CreatePolygonTexture((int)ARENA_RADIUS + 2, _arenaEdges);
            _arenaTexture = _textureFactory.CreatePolygonTexture((int)ARENA_RADIUS, _arenaEdges);

            var selectedIds = _gameState.SelectedRoster;
            int count = selectedIds.Count;

            for (int i = 0; i < count; i++)
            {
                string id = selectedIds[i];
                if (!GameDataCache.WizardCats.TryGetValue(id, out var data)) continue;
                if (!_gameState.RunWizards.TryGetValue(id, out var rolledStats)) continue;

                float angle = (i / (float)count) * MathHelper.TwoPi;
                float spawnRadius = GetMaxRadiusAtAngle(angle, 16f);
                Vector2 spawnPos = _arenaCenter + new Vector2(MathF.Cos(angle) * spawnRadius, MathF.Sin(angle) * spawnRadius);

                var wizard = new ArenaWizard();
                bool isPlayer = (id == _gameState.PlayerControlledId);
                wizard.Controller.Initialize(data, rolledStats, spawnPos, isPlayer);
                _wizards.Add(wizard);
            }

            int totalRating = _wizards.Sum(w => w.Data.Stats.Rating);
            foreach (var w in _wizards)
            {
                if (totalRating > 0 && w.Data.Stats.Rating > 0)
                {
                    _winProbabilities[w] = (float)w.Data.Stats.Rating / totalRating;
                }
                else
                {
                    _winProbabilities[w] = 0f;
                }

                _probSteps[w] = GetProbabilityStep(_winProbabilities[w]);
                _probPlinks[w] = new PlinkAnimator { MaxScale = 1.5f, PlinkTriggerThreshold = 0f };
            }

            CalculateHUDLayout();

            _wizardsByHudOrder = _wizards.OrderBy(w => w.Data.UI.HudNamePos.Y).ToList();
        }

        private void CalculateHUDLayout()
        {
            var defaultFont = _core.DefaultFont;
            var secondaryFont = _core.SecondaryFont;

            int totalWizards = _wizards.Count;
            if (totalWizards == 0) return;

            int spacingY = 20;
            int itemHeight = (int)secondaryFont.LineHeight + 7;

            int totalBlockHeight = (totalWizards - 1) * spacingY + itemHeight;
            int startY = (Global.VIRTUAL_HEIGHT - totalBlockHeight) / 2;

            float uiAreaWidth = Global.VIRTUAL_WIDTH - 4 - ARENA_RADIUS * 2;

            float maxNameWidth = 0f;
            foreach (var w in _wizards)
            {
                w.Data.UI.HudNameSize = secondaryFont.MeasureString(w.Data.Stats.Name.ToUpper());
                if (w.Data.UI.HudNameSize.X > maxNameWidth) maxNameWidth = w.Data.UI.HudNameSize.X;
            }

            float maxProbWidth = defaultFont.MeasureString("100%").Width;

            float probCenterX = maxProbWidth / 2f;
            float nameX = maxProbWidth + 8f;
            float spellCenterX = nameX + maxNameWidth + 8f + 4.5f;

            float totalWidth = spellCenterX + 4.5f;

            float originalBaseX = MathF.Round((uiAreaWidth - totalWidth) / 2f);
            if (originalBaseX < 4) originalBaseX = 4;

            // Apply requested leftward shifts
            _hudBaseX = originalBaseX + probCenterX - 16f;
            _hudNameX = originalBaseX + nameX - 24f;
            _hudMultCenterX = originalBaseX + spellCenterX - 24f;

            for (int i = 0; i < totalWizards; i++)
            {
                var w = _wizards[i];
                w.Data.UI.HudIsLeft = true;

                float currentY = startY + i * spacingY;

                w.Data.UI.HudNamePos = new Vector2(_hudNameX, currentY);
                w.Data.UI.HudHeartStartPos = new Vector2(_hudNameX, currentY + w.Data.UI.HudNameSize.Y + 2);
            }
        }

        public override void Exit()
        {
            base.Exit();
            _arenaTexture?.Dispose();
            _arenaOutlineTexture?.Dispose();
            PoolManager.ClearAll();
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

            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);
            float dt = (float)effectiveGameTime.ElapsedGameTime.TotalSeconds;

            _plinkBetCountdown?.Update(effectiveGameTime, _arenaCenter);
            _plinkFight?.Update(effectiveGameTime, _arenaCenter);
            _plinkTimerText?.Update(effectiveGameTime, new Vector2(_arenaCenter.X, 12));
            _plinkSuddenDeath?.Update(effectiveGameTime, new Vector2(_arenaCenter.X, Global.VIRTUAL_HEIGHT / 2f - 20));

            foreach (var w in _wizards)
            {
                if (_probPlinks.TryGetValue(w, out var plink) && plink.IsActive)
                {
                    plink.Update(effectiveGameTime, Vector2.Zero);
                }
            }

            var mouseState = _inputManager.GetEffectiveMouseState();
            Vector2 virtualMousePos = Core.TransformMouse(mouseState.Position);
            bool isClicking = mouseState.LeftButton == ButtonState.Pressed;
            bool justClicked = isClicking && _lastMouseState.LeftButton == ButtonState.Released;

            int aliveCount = _wizards.Count(w => w.Data.Combat.State != WizardState.Dead);

            _hoveredHudWizard = null;
            bool canHover = _arenaState == ArenaState.Countdown || (_arenaState == ArenaState.Fighting && aliveCount > 1);

            if (canHover)
            {
                var defaultFont = _core.DefaultFont;
                float maxProbWidth = defaultFont.MeasureString("100%").Width;

                for (int i = 0; i < _wizardsByHudOrder.Count; i++)
                {
                    var w = _wizardsByHudOrder[i];
                    if (w.Data.Combat.State == WizardState.Dead) continue;

                    Rectangle hudRect = new Rectangle((int)(_hudBaseX - maxProbWidth / 2f - 2), (int)w.Data.UI.HudNamePos.Y - 2, (int)(_hudMultCenterX - (_hudBaseX - maxProbWidth / 2f) + 16), 20);

                    if (hudRect.Contains(virtualMousePos))
                    {
                        _hoveredHudWizard = w;
                    }
                }
            }

            _ticketManager.Update(dt, virtualMousePos, justClicked, isClicking, _inputManager);

            if (_ticketManager.IsHoveringTicket || _ticketManager.IsDraggingTicket)
            {
                _cursorManager.SetState(_ticketManager.IsDraggingTicket ? CursorState.Dragging : CursorState.HoverDraggable);
            }

            if (_arenaState == ArenaState.Countdown)
            {
                _phaseTimer -= dt;
                int currentSecond = (int)Math.Ceiling(_phaseTimer);
                if (currentSecond != _lastCountdownSecond && currentSecond > 0)
                {
                    _lastCountdownSecond = currentSecond;
                    _plinkBetCountdown.Start(0f, 0.2f);
                }

                if (_phaseTimer <= 0)
                {
                    _arenaState = ArenaState.Fighting;
                    _phaseTimer = 0f;
                    _plinkFight.Start(0f, 0.3f);
                }
            }

            if (_arenaState == ArenaState.Fighting || _arenaState == ArenaState.MatchOver)
            {
                _phaseTimer += dt;

                if (_arenaState == ArenaState.Fighting)
                {
                    if (_phaseTimer >= 1.0f && !IsOvertime)
                    {
                        _matchTimer -= dt;
                        int currentSec = (int)Math.Ceiling(_matchTimer);
                        if (currentSec != _lastMatchSecond && currentSec >= 0)
                        {
                            _lastMatchSecond = currentSec;
                            if (currentSec == 30 || currentSec <= 10)
                            {
                                _plinkTimerText.Start(0f, 0.3f);
                            }
                        }

                        if (_matchTimer <= 0)
                        {
                            IsOvertime = true;
                            _suddenDeathTimer = 1.0f;
                            _plinkSuddenDeath.Start(0f, 0.3f);
                            _plinkTimerText.Start(0f, 0.3f);

                            foreach (var w in _wizards)
                            {
                                if (w.Data.Combat.State != WizardState.Dead)
                                {
                                    w.Data.Stats.CurrentHP = 1;
                                    if (w.Data.Combat.QueuedMove != null && w.Data.Combat.QueuedMove.Effects.Any(e => e is HealEffect))
                                    {
                                        w.Data.Combat.State = WizardState.Recovering;
                                        w.Data.Combat.StateTimer = 0.25f;
                                        w.Data.Combat.QueuedMove = null;
                                    }
                                }
                            }

                            foreach (var attack in _activeAttacks)
                            {
                                if (attack.Move.Effects.Any(e => e is HealEffect))
                                {
                                    attack.IsCanceled = true;
                                }
                            }
                        }
                    }

                    if (_suddenDeathTimer > 0)
                    {
                        _suddenDeathTimer -= dt;
                    }
                }

                if (_inputManager.ActiveSpellTriggered && _arenaState == ArenaState.Fighting)
                {
                    var player = _wizards.FirstOrDefault(w => w.Data.Stats.IsPlayer);
                    if (player != null) player.Controller.TriggerActiveSpell(_battleContext);
                }

                foreach (var wizard in _wizards)
                {
                    wizard.Data.UI.IsHovered = wizard.Data.Combat.State != WizardState.Dead && wizard.Controller.GetHitbox(_spriteManager).Contains(virtualMousePos);
                    wizard.Controller.Update(dt, _battleContext);
                }

                for (int i = 0; i < _wizards.Count; i++)
                {
                    var w1 = _wizards[i];
                    if (w1.Data.Combat.State == WizardState.Dead) continue;

                    for (int j = i + 1; j < _wizards.Count; j++)
                    {
                        var w2 = _wizards[j];
                        if (w2.Data.Combat.State == WizardState.Dead) continue;

                        var box1 = w1.Controller.GetHitbox(_spriteManager);
                        var box2 = w2.Controller.GetHitbox(_spriteManager);

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

                            if (w1.Data.Combat.State == WizardState.Moving)
                            {
                                w1.Data.Combat.TargetPosition = ClampToArena(w1.Data.Combat.Position + pushDir * 50f, 12f);
                            }
                            if (w2.Data.Combat.State == WizardState.Moving)
                            {
                                w2.Data.Combat.TargetPosition = ClampToArena(w2.Data.Combat.Position - pushDir * 50f, 12f);
                            }
                        }
                    }
                }

                for (int i = _activeAttacks.Count - 1; i >= 0; i--)
                {
                    var attack = _activeAttacks[i];

                    if (attack.TargetWizard != null && attack.TargetWizard.Data.Combat.IsSuspended)
                    {
                        attack.TargetWizard = null;
                    }

                    attack.Update(dt, _battleContext);

                    if (attack.IsFinished)
                    {
                        _activeAttacks.RemoveAt(i);
                        attack.ReturnToPool();
                    }
                }

                if (_arenaState == ArenaState.Fighting)
                {
                    UpdateDynamicOdds();

                    int currentAliveCount = _wizards.Count(w => w.Data.Combat.State != WizardState.Dead);

                    foreach (var w in _wizards)
                    {
                        if (w.Data.Combat.State == WizardState.Dead && w.Data.Metrics.Placement == 0)
                        {
                            w.Data.Metrics.Placement = currentAliveCount + 1;
                            w.Data.Metrics.TimeSurvived = _phaseTimer;
                        }
                    }

                    if (currentAliveCount <= 1)
                    {
                        _arenaState = ArenaState.MatchOver;
                        _matchOverTimer = 4.0f;

                        var winner = _wizards.FirstOrDefault(w => w.Data.Combat.State != WizardState.Dead);
                        if (winner != null)
                        {
                            winner.Data.Metrics.Placement = 1;
                            winner.Data.Metrics.TimeSurvived = _phaseTimer;

                            _matchResultText = $"{winner.Data.Stats.Name.ToUpper()} WINS!";
                            if (!_playerTicketPrinted)
                            {
                                _playerTicketPrinted = true;
                                _ticketManager.PrintTicket(_wizards.IndexOf(winner) + 1, 1);
                            }
                        }
                        else
                        {
                            _matchResultText = "DRAW";
                            _matchResultColor = _global.Palette_Gray;
                        }

                        _gameState.LastMatchWizards = _wizards.ToList();
                    }
                }
                else if (_arenaState == ArenaState.MatchOver)
                {
                    _matchOverTimer -= dt;
                }
            }

            if (_inputManager.Back)
            {
                _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff);
            }

            _lastMouseState = mouseState;
        }

        private int GetProbabilityStep(float prob)
        {
            return Math.Clamp((int)Math.Round(prob * 12.0f), 0, 12);
        }

        private void UpdateDynamicOdds()
        {
            float totalDynamicRating = 0f;

            foreach (var w in _wizards)
            {
                if (w.Data.Combat.State != WizardState.Dead && w.Data.Stats.CurrentHP > 0)
                {
                    totalDynamicRating += w.Data.Stats.Rating * ((float)w.Data.Stats.CurrentHP / w.Data.Stats.MaxHP);
                }
            }

            foreach (var w in _wizards)
            {
                if (w.Data.Combat.State == WizardState.Dead || w.Data.Stats.CurrentHP <= 0)
                {
                    _winProbabilities[w] = 0f;
                }
                else if (totalDynamicRating > 0)
                {
                    float dynamicRating = w.Data.Stats.Rating * ((float)w.Data.Stats.CurrentHP / w.Data.Stats.MaxHP);
                    _winProbabilities[w] = dynamicRating / totalDynamicRating;
                }

                int step = GetProbabilityStep(_winProbabilities[w]);

                if (_probSteps.TryGetValue(w, out int currentStep) && currentStep != step)
                {
                    if (currentStep != -1)
                    {
                        _probPlinks[w].Start(0f, 0.3f);
                    }
                    _probSteps[w] = step;
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
            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);

            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.GameBg);

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

            _wizards.Sort((a, b) => a.Data.Combat.Position.Y.CompareTo(b.Data.Combat.Position.Y));

            foreach (var wizard in _wizards)
            {
                if (wizard.Data.Combat.State == WizardState.Dead) _wizardRenderer.DrawWizard(wizard, spriteBatch, _spriteManager, _global);
            }

            foreach (var wizard in _wizards)
            {
                if (wizard.Data.Combat.State != WizardState.Dead) _wizardRenderer.DrawWizard(wizard, spriteBatch, _spriteManager, _global);
            }

            foreach (var attack in _activeAttacks)
            {
                attack.Animation?.Draw(spriteBatch, attack);
            }

            spriteBatch.End();
            _particleSystemManager.Draw(spriteBatch, transform);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            if (_hoveredHudWizard != null && _hoveredHudWizard.Data.Combat.State != WizardState.Dead)
            {
                Vector2 mousePos = Core.TransformMouse(_inputManager.GetEffectiveMouseState().Position);
                var points = SpriteBatchExtensions.GetBresenhamLinePoints(mousePos, _hoveredHudWizard.Data.Combat.Position);
                int offset = (int)(effectiveGameTime.TotalGameTime.TotalSeconds * 30) % 6;
                for (int i = offset; i < points.Count; i += 6)
                {
                    spriteBatch.Draw(_pixel, new Vector2(points[i].X, points[i].Y), _global.Palette_Fruit);
                }
            }

            foreach (var wizard in _wizards)
            {
                _wizardRenderer.DrawUI(wizard, spriteBatch, _spriteManager, effectiveGameTime);
                _wizardRenderer.DrawDebug(wizard, spriteBatch, _battleContext);
            }

            DrawSideHUD(spriteBatch);

            var mainFont = _core.DefaultFont;
            var tertFont = _core.TertiaryFont;

            _ticketManager.Draw(spriteBatch, _spriteManager, _global, mainFont, tertFont);

            if ((_arenaState == ArenaState.Fighting && _phaseTimer >= 1.0f) || _arenaState == ArenaState.MatchOver)
            {
                string timerText = IsOvertime ? "OVERTIME" : _lastMatchSecond.ToString();
                Color timerColor = IsOvertime ? _global.Palette_Rust : (_lastMatchSecond <= 3 ? _global.Palette_Rust : _global.Palette_DarkPale);

                Vector2 tSize = mainFont.MeasureString(timerText);
                Vector2 tPos = new Vector2(MathF.Round(_arenaCenter.X), 12 + MathF.Round(tSize.Y / 2f));
                Vector2 tOrigin = new Vector2(MathF.Round(tSize.X / 2f), MathF.Round(tSize.Y / 2f));

                float tScale = _plinkTimerText.IsActive ? _plinkTimerText.Scale : 1f;
                float tRot = _plinkTimerText.IsActive ? _plinkTimerText.Rotation : 0f;

                spriteBatch.DrawStringOutlinedSnapped(mainFont, timerText, tPos, timerColor, _global.Palette_Off, tRot, tOrigin, tScale, SpriteEffects.None, 0f);
            }

            if (_suddenDeathTimer > 0)
            {
                string sdText = "SUDDEN DEATH";
                Vector2 sdSize = mainFont.MeasureString(sdText);
                Vector2 sdPos = new Vector2(MathF.Round(_arenaCenter.X), MathF.Round(Global.VIRTUAL_HEIGHT / 2f - 20));
                Vector2 sdOrigin = new Vector2(MathF.Round(sdSize.X / 2f), MathF.Round(sdSize.Y / 2f));

                float sdScale = _plinkSuddenDeath.IsActive ? _plinkSuddenDeath.Scale : 1f;
                float sdRot = _plinkSuddenDeath.IsActive ? _plinkSuddenDeath.Rotation : 0f;
                float sdAlpha = Math.Clamp(_suddenDeathTimer, 0f, 1f);

                spriteBatch.DrawStringOutlinedSnapped(mainFont, sdText, sdPos, _global.Palette_Rust * sdAlpha, _global.Palette_Off * sdAlpha, sdRot, sdOrigin, sdScale, SpriteEffects.None, 0f);
            }

            if (_arenaState == ArenaState.Countdown)
            {
                int currentSecond = (int)Math.Ceiling(_phaseTimer);
                if (currentSecond > 0)
                {
                    string countText = currentSecond.ToString();
                    Vector2 countSize = mainFont.MeasureString(countText);
                    Vector2 countPos = new Vector2(MathF.Round(_arenaCenter.X - countSize.X / 2f), MathF.Round(_arenaCenter.Y - countSize.Y / 2f));
                    Vector2 countOrigin = new Vector2(MathF.Round(countSize.X / 2f), MathF.Round(countSize.Y / 2f));
                    float countScale = _plinkBetCountdown.IsActive ? _plinkBetCountdown.Scale : 1f;
                    float countRot = _plinkBetCountdown.IsActive ? _plinkBetCountdown.Rotation : 0f;
                    Color countColor = currentSecond <= 3 ? _global.Palette_Rust : _global.Palette_Sun;

                    spriteBatch.DrawStringOutlinedSnapped(mainFont, countText, countPos + countOrigin, countColor, _global.Palette_Off, countRot, countOrigin, countScale, SpriteEffects.None, 0f);
                }
            }
            else if (_arenaState == ArenaState.Fighting && _phaseTimer < 1.0f)
            {
                string text = "FIGHT!";
                Vector2 size = mainFont.MeasureString(text);
                Vector2 pos = new Vector2(MathF.Round(_arenaCenter.X - size.X / 2f), MathF.Round(_arenaCenter.Y - size.Y / 2f));
                Vector2 origin = new Vector2(MathF.Round(size.X / 2f), MathF.Round(size.Y / 2f));
                float scale = _plinkFight.IsActive ? _plinkFight.Scale : 1f;
                float rot = _plinkFight.IsActive ? _plinkFight.Rotation : 0f;
                float alpha = 1.0f - _phaseTimer;

                spriteBatch.DrawStringOutlinedSnapped(mainFont, text, pos + origin, _global.Palette_Sun * alpha, _global.Palette_Off * alpha, rot, origin, scale, SpriteEffects.None, 0f);
            }
            else if (_arenaState == ArenaState.MatchOver)
            {
                string text = _matchResultText;
                Color textColor = _matchResultColor;

                if (text.EndsWith("WINS!"))
                {
                    float flash = (float)(Math.Sin(effectiveGameTime.TotalGameTime.TotalSeconds * 15f) + 1f) / 2f;
                    textColor = Color.Lerp(_global.Palette_Sky, _global.Palette_Sun, flash);
                }

                Vector2 size = mainFont.MeasureString(text);
                Vector2 pos = new Vector2(MathF.Round(_arenaCenter.X - size.X / 2f), MathF.Round(_arenaCenter.Y - size.Y / 2f - 10));

                spriteBatch.DrawStringOutlinedSnapped(mainFont, text, pos, textColor, _global.Palette_Off);
            }
        }

        private string GetOrdinalSuffix(int number)
        {
            int mod100 = number % 100;
            if (mod100 >= 11 && mod100 <= 13) return "TH";
            switch (number % 10)
            {
                case 1: return "ST";
                case 2: return "ND";
                case 3: return "RD";
                default: return "TH";
            }
        }

        private void DrawSideHUD(SpriteBatch spriteBatch)
        {
            var defaultFont = _core.DefaultFont;
            var secondaryFont = _core.SecondaryFont;
            var tertiaryFont = _core.TertiaryFont;
            var sheet = _spriteManager.HealthHeartsSpriteSheet;
            if (sheet == null || _wizards.Count == 0) return;

            int heartWidth = 5;
            int heartSpacing = 1;

            int aliveCount = _wizards.Count(wiz => wiz.Data.Combat.State != WizardState.Dead);
            bool showProbabilities = _arenaState == ArenaState.Countdown || (_arenaState == ArenaState.Fighting && aliveCount > 1);

            float maxProbWidth = defaultFont.MeasureString("100%").Width;

            foreach (var w in _wizards)
            {
                float shakeX = 0f;
                float shakeY = 0f;
                if (w.Data.UI.HudShakeTimer > 0)
                {
                    float shakeMag = (w.Data.UI.HudShakeTimer / 0.4f) * 3f;
                    shakeX = (float)(_random.NextDouble() * 2 - 1) * shakeMag;
                    shakeY = (float)(_random.NextDouble() * 2 - 1) * shakeMag;
                }

                Rectangle hudRect = new Rectangle((int)(_hudBaseX - maxProbWidth / 2f - 2), (int)w.Data.UI.HudNamePos.Y - 2, (int)(_hudMultCenterX - (_hudBaseX - maxProbWidth / 2f) + 16), 20);
                if (_hoveredHudWizard == w)
                {
                    spriteBatch.Draw(_pixel, hudRect, _global.Palette_DarkShadow * 0.5f);
                }

                Color baseNameColor = w.Data.Stats.IsPlayer ? _global.Palette_DarkPale : _global.Palette_DarkestPale;
                Color nameColor = baseNameColor;

                if (w.Data.Combat.State == WizardState.Dead)
                {
                    float fadeProgress = Math.Clamp(w.Data.Combat.TimeSinceDeath / 0.5f, 0f, 1f);
                    nameColor = Color.Lerp(baseNameColor, _global.Palette_Black, fadeProgress);
                }

                Vector2 finalNamePos = new Vector2(MathF.Round(_hudNameX + shakeX), MathF.Round(w.Data.UI.HudNamePos.Y + shakeY));
                spriteBatch.DrawStringSnapped(secondaryFont, w.Data.Stats.Name.ToUpper(), finalNamePos, nameColor);

                if (w.Data.Combat.State != WizardState.Dead && showProbabilities)
                {
                    int step = _probSteps.TryGetValue(w, out var s) ? s : 0;
                    Color probColor = GetMultiplierColor(step);
                    BitmapFont probFont = secondaryFont;

                    if (step < 4) probFont = tertiaryFont;
                    else if (step >= MULT_DEFAULT_MIN_STEP) probFont = defaultFont;

                    float prob = _winProbabilities.TryGetValue(w, out var p) ? p : 0f;
                    string probText = $"{(prob * 100f):F0}%";

                    var plink = _probPlinks[w];
                    float pScale = plink.IsActive ? plink.Scale : 1f;
                    float pRot = plink.IsActive ? plink.Rotation : 0f;

                    if (plink.IsActive && plink.FlashTint.HasValue)
                    {
                        float flashAmount = plink.FlashTint.Value.A / 255f;
                        probColor = Color.Lerp(probColor, Color.White, flashAmount);
                    }

                    Vector2 probSize = probFont.MeasureString(probText);
                    Vector2 pivot = new Vector2(MathF.Round(probSize.X / 2f), MathF.Round(probSize.Y / 2f));
                    Vector2 probOrigin = new Vector2(MathF.Round(pivot.X), MathF.Round(pivot.Y));

                    float probYOffset = MathF.Round((secondaryFont.LineHeight - probSize.Y) / 2f);
                    Vector2 drawPos = new Vector2(MathF.Round(_hudBaseX + shakeX), MathF.Round(finalNamePos.Y + probYOffset + pivot.Y));

                    spriteBatch.DrawStringSnapped(probFont, probText, drawPos, probColor, pRot, probOrigin, pScale, SpriteEffects.None, 0f);
                }

                float spellYOffset = MathF.Round((secondaryFont.LineHeight - 9f) / 2f) + 4f;
                Vector2 spellPos = new Vector2(MathF.Round(_hudMultCenterX - 4.5f + shakeX), MathF.Round(finalNamePos.Y + spellYOffset + shakeY));

                float showSpellAlpha = 1f;
                float showPlacementAlpha = 0f;

                if (w.Data.Combat.State == WizardState.Dead)
                {
                    float fadeProgress = Math.Clamp(w.Data.Combat.TimeSinceDeath / 0.5f, 0f, 1f);
                    showSpellAlpha = 1f - fadeProgress;
                    showPlacementAlpha = fadeProgress;
                }
                else if (w.Data.Metrics.Placement == 1 && _arenaState == ArenaState.MatchOver)
                {
                    float fadeProgress = Math.Clamp((4.0f - _matchOverTimer) / 0.5f, 0f, 1f);
                    showSpellAlpha = 1f - fadeProgress;
                    showPlacementAlpha = fadeProgress;
                }

                if (w.Data.Combat.EquippedActiveSpell != null && showSpellAlpha > 0f)
                {
                    var spellSheet = _spriteManager.ActiveSpellsSpriteSheet;
                    if (spellSheet != null)
                    {
                        int frame = w.Data.Combat.EquippedActiveSpell.SpriteFrame;
                        Rectangle sourceRect = new Rectangle(frame * 9, 0, 9, 9);

                        float cd = w.Data.Combat.ActiveSpellCooldownTimer;
                        float opacity = (cd > 0 ? 0.15f : 1.0f) * showSpellAlpha;

                        spriteBatch.DrawSnapped(spellSheet, spellPos, sourceRect, Color.White * opacity);

                        if (cd > 0)
                        {
                            string cdText = MathF.Ceiling(cd).ToString();
                            Vector2 cdSize = secondaryFont.MeasureString(cdText);
                            Vector2 cdPos = spellPos + new Vector2(4f, 4f) - new Vector2(MathF.Round(cdSize.X / 2f), MathF.Round(cdSize.Y / 2f));

                            spriteBatch.DrawStringSquareOutlinedSnapped(secondaryFont, cdText, cdPos, _global.Palette_Sun * showSpellAlpha, _global.Palette_Black * showSpellAlpha);
                        }
                    }
                }

                if (showPlacementAlpha > 0f && w.Data.Metrics.Placement > 0)
                {
                    string numText = w.Data.Metrics.Placement.ToString();
                    string sufText = GetOrdinalSuffix(w.Data.Metrics.Placement);

                    Vector2 numSize = defaultFont.MeasureString(numText);
                    Vector2 sufSize = secondaryFont.MeasureString(sufText);

                    float totalWidth = numSize.X + sufSize.X;
                    float startX = spellPos.X + 4.5f - totalWidth / 2f;

                    float numY = spellPos.Y + 4.5f - numSize.Y / 2f;
                    float sufY = spellPos.Y + 4.5f - sufSize.Y / 2f;

                    Color placementColor = w.Data.Metrics.Placement == 1 ? _global.Palette_Sun : _global.Palette_Black;
                    placementColor *= showPlacementAlpha;

                    spriteBatch.DrawStringSnapped(defaultFont, numText, new Vector2(MathF.Round(startX), MathF.Round(numY)), placementColor);
                    spriteBatch.DrawStringSnapped(tertiaryFont, sufText, new Vector2(MathF.Round(startX + numSize.X) + 1, MathF.Round(sufY)), placementColor);
                }

                if (w.Data.Combat.State == WizardState.Dead)
                {
                    float fadeProgress = Math.Clamp(w.Data.Combat.TimeSinceDeath / 0.5f, 0f, 1f);
                    int currentLineWidth = (int)(w.Data.UI.HudNameSize.X * fadeProgress);
                    if (currentLineWidth > 0)
                    {
                        int lineY = (int)MathF.Round(finalNamePos.Y + w.Data.UI.HudNameSize.Y / 2f);
                        spriteBatch.Draw(_pixel, new Rectangle((int)finalNamePos.X, lineY, currentLineWidth, 1), _global.Palette_Black);
                    }
                }
                else
                {
                    int maxHearts = (w.Data.Stats.MaxHP + 2) / 3;
                    for (int h = 0; h < maxHearts; h++)
                    {
                        int heartVal = Math.Clamp(w.Data.Stats.CurrentHP - h * 3, 0, 3);
                        int frameIndex = 3; // 0/3
                        if (heartVal == 3) frameIndex = 0; // 3/3
                        else if (heartVal == 2) frameIndex = 1; // 2/3
                        else if (heartVal == 1) frameIndex = 2; // 1/3

                        int flashFrame = w.Controller.GetHeartFlashFrame(h);
                        if (flashFrame != -1) frameIndex = flashFrame;

                        var sourceRect = new Rectangle(frameIndex * heartWidth, 0, heartWidth, 5);

                        int yOffset = 0;
                        if (w.Data.Stats.CurrentHP > 0)
                        {
                            float localWaveTime = w.Data.UI.HudHeartWaveTimer - w.Data.UI.HudHeartWaveInterval - (h * 0.08f);
                            if (localWaveTime > 0 && localWaveTime < 0.15f)
                            {
                                yOffset = -1;
                            }
                        }

                        Vector2 finalHeartPos = new Vector2(MathF.Round(_hudNameX + h * (heartWidth + heartSpacing) + shakeX), MathF.Round(w.Data.UI.HudHeartStartPos.Y + shakeY) + yOffset);
                        spriteBatch.DrawSnapped(sheet, finalHeartPos, sourceRect, Color.White);
                    }
                }
            }
        }
    }
}