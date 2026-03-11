using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
        private enum ArenaState
        {
            Countdown,
            Fighting,
            MatchOver
        }

        private class MatchTicket
        {
            public int Placement;
            public int WizardNumber;
            public float AnimTimer;
            public float TargetX;

            public Vector2 Position;
            public Vector2 Velocity;
            public bool IsDragging;
            public Vector2 DragOffset;
            public bool IsDispensed;
            public bool IsHanging;
            public float Scale = 1.0f;

            // 3D Rotation properties
            public float RotX;
            public float RotY;
            public float RotZ;
            public float VelRotX;
            public float VelRotY;
            public float VelRotZ;

            // Flutter properties
            public float FlutterPhase;
            public float FlutterSpeed;
        }

        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly GameState _gameState;
        private readonly InputManager _inputManager;
        private readonly SceneManager _sceneManager;
        private readonly TransitionManager _transitionManager;

        private readonly List<ArenaWizard> _wizards = new List<ArenaWizard>();
        private List<ArenaWizard> _wizardsByHudOrder = new List<ArenaWizard>();
        private readonly List<ActiveAttack> _activeAttacks = new List<ActiveAttack>();
        private readonly List<ArenaWizard> _queryResults = new List<ArenaWizard>();
        private readonly List<MatchTicket> _tickets = new List<MatchTicket>();
        private readonly Queue<MatchTicket> _pendingTickets = new Queue<MatchTicket>();
        private readonly Random _random = new Random();

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
            _wizardsByHudOrder.Clear();
            _activeAttacks.Clear();
            _tickets.Clear();
            _pendingTickets.Clear();
            _probSteps.Clear();
            _probPlinks.Clear();
            _winProbabilities.Clear();

            _arenaState = ArenaState.Countdown;
            _phaseTimer = 5.0f;
            _lastCountdownSecond = 5;
            _playerTicketPrinted = false;

            _plinkBetCountdown = new PlinkAnimator();
            _plinkFight = new PlinkAnimator();

            _plinkBetCountdown.Start(0f, 0.3f);

            _matchOverTimer = 0f;
            _matchResultText = "";

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
                    _winProbabilities[w] = (float)w.Rating / totalRating;
                }
                else
                {
                    _winProbabilities[w] = 0f;
                }

                _probSteps[w] = GetProbabilityStep(_winProbabilities[w]);
                _probPlinks[w] = new PlinkAnimator { MaxScale = 1.5f, PlinkTriggerThreshold = 0f };
            }

            CalculateHUDLayout();

            _wizardsByHudOrder = _wizards.OrderBy(w => w.HudNamePos.Y).ToList();
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

            float standardProbWidth = defaultFont.MeasureString("100%").Width;
            float totalWidth = multXOffset + standardProbWidth + 10f;

            _hudBaseX = MathF.Round((uiAreaWidth - totalWidth) / 2f);
            if (_hudBaseX < 4) _hudBaseX = 4;

            _hudBaseX -= 5f;
            _hudNameX = MathF.Round(_hudBaseX + nameXOffset);
            _hudMultCenterX = MathF.Round(_hudBaseX + multXOffset + 7f + (standardProbWidth / 2f));

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

        private void UpdateTicketDispense(MatchTicket ticket, float dt)
        {
            ticket.AnimTimer += dt;
            float startY = -16f;
            float dispenseY = 14.5f;

            float t1 = 0.25f; // Jerk 1 duration
            float t2 = t1 + 0.5f; // Wait 1
            float t3 = t2 + 0.25f; // Jerk 2 duration
            float t4 = t3 + 0.5f; // Wait 2
            float t5 = t4 + 1.0f; // Long Dispense duration

            float progress = 0f;

            if (ticket.AnimTimer < t1)
            {
                float p = ticket.AnimTimer / t1;
                progress = MathHelper.Lerp(0f, 0.2f, p);
            }
            else if (ticket.AnimTimer < t2)
            {
                progress = 0.2f;
            }
            else if (ticket.AnimTimer < t3)
            {
                float p = (ticket.AnimTimer - t2) / (t3 - t2);
                progress = MathHelper.Lerp(0.2f, 0.4f, p);
            }
            else if (ticket.AnimTimer < t4)
            {
                progress = 0.4f;
            }
            else if (ticket.AnimTimer < t5)
            {
                float p = (ticket.AnimTimer - t4) / (t5 - t4);
                progress = MathHelper.Lerp(0.4f, 1.0f, p);
            }
            else
            {
                progress = 1.0f;
                if (!ticket.IsDispensed)
                {
                    ticket.IsDispensed = true;
                    ticket.IsHanging = true;
                }
            }

            ticket.Position.Y = MathHelper.Lerp(startY, dispenseY, progress);
            ticket.Position.X = ticket.TargetX;
        }

        private float WrapAngle(float angle)
        {
            angle %= MathHelper.TwoPi;
            if (angle <= -MathHelper.Pi) angle += MathHelper.TwoPi;
            else if (angle > MathHelper.Pi) angle -= MathHelper.TwoPi;
            return angle;
        }

        private void PrintTicket(ArenaWizard wizard, int placement)
        {
            _pendingTickets.Enqueue(new MatchTicket
            {
                Placement = placement,
                WizardNumber = _wizards.IndexOf(wizard) + 1,
                AnimTimer = 0f,
                TargetX = 44,
                Position = new Vector2(44, -16f),
                Scale = 1.0f
            });
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _plinkBetCountdown?.Update(gameTime, _arenaCenter);
            _plinkFight?.Update(gameTime, _arenaCenter);

            foreach (var w in _wizards)
            {
                if (_probPlinks.TryGetValue(w, out var plink) && plink.IsActive)
                {
                    plink.Update(gameTime, Vector2.Zero);
                }
            }

            var mouseState = _inputManager.GetEffectiveMouseState();
            Vector2 virtualMousePos = Core.TransformMouse(mouseState.Position);
            bool isClicking = mouseState.LeftButton == ButtonState.Pressed;
            bool justClicked = isClicking && _lastMouseState.LeftButton == ButtonState.Released;

            int aliveCount = _wizards.Count(w => w.State != WizardState.Dead);

            _hoveredHudWizard = null;
            bool canHover = _arenaState == ArenaState.Countdown || (_arenaState == ArenaState.Fighting && aliveCount > 1);

            if (canHover)
            {
                for (int i = 0; i < _wizardsByHudOrder.Count; i++)
                {
                    var w = _wizardsByHudOrder[i];
                    if (w.State == WizardState.Dead) continue;

                    Rectangle hudRect = new Rectangle((int)_hudBaseX - 2, (int)w.HudNamePos.Y - 2, (int)(_hudMultCenterX - _hudBaseX + 30), 20);

                    if (hudRect.Contains(virtualMousePos))
                    {
                        _hoveredHudWizard = w;
                    }
                }
            }

            // Process Ticket Queue
            bool isPrinting = _tickets.Any(t => !t.IsDispensed);
            if (!isPrinting && _pendingTickets.Count > 0)
            {
                // Drop any currently hanging tickets to make room for the new print
                foreach (var t in _tickets)
                {
                    if (t.IsHanging)
                    {
                        t.IsHanging = false;
                        t.Velocity = new Vector2((float)(_random.NextDouble() * 60 - 30), 0f);
                        t.VelRotX = (float)(_random.NextDouble() * 4.0 - 2.0);
                        t.VelRotY = (float)(_random.NextDouble() * 4.0 - 2.0);
                        t.VelRotZ = (float)(_random.NextDouble() * 2.0 - 1.0);
                        t.FlutterPhase = (float)(_random.NextDouble() * MathHelper.TwoPi);
                        t.FlutterSpeed = (float)(_random.NextDouble() * 2.0 + 1.5);
                    }
                }

                _tickets.Add(_pendingTickets.Dequeue());
            }

            MatchTicket draggedTicket = _tickets.FirstOrDefault(t => t.IsDragging);

            if (justClicked && draggedTicket == null && _inputManager.IsMouseClickAvailable())
            {
                for (int i = _tickets.Count - 1; i >= 0; i--)
                {
                    var t = _tickets[i];
                    if (!t.IsDispensed) continue;

                    Matrix transform = Matrix.CreateTranslation(-t.Position.X, -t.Position.Y, 0) *
                                       Matrix.CreateRotationZ(-t.RotZ) *
                                       Matrix.CreateTranslation(t.Position.X, t.Position.Y, 0);
                    Vector2 localMouse = Vector2.Transform(virtualMousePos, transform);

                    float cosX = MathF.Cos(t.RotX);
                    float cosY = MathF.Cos(t.RotY);
                    int w = (int)(19 * t.Scale * Math.Abs(cosY));
                    int h = (int)(31 * t.Scale * Math.Abs(cosX));

                    w = Math.Max(w, 4);
                    h = Math.Max(h, 4);

                    Rectangle localBounds = new Rectangle((int)t.Position.X - w / 2, (int)t.Position.Y - h / 2, w, h);

                    if (localBounds.Contains(localMouse))
                    {
                        t.IsDragging = true;
                        t.IsHanging = false;
                        t.DragOffset = t.Position - virtualMousePos;
                        t.Velocity = Vector2.Zero;
                        t.VelRotX = 0f;
                        t.VelRotY = 0f;
                        t.VelRotZ = 0f;
                        draggedTicket = t;
                        _inputManager.ConsumeMouseClick();

                        _tickets.RemoveAt(i);
                        _tickets.Add(t);
                        break;
                    }
                }
            }

            if (draggedTicket != null)
            {
                if (isClicking)
                {
                    Vector2 prevPos = draggedTicket.Position;
                    draggedTicket.Position = virtualMousePos + draggedTicket.DragOffset;

                    if (dt > 0)
                    {
                        draggedTicket.Velocity = (draggedTicket.Position - prevPos) / dt;
                    }

                    draggedTicket.RotX = WrapAngle(draggedTicket.RotX);
                    draggedTicket.RotY = WrapAngle(draggedTicket.RotY);
                    draggedTicket.RotZ = WrapAngle(draggedTicket.RotZ);

                    draggedTicket.RotX = MathHelper.Lerp(draggedTicket.RotX, Math.Clamp(draggedTicket.Velocity.Y * 0.002f, -0.3f, 0.3f), 15f * dt);
                    draggedTicket.RotY = MathHelper.Lerp(draggedTicket.RotY, Math.Clamp(draggedTicket.Velocity.X * 0.002f, -0.3f, 0.3f), 15f * dt);
                    draggedTicket.RotZ = MathHelper.Lerp(draggedTicket.RotZ, Math.Clamp(draggedTicket.Velocity.X * 0.001f, -0.2f, 0.2f), 15f * dt);

                    draggedTicket.VelRotX = 0f;
                    draggedTicket.VelRotY = 0f;
                    draggedTicket.VelRotZ = 0f;
                }
                else
                {
                    draggedTicket.IsDragging = false;

                    // Cap release velocity to prevent physics explosions
                    draggedTicket.Velocity.X = Math.Clamp(draggedTicket.Velocity.X, -600f, 600f);
                    draggedTicket.Velocity.Y = Math.Clamp(draggedTicket.Velocity.Y, -600f, 600f);

                    // Give it a good spin when thrown
                    draggedTicket.VelRotX = Math.Clamp(draggedTicket.Velocity.Y * 0.015f, -8f, 8f) + (float)(_random.NextDouble() * 4.0 - 2.0);
                    draggedTicket.VelRotY = Math.Clamp(draggedTicket.Velocity.X * 0.015f, -8f, 8f) + (float)(_random.NextDouble() * 4.0 - 2.0);
                    draggedTicket.VelRotZ = Math.Clamp(draggedTicket.Velocity.X * 0.005f, -3f, 3f) + (float)(_random.NextDouble() * 2.0 - 1.0);

                    draggedTicket.FlutterPhase = (float)(_random.NextDouble() * MathHelper.TwoPi);
                    draggedTicket.FlutterSpeed = (float)(_random.NextDouble() * 2.0 + 1.5);
                    draggedTicket = null;
                }
            }

            bool hoveringTicket = false;
            for (int i = _tickets.Count - 1; i >= 0; i--)
            {
                var t = _tickets[i];

                float targetScale = t.IsDragging ? 1.10f : 1.0f;
                t.Scale = MathHelper.Lerp(t.Scale, targetScale, 15f * dt);

                if (!t.IsDispensed)
                {
                    UpdateTicketDispense(t, dt);
                }
                else
                {
                    if (!t.IsDragging && !t.IsHanging)
                    {
                        // Air resistance (Drag)
                        t.Velocity.X *= MathF.Max(0f, 1f - 3f * dt);
                        t.Velocity.Y *= MathF.Max(0f, 1f - 3f * dt);

                        // Gravity
                        t.Velocity.Y += 400f * dt;

                        // Terminal velocity (leaf-like, slow fall)
                        if (t.Velocity.Y > 100f) t.Velocity.Y = 100f;

                        // Flutter (Sway)
                        t.FlutterPhase += t.FlutterSpeed * dt;
                        t.Velocity.X += MathF.Sin(t.FlutterPhase) * 120f * dt;

                        // Rotational drag (light drag so it keeps tumbling)
                        t.VelRotX *= MathF.Max(0f, 1f - 1.5f * dt);
                        t.VelRotY *= MathF.Max(0f, 1f - 1.5f * dt);
                        t.VelRotZ *= MathF.Max(0f, 1f - 1.5f * dt);

                        // Rotational flutter (adds continuous tumbling energy)
                        t.VelRotX += MathF.Sin(t.FlutterPhase * 1.3f) * 4f * dt;
                        t.VelRotY += MathF.Cos(t.FlutterPhase * 1.1f) * 4f * dt;
                        t.VelRotZ += MathF.Sin(t.FlutterPhase * 0.8f) * 1.5f * dt;

                        t.RotX += t.VelRotX * dt;
                        t.RotY += t.VelRotY * dt;
                        t.RotZ += t.VelRotZ * dt;

                        t.Position += t.Velocity * dt;

                        if (t.Position.X < -100 || t.Position.X > Global.VIRTUAL_WIDTH + 100 ||
                            t.Position.Y < -100 || t.Position.Y > Global.VIRTUAL_HEIGHT + 100)
                        {
                            _tickets.RemoveAt(i);
                            continue;
                        }
                    }

                    if (!hoveringTicket && draggedTicket == null)
                    {
                        Matrix transform = Matrix.CreateTranslation(-t.Position.X, -t.Position.Y, 0) *
                                           Matrix.CreateRotationZ(-t.RotZ) *
                                           Matrix.CreateTranslation(t.Position.X, t.Position.Y, 0);
                        Vector2 localMouse = Vector2.Transform(virtualMousePos, transform);

                        float cosX = MathF.Cos(t.RotX);
                        float cosY = MathF.Cos(t.RotY);
                        int w = (int)(19 * t.Scale * Math.Abs(cosY));
                        int h = (int)(31 * t.Scale * Math.Abs(cosX));

                        w = Math.Max(w, 4);
                        h = Math.Max(h, 4);

                        Rectangle localBounds = new Rectangle((int)t.Position.X - w / 2, (int)t.Position.Y - h / 2, w, h);

                        if (localBounds.Contains(localMouse))
                        {
                            hoveringTicket = true;
                        }
                    }
                }
            }

            if (hoveringTicket || draggedTicket != null)
            {
                ServiceLocator.Get<CursorManager>().SetState(draggedTicket != null ? CursorState.Dragging : CursorState.HoverDraggable);
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

                if (_arenaState == ArenaState.Fighting)
                {
                    UpdateDynamicOdds();

                    int currentAliveCount = _wizards.Count(w => w.State != WizardState.Dead);

                    var playerWizard = _wizards.FirstOrDefault(w => w.IsPlayer);
                    if (playerWizard != null && !_playerTicketPrinted && playerWizard.State == WizardState.Dead)
                    {
                        _playerTicketPrinted = true;
                        PrintTicket(playerWizard, currentAliveCount + 1);
                    }

                    if (currentAliveCount <= 1)
                    {
                        _arenaState = ArenaState.MatchOver;
                        _matchOverTimer = 4.0f;

                        var winner = _wizards.FirstOrDefault(w => w.State != WizardState.Dead);
                        if (winner != null)
                        {
                            _matchResultText = $"{winner.Name.ToUpper()} WINS!";
                            if (winner.IsPlayer && !_playerTicketPrinted)
                            {
                                _playerTicketPrinted = true;
                                PrintTicket(winner, 1);
                            }
                        }
                        else
                        {
                            _matchResultText = "DRAW";
                            _matchResultColor = _global.Palette_Gray;
                            if (playerWizard != null && !_playerTicketPrinted)
                            {
                                _playerTicketPrinted = true;
                                PrintTicket(playerWizard, 2);
                            }
                        }

                        _gameState.AdvanceDay();
                    }
                }
                else if (_arenaState == ArenaState.MatchOver)
                {
                    _matchOverTimer -= dt;
                    if (_matchOverTimer <= 0 && !_transitionManager.IsTransitioning)
                    {
                        _sceneManager.ChangeScene(GameSceneState.DayPrep, TransitionType.FadeOff, TransitionType.FadeOff);
                    }
                }
            }
            else
            {
                foreach (var w in _wizards)
                {
                    w.HopTimer += dt * 2f;
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
                if (w.State != WizardState.Dead && w.CurrentHP > 0)
                {
                    totalDynamicRating += w.Rating * ((float)w.CurrentHP / w.MaxHP);
                }
            }

            foreach (var w in _wizards)
            {
                if (w.State == WizardState.Dead || w.CurrentHP <= 0)
                {
                    _winProbabilities[w] = 0f;
                }
                else if (totalDynamicRating > 0)
                {
                    float dynamicRating = w.Rating * ((float)w.CurrentHP / w.MaxHP);
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

        private Vector2 RotateOffset(Vector2 offset, float rot)
        {
            float cos = MathF.Cos(rot);
            float sin = MathF.Sin(rot);
            return new Vector2(offset.X * cos - offset.Y * sin, offset.X * sin + offset.Y * cos);
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

            if (_hoveredHudWizard != null && _hoveredHudWizard.State != WizardState.Dead)
            {
                Vector2 mousePos = Core.TransformMouse(_inputManager.GetEffectiveMouseState().Position);
                var points = SpriteBatchExtensions.GetBresenhamLinePoints(mousePos, _hoveredHudWizard.Position);
                int offset = (int)(gameTime.TotalGameTime.TotalSeconds * 30) % 6;
                for (int i = offset; i < points.Count; i += 6)
                {
                    spriteBatch.Draw(pixel, new Vector2(points[i].X, points[i].Y), _global.Palette_Fruit);
                }
            }

            foreach (var wizard in _wizards)
            {
                wizard.DrawUI(spriteBatch, _spriteManager, gameTime);
                wizard.DrawDebug(spriteBatch, _spriteManager);
            }

            DrawSideHUD(spriteBatch);
            DrawTopHUD(spriteBatch);

            var mainFont = ServiceLocator.Get<Core>().DefaultFont;
            var secFont = ServiceLocator.Get<Core>().SecondaryFont;
            var tertFont = ServiceLocator.Get<Core>().TertiaryFont;
            var ticketSheet = _spriteManager.BetTicketSpriteSheet;

            foreach (var ticket in _tickets)
            {
                Vector2 origin = new Vector2(10f, 16f);

                float cosX = MathF.Cos(ticket.RotX);
                float cosY = MathF.Cos(ticket.RotY);

                bool isBackside = (cosX * cosY) < 0;

                // Frame 0 is front background, Frame 1 is back background
                Rectangle sourceRect = isBackside
                    ? new Rectangle(1 * 19, 0, 19, 31)
                    : new Rectangle(0 * 19, 0, 19, 31);

                // Clamp the minimum scale so it looks like a thin edge instead of shrinking to a tiny dot
                float absScaleX = Math.Max(0.15f, Math.Abs(cosY)) * ticket.Scale;
                float absScaleY = Math.Max(0.15f, Math.Abs(cosX)) * ticket.Scale;
                Vector2 finalScale = new Vector2(absScaleX, absScaleY);

                SpriteEffects effects = SpriteEffects.None;
                if (cosY < 0) effects |= SpriteEffects.FlipHorizontally;
                if (cosX < 0) effects |= SpriteEffects.FlipVertically;

                float normalZ = Math.Abs(cosX * cosY);
                float brightness = 0.4f + 0.6f * normalZ;

                Color ticketColor = new Color((int)(255 * brightness), (int)(255 * brightness), (int)(255 * brightness), 255);
                Color textColor = new Color((int)(_global.Palette_Black.R * brightness), (int)(_global.Palette_Black.G * brightness), (int)(_global.Palette_Black.B * brightness), 255);

                if (ticketSheet != null)
                {
                    // Draw base ticket
                    spriteBatch.DrawSnapped(ticketSheet, ticket.Position, sourceRect, ticketColor, ticket.RotZ, origin, finalScale, effects, 0f);

                    // Draw overlay sticker if applicable
                    if (!isBackside && ticket.Placement >= 1 && ticket.Placement <= 3)
                    {
                        int overlayFrameIndex = ticket.Placement + 1; // 1st -> Frame 2, 2nd -> Frame 3, 3rd -> Frame 4
                        Rectangle overlayRect = new Rectangle(overlayFrameIndex * 19, 0, 19, 31);
                        spriteBatch.DrawSnapped(ticketSheet, ticket.Position, overlayRect, ticketColor, ticket.RotZ, origin, finalScale, effects, 0f);
                    }
                }
                else
                {
                    Color fallbackColor = new Color((int)(_global.Palette_Pale.R * brightness), (int)(_global.Palette_Pale.G * brightness), (int)(_global.Palette_Pale.B * brightness), 255);
                    spriteBatch.DrawSnapped(pixel, ticket.Position, new Rectangle(0, 0, 19, 31), fallbackColor, ticket.RotZ, origin, finalScale, effects, 0f);
                }

                if (!isBackside)
                {
                    string numText = ticket.Placement.ToString();
                    string sufText = GetOrdinalSuffix(ticket.Placement);

                    Vector2 numSize = mainFont.MeasureString(numText);
                    Vector2 sufSize = tertFont.MeasureString(sufText);

                    float totalWidth = numSize.X + sufSize.X;

                    // Shift pivot X by +2 to move the text 2 pixels to the left
                    Vector2 pivot = new Vector2(MathF.Round(totalWidth / 2f) + 2f, MathF.Round(numSize.Y / 2f));

                    Vector2 numOrigin = new Vector2(MathF.Round(pivot.X), MathF.Round(pivot.Y));
                    Vector2 sufOrigin = new Vector2(MathF.Round(pivot.X - numSize.X), MathF.Round(pivot.Y));

                    spriteBatch.DrawStringSnapped(mainFont, numText, ticket.Position, textColor, ticket.RotZ, numOrigin, finalScale, SpriteEffects.None, 0f);
                    spriteBatch.DrawStringSnapped(tertFont, sufText, ticket.Position, textColor, ticket.RotZ, sufOrigin, finalScale, SpriteEffects.None, 0f);
                }
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

                    spriteBatch.DrawStringSnapped(mainFont, countText, countPos + countOrigin, countColor, countRot, countOrigin, countScale, SpriteEffects.None, 0f);
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

                spriteBatch.DrawStringSnapped(mainFont, text, pos + origin, _global.Palette_Sun * alpha, rot, origin, scale, SpriteEffects.None, 0f);
            }
            else if (_arenaState == ArenaState.MatchOver)
            {
                string text = _matchResultText;
                Color textColor = _matchResultColor;

                if (text.EndsWith("WINS!"))
                {
                    float flash = (float)(Math.Sin(gameTime.TotalGameTime.TotalSeconds * 15f) + 1f) / 2f;
                    textColor = Color.Lerp(_global.Palette_Sky, _global.Palette_Sun, flash);
                }

                Vector2 size = mainFont.MeasureString(text);
                Vector2 pos = new Vector2(MathF.Round(_arenaCenter.X - size.X / 2f), MathF.Round(_arenaCenter.Y - size.Y / 2f - 10));

                spriteBatch.DrawStringSnapped(mainFont, text, pos, textColor);
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

            float amountWidth = MathF.Round(defaultFont.MeasureString(amountText).Width);
            float yOffset = MathF.Round(MathF.Max(0, defaultFont.LineHeight - tertiaryFont.LineHeight));

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

            int aliveCount = _wizards.Count(wiz => wiz.State != WizardState.Dead);
            bool showProbabilities = _arenaState == ArenaState.Countdown || (_arenaState == ArenaState.Fighting && aliveCount > 1);

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

                Rectangle hudRect = new Rectangle((int)_hudBaseX - 2, (int)w.HudNamePos.Y - 2, (int)(_hudMultCenterX - _hudBaseX + 30), 20);
                if (_hoveredHudWizard == w)
                {
                    spriteBatch.Draw(pixel, hudRect, _global.Palette_DarkShadow * 0.5f);
                }

                Color baseNameColor = w.IsPlayer ? _global.Palette_DarkPale : _global.Palette_DarkestPale;
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

                if (w.State != WizardState.Dead && showProbabilities)
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

                    float probYOffset = MathF.Round(MathF.Max(0, (defaultFont.LineHeight - probSize.Y) / 2f));
                    Vector2 drawPos = new Vector2(MathF.Round(_hudMultCenterX + shakeX), MathF.Round(finalNamePos.Y + probYOffset + pivot.Y));

                    spriteBatch.DrawStringSnapped(probFont, probText, drawPos, probColor, pRot, probOrigin, pScale, SpriteEffects.None, 0f);
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