using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Animations;
using ProjectVagabond.Battle;
using ProjectVagabond.Particles;
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

        private readonly List<ArenaWizard> _wizards = new List<ArenaWizard>();
        private readonly List<ActiveAttack> _activeAttacks = new List<ActiveAttack>();
        private readonly Random _random = new Random();

        private float _stateTimer = 0f;
        private const float ARENA_RADIUS = 85f;
        private int _arenaEdges = 8;
        private int _arenaBevel = 3;
        private Vector2 _arenaCenter;
        private Texture2D _arenaTexture;

        public ArenaScene()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        private float GetMaxRadiusAtAngle(float angle, float margin)
        {
            if (angle < 0) angle += MathHelper.TwoPi;
            float sectorAngle = MathHelper.TwoPi / _arenaEdges;
            float effectiveRadius = ARENA_RADIUS - margin;

            float apothem = effectiveRadius * MathF.Cos(sectorAngle / 2f);
            float bevelApothem = effectiveRadius - _arenaBevel;

            float localAngleEdge = Math.Abs((angle % sectorAngle) - (sectorAngle / 2f));
            float maxDistEdge = apothem / MathF.Cos(localAngleEdge);

            float closestVertexAngle = MathF.Round(angle / sectorAngle) * sectorAngle;
            float localAngleVertex = Math.Abs(angle - closestVertexAngle);
            float maxDistBevel = bevelApothem / MathF.Cos(localAngleVertex);

            return Math.Min(maxDistEdge, maxDistBevel);
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
            while (Vector2.Distance(_arenaCenter, target) > GetMaxRadiusAtAngle(MathF.Atan2(target.Y - _arenaCenter.Y, target.X - _arenaCenter.X), 4f));

            return target;
        }

        public IEnumerable<ArenaWizard> GetAllWizards() => _wizards;

        public IEnumerable<ArenaWizard> GetWizardsInCircle(Vector2 center, float radius)
        {
            return _wizards.Where(w => w.State != WizardState.Dead && CollisionMath.RectangleIntersectsCircle(w.GetHitbox(_spriteManager), center, radius));
        }

        public IEnumerable<ArenaWizard> GetWizardsInOBB(Vector2 origin, Vector2 direction, float width, float length)
        {
            return _wizards.Where(w => w.State != WizardState.Dead && CollisionMath.AABBIntersectsOBB(w.GetHitbox(_spriteManager), origin, direction, width, length));
        }

        public void SpawnAttack(ActiveAttack attack)
        {
            attack.DeliveryInstance.Start(attack);
            attack.Animation?.Start(attack, this);
            _activeAttacks.Add(attack);
        }

        public override void Enter()
        {
            base.Enter();
            _wizards.Clear();
            _activeAttacks.Clear();
            _stateTimer = 0f;
            _arenaCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);
            _arenaEdges = Math.Max(3, _arenaEdges);

            _arenaTexture?.Dispose();
            _arenaTexture = ServiceLocator.Get<TextureFactory>().CreatePolygonTexture((int)ARENA_RADIUS, _arenaEdges, _arenaBevel);

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
                float spawnRadius = GetMaxRadiusAtAngle(angle, 10f);
                Vector2 spawnPos = _arenaCenter + new Vector2(MathF.Cos(angle) * spawnRadius, MathF.Sin(angle) * spawnRadius);

                var wizard = new ArenaWizard();
                wizard.Initialize(data, spawnPos, i == 0);
                _wizards.Add(wizard);
            }
        }

        public override void Exit()
        {
            base.Exit();
            _arenaTexture?.Dispose();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _stateTimer += dt;

            if (_stateTimer >= 3.0f)
            {
                Vector2 virtualMousePos = Core.TransformMouse(_inputManager.GetEffectiveMouseState().Position);

                foreach (var wizard in _wizards)
                {
                    wizard.IsHovered = wizard.State != WizardState.Dead && wizard.GetHitbox(_spriteManager).Contains(virtualMousePos);
                    wizard.Update(dt, this);
                }

                HashSet<ArenaWizard> overlappingWizards = new HashSet<ArenaWizard>();

                for (int i = 0; i < _wizards.Count; i++)
                {
                    var w1 = _wizards[i];
                    if (w1.State == WizardState.Dead) continue;

                    var rect1 = w1.GetHitbox(_spriteManager);

                    for (int j = i + 1; j < _wizards.Count; j++)
                    {
                        var w2 = _wizards[j];
                        if (w2.State == WizardState.Dead) continue;

                        var rect2 = w2.GetHitbox(_spriteManager);

                        if (rect1.Intersects(rect2))
                        {
                            overlappingWizards.Add(w1);
                            overlappingWizards.Add(w2);

                            if (!w1.IsSparPassive && !w2.IsSparPassive && w1.State == WizardState.Moving && w2.State == WizardState.Moving)
                            {
                                float totalAgility = w1.Agility + w2.Agility;
                                float p1Wins = totalAgility > 0 ? (float)w1.Agility / totalAgility : 0.5f;
                                bool w1Wins = _random.NextDouble() < p1Wins;

                                w1.InitiateSpar(w2, w1Wins, this);
                                w2.InitiateSpar(w1, !w1Wins, this);
                            }
                        }
                    }
                }

                foreach (var w in _wizards)
                {
                    if (w.IsSparPassive && w.SparCooldownTimer <= 0)
                    {
                        if (!overlappingWizards.Contains(w))
                        {
                            w.IsSparPassive = false;
                        }
                    }
                }

                for (int i = _activeAttacks.Count - 1; i >= 0; i--)
                {
                    var attack = _activeAttacks[i];

                    if (attack.Animation != null)
                    {
                        attack.Animation.Update(dt, this, attack);
                        if (attack.Animation.HasTriggeredImpact && !attack.HasTriggeredImpact)
                        {
                            attack.HasTriggeredImpact = true;
                            attack.DeliveryInstance.TriggerImpact(this, attack);
                        }
                    }
                    else if (!attack.HasTriggeredImpact)
                    {
                        attack.HasTriggeredImpact = true;
                        attack.DeliveryInstance.TriggerImpact(this, attack);
                    }

                    attack.DeliveryInstance.Update(dt, this, attack);

                    bool animFinished = attack.Animation == null || attack.Animation.IsFinished;
                    if (attack.DeliveryInstance.IsFinished && animFinished)
                    {
                        _activeAttacks.RemoveAt(i);
                    }
                }
            }

            if (_inputManager.Back)
            {
                _sceneManager.ChangeScene(GameSceneState.MainMenu, Transitions.TransitionType.FadeOff, Transitions.TransitionType.FadeOff);
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Black);

            if (_arenaTexture != null)
            {
                Vector2 origin = new Vector2(_arenaTexture.Width / 2f, _arenaTexture.Height / 2f);
                spriteBatch.DrawSnapped(_arenaTexture, _arenaCenter, null, _global.Palette_Off, 0f, origin, 1f, SpriteEffects.None, 0f);
            }

            foreach (var wizard in _wizards.Where(w => w.State == WizardState.Dead).OrderBy(w => w.Position.Y))
            {
                DrawWizard(spriteBatch, wizard);
            }

            foreach (var attack in _activeAttacks)
            {
                attack.DeliveryInstance.Draw(spriteBatch, attack);
            }

            foreach (var wizard in _wizards.Where(w => w.State != WizardState.Dead).OrderBy(w => w.Position.Y))
            {
                DrawWizard(spriteBatch, wizard);
            }

            // Draw animations ON TOP of living wizards
            foreach (var attack in _activeAttacks)
            {
                attack.Animation?.Draw(spriteBatch, attack);
            }

            // --- DRAW PARTICLES ---
            // Interrupt the current batch so the particle system can use its own blend states and sorting
            spriteBatch.End();
            ServiceLocator.Get<ParticleSystemManager>().Draw(spriteBatch, transform);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
            // ----------------------

            foreach (var wizard in _wizards)
            {
                wizard.DrawUI(spriteBatch, _spriteManager);
                wizard.DrawDebug(spriteBatch, _spriteManager);
            }

            string text = "";
            if (_stateTimer < 1f) text = "3";
            else if (_stateTimer < 2f) text = "2";
            else if (_stateTimer < 3f) text = "1";
            else if (_stateTimer < 4f) text = "FIGHT!";

            if (!string.IsNullOrEmpty(text))
            {
                var mainFont = ServiceLocator.Get<Core>().DefaultFont;
                Vector2 size = mainFont.MeasureString(text);
                Vector2 pos = _arenaCenter - (size / 2f);
                spriteBatch.DrawStringOutlinedSnapped(mainFont, text, pos, _global.Palette_Sun, _global.Palette_DarkShadow);
            }
        }

        private void DrawWizard(SpriteBatch spriteBatch, ArenaWizard wizard)
        {
            var sheet = _spriteManager.PlayerMasterSpriteSheet;
            if (sheet == null) return;

            Vector2 origin = new Vector2(16, 16);
            var sourceRect = _spriteManager.GetPlayerSourceRect(wizard.PortraitIndex, PlayerSpriteType.Portrait8x8);

            bool isDead = wizard.State == WizardState.Dead;
            float hopOffset = isDead ? 0f : -MathF.Abs(MathF.Sin(wizard.HopTimer)) * 4f;
            Vector2 drawPos = new Vector2(MathF.Round(wizard.Position.X), MathF.Round(wizard.Position.Y + hopOffset));
            float rotation = isDead ? MathHelper.PiOver2 : 0f;

            float alpha = wizard.GetDeathAlpha();
            Color color = isDead ? (Color.Gray * alpha) : Color.White;

            bool isInvincibleFlash = wizard.InvincibilityTimer > 0 && (wizard.InvincibilityTimer % 0.16f) > 0.08f;

            if (wizard.IsPlayer && !isDead)
            {
                var silhouette = _spriteManager.PlayerMasterSpriteSheetSilhouette;
                if (silhouette != null)
                {
                    spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(-1, 0), sourceRect, _global.Palette_Sun, rotation, origin, 1f, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(1, 0), sourceRect, _global.Palette_Sun, rotation, origin, 1f, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(0, -1), sourceRect, _global.Palette_Sun, rotation, origin, 1f, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(0, 1), sourceRect, _global.Palette_Sun, rotation, origin, 1f, SpriteEffects.None, 0f);
                }
            }

            if (isInvincibleFlash && !isDead)
            {
                var silhouette = _spriteManager.PlayerMasterSpriteSheetSilhouette;
                if (silhouette != null)
                {
                    spriteBatch.DrawSnapped(silhouette, drawPos, sourceRect, _global.Palette_Sun, rotation, origin, 1f, SpriteEffects.None, 0f);
                }
                else
                {
                    spriteBatch.DrawSnapped(sheet, drawPos, sourceRect, _global.Palette_Sun, rotation, origin, 1f, SpriteEffects.None, 0f);
                }
            }
            else
            {
                spriteBatch.DrawSnapped(sheet, drawPos, sourceRect, color, rotation, origin, 1f, SpriteEffects.None, 0f);
            }
        }
    }
}