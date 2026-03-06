using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class ArenaScene : GameScene
    {
        private class ArenaWizard
        {
            public Vector2 Position;
            public Vector2 TargetPosition;
            public float Speed;
            public int PortraitIndex;
            public bool IsPlayer;
            public float HopTimer;
        }

        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly GameState _gameState;
        private readonly InputManager _inputManager;
        private readonly SceneManager _sceneManager;

        private readonly List<ArenaWizard> _wizards = new List<ArenaWizard>();
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

        public override void Enter()
        {
            base.Enter();
            _wizards.Clear();
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

            var selectedIds = availableIds.OrderBy(x => _random.Next()).Take(7).ToList();
            selectedIds.Insert(0, playerEntry.Key ?? "0");

            for (int i = 0; i < 8; i++)
            {
                string id = selectedIds[i];
                if (!GameDataCache.WizardCats.TryGetValue(id, out var data)) continue;

                float angle = (i / 8f) * MathHelper.TwoPi;
                float spawnRadius = GetMaxRadiusAtAngle(angle, 10f);
                Vector2 spawnPos = _arenaCenter + new Vector2(MathF.Cos(angle) * spawnRadius, MathF.Sin(angle) * spawnRadius);

                _wizards.Add(new ArenaWizard
                {
                    Position = spawnPos,
                    TargetPosition = spawnPos,
                    Speed = data.Agility * 5f + 10f,
                    PortraitIndex = int.TryParse(data.MemberID, out int pid) ? pid : 0,
                    IsPlayer = (i == 0),
                    HopTimer = (float)(_random.NextDouble() * MathHelper.TwoPi)
                });
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
                foreach (var wizard in _wizards)
                {
                    float dist = Vector2.Distance(wizard.Position, wizard.TargetPosition);
                    if (dist < 1f)
                    {
                        Vector2 target;
                        do
                        {
                            float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                            float radius = ARENA_RADIUS * (float)Math.Sqrt(_random.NextDouble());
                            target = _arenaCenter + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
                        }
                        while (Vector2.Distance(_arenaCenter, target) > GetMaxRadiusAtAngle(MathF.Atan2(target.Y - _arenaCenter.Y, target.X - _arenaCenter.X), 4f));

                        wizard.TargetPosition = target;
                    }

                    Vector2 dir = wizard.TargetPosition - wizard.Position;
                    if (dir.LengthSquared() > 0)
                    {
                        dir.Normalize();
                        wizard.Position += dir * wizard.Speed * dt;
                        wizard.HopTimer += dt * wizard.Speed * 0.25f;
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
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off);

            if (_arenaTexture != null)
            {
                Vector2 origin = new Vector2(_arenaTexture.Width / 2f, _arenaTexture.Height / 2f);
                spriteBatch.DrawSnapped(_arenaTexture, _arenaCenter, null, _global.Palette_Black, 0f, origin, 1f, SpriteEffects.None, 0f);
            }

            var sheet = _spriteManager.PlayerMasterSpriteSheet;
            if (sheet != null)
            {
                Vector2 origin = new Vector2(16, 16);
                foreach (var wizard in _wizards.OrderBy(w => w.Position.Y))
                {
                    var sourceRect = _spriteManager.GetPlayerSourceRect(wizard.PortraitIndex, PlayerSpriteType.Portrait8x8);

                    float hopOffset = -MathF.Abs(MathF.Sin(wizard.HopTimer)) * 4f;
                    Vector2 drawPos = new Vector2(MathF.Round(wizard.Position.X), MathF.Round(wizard.Position.Y + hopOffset));

                    if (wizard.IsPlayer)
                    {
                        var silhouette = _spriteManager.PlayerMasterSpriteSheetSilhouette;
                        if (silhouette != null)
                        {
                            spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(-1, 0), sourceRect, _global.Palette_Sun, 0f, origin, 1f, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(1, 0), sourceRect, _global.Palette_Sun, 0f, origin, 1f, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(0, -1), sourceRect, _global.Palette_Sun, 0f, origin, 1f, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(0, 1), sourceRect, _global.Palette_Sun, 0f, origin, 1f, SpriteEffects.None, 0f);
                        }
                    }

                    spriteBatch.DrawSnapped(sheet, drawPos, sourceRect, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);
                }
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
    }
}