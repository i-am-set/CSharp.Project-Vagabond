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

            foreach (var attack in _activeAttacks)
            {
                attack.Animation?.Draw(spriteBatch, attack);
            }

            spriteBatch.End();
            ServiceLocator.Get<ParticleSystemManager>().Draw(spriteBatch, transform);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            foreach (var wizard in _wizards)
            {
                wizard.DrawUI(spriteBatch, _spriteManager);
                wizard.DrawDebug(spriteBatch, _spriteManager);
            }

            DrawSideHUD(spriteBatch);

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

        private void DrawSideHUD(SpriteBatch spriteBatch)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var sheet = _spriteManager.HealthHearts3x3SpriteSheet;
            if (sheet == null) return;

            int totalWizards = _wizards.Count;
            if (totalWizards == 0) return;

            int leftCount = (totalWizards + 1) / 2;
            int rightCount = totalWizards - leftCount;
            int spacingY = 24;
            int itemHeight = (int)secondaryFont.LineHeight + 5; // Name height + 2px gap + 3px heart

            int leftBlockHeight = (leftCount - 1) * spacingY + itemHeight;
            int rightBlockHeight = (rightCount - 1) * spacingY + itemHeight;

            int leftStartY = (Global.VIRTUAL_HEIGHT - leftBlockHeight) / 2;
            int rightStartY = (Global.VIRTUAL_HEIGHT - rightBlockHeight) / 2;

            int marginX = 12;

            for (int i = 0; i < totalWizards; i++)
            {
                var w = _wizards[i];
                bool isLeft = i < leftCount;
                int sideIndex = isLeft ? i : i - leftCount;

                float shakeX = 0f;
                float shakeY = 0f;
                if (w.HudShakeTimer > 0)
                {
                    float shakeMag = (w.HudShakeTimer / 0.4f) * 3f;
                    shakeX = (float)(_random.NextDouble() * 2 - 1) * shakeMag;
                    shakeY = (float)(_random.NextDouble() * 2 - 1) * shakeMag;
                }

                string name = w.Name.ToUpper();
                Vector2 nameSize = secondaryFont.MeasureString(name);
                Color nameColor = w.IsPlayer ? _global.Palette_Sky : _global.Palette_Sun;
                if (w.State == WizardState.Dead) nameColor = _global.Palette_DarkGray;

                int maxHearts = (w.MaxHP + 1) / 2;
                int heartWidth = 3;
                int heartSpacing = 1;
                int heartsWidth = maxHearts * heartWidth + (maxHearts - 1) * heartSpacing;

                float baseX = isLeft ? marginX : Global.VIRTUAL_WIDTH - marginX;
                float nameX = isLeft ? baseX : baseX - nameSize.X;
                float hX = isLeft ? baseX : baseX - heartsWidth;

                float currentY = (isLeft ? leftStartY : rightStartY) + sideIndex * spacingY;

                Vector2 finalNamePos = new Vector2(MathF.Round(nameX + shakeX), MathF.Round(currentY + shakeY));
                spriteBatch.DrawStringOutlinedSnapped(secondaryFont, name, finalNamePos, nameColor, _global.Palette_DarkShadow);

                float hY = currentY + nameSize.Y + 2;

                for (int h = 0; h < maxHearts; h++)
                {
                    int heartVal = Math.Clamp(w.CurrentHP - h * 2, 0, 2);
                    int frameIndex = 2;
                    if (heartVal == 2) frameIndex = 0;
                    else if (heartVal == 1) frameIndex = 1;

                    int flashFrame = w.GetHeartFlashFrame(h);
                    if (flashFrame != -1) frameIndex = flashFrame;

                    var sourceRect = new Rectangle(frameIndex * heartWidth, 0, heartWidth, 3);
                    Color heartColor = w.State == WizardState.Dead ? Color.Gray : Color.White;

                    Vector2 finalHeartPos = new Vector2(MathF.Round(hX + h * (heartWidth + heartSpacing) + shakeX), MathF.Round(hY + shakeY));
                    spriteBatch.DrawSnapped(sheet, finalHeartPos, sourceRect, heartColor);
                }
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

            bool drawSilhouette = false;
            bool skipDraw = false;

            if (wizard.InvincibilityTimer > 0 && !isDead)
            {
                float timeActive = wizard.InvincibilityDuration - wizard.InvincibilityTimer;
                int state = (int)(timeActive / 0.05f) % 3;

                if (state == 0) drawSilhouette = true;
                else if (state == 1) skipDraw = true;
            }

            if (!skipDraw)
            {
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

                if (drawSilhouette)
                {
                    var silhouette = _spriteManager.PlayerMasterSpriteSheetSilhouette;
                    spriteBatch.DrawSnapped(silhouette ?? sheet, drawPos, sourceRect, _global.Palette_Sun, rotation, origin, 1f, SpriteEffects.None, 0f);
                }
                else
                {
                    spriteBatch.DrawSnapped(sheet, drawPos, sourceRect, color, rotation, origin, 1f, SpriteEffects.None, 0f);
                }
            }
        }
    }
}