using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Scenes
{
    public class LeagueButton : Button
    {
        public ArenaTier Tier { get; }
        public bool IsAffordable { get; set; }
        public bool IsClosed { get; set; }

        public LeagueButton(Rectangle bounds, ArenaTier tier) : base(bounds, "")
        {
            Tier = tier;
            EnableHoverSway = false;
            HoverAnimation = HoverAnimationType.None;
            TriggerHapticOnHover = false;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var core = ServiceLocator.Get<Core>();
            var secondaryFont = core.SecondaryFont;
            var tertiaryFont = core.TertiaryFont;
            var global = ServiceLocator.Get<Global>();

            bool isHovered = IsHovered || IsSelected || forceHover;

            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);

            float scale = _currentScale;
            if (scale < 0.01f) return;

            // Hardcode visual size to 144x24 so the extended hitbox doesn't stretch the rendering
            int width = (int)(144 * scale);
            int height = (int)(24 * scale);

            float totalX = Bounds.Center.X + (horizontalOffset ?? 0f);
            float totalY = Bounds.Center.Y + (verticalOffset ?? 0f);

            Rectangle scaledBounds = new Rectangle(
                (int)(totalX - width / 2f),
                (int)(totalY - height / 2f),
                width,
                height
            );

            if (IsClosed)
            {
                DrawBeveledCornersRect(spriteBatch, pixel, scaledBounds, global.Palette_DarkestPale * 0.25f);
                DrawNormalText(spriteBatch, defaultFont, secondaryFont, tertiaryFont, global, 0.25f, scaledBounds, scale);

                spriteBatch.DrawLineSnapped(new Vector2(scaledBounds.Left + 2, scaledBounds.Center.Y), new Vector2(scaledBounds.Right - 2, scaledBounds.Center.Y), global.Palette_Black * 0.5f, 2f);
            }
            else if (!IsAffordable)
            {
                if (isHovered)
                {
                    // Even more transparent when hovered
                    spriteBatch.DrawAnimatedDottedRectangle(pixel, scaledBounds, global.Palette_DarkestPale * 0.22f, 1f, 1f, 1f, 0f);
                    DrawNormalText(spriteBatch, defaultFont, secondaryFont, tertiaryFont, global, 0.1f, scaledBounds, scale);

                    string text = "CANNOT AFFORD";
                    Color textColor = _isPressed ? global.Palette_Rust : global.Palette_DarkRust;
                    Vector2 size = defaultFont.MeasureString(text) * scale;

                    float textShakeX = shakeOffset.X * 2.5f;
                    Vector2 pos = new Vector2(scaledBounds.Center.X - size.X / 2f + textShakeX, scaledBounds.Center.Y - size.Y / 2f);

                    spriteBatch.DrawStringOutlinedSnapped(defaultFont, text, pos, textColor, global.Palette_Off, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
                else
                {
                    // Same transparency as the old hovered state
                    spriteBatch.DrawAnimatedDottedRectangle(pixel, scaledBounds, global.Palette_DarkestPale * 0.25f, 1f, 1f, 1f, 0f);
                    DrawNormalText(spriteBatch, defaultFont, secondaryFont, tertiaryFont, global, 0.25f, scaledBounds, scale);
                }
            }
            else
            {
                DrawBeveledCornersRect(spriteBatch, pixel, scaledBounds, global.Palette_DarkestPale);
                DrawNormalText(spriteBatch, defaultFont, secondaryFont, tertiaryFont, global, 1f, scaledBounds, scale);

                if (isHovered)
                {
                    DrawBeveledCornersRect(spriteBatch, pixel, scaledBounds, _global.Palette_Sun * 0.35f);
                }
            }

            if (flashTint.HasValue)
            {
                spriteBatch.Draw(pixel, scaledBounds, flashTint.Value);
            }
        }

        private void DrawBeveledCornersRect(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y, rect.Width - 2, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 1, rect.Width - 2, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + 1, rect.Width, rect.Height - 2), color);
        }

        private void DrawNormalText(SpriteBatch spriteBatch, BitmapFont defaultFont, BitmapFont secondaryFont, BitmapFont tertiaryFont, Global global, float alpha, Rectangle bounds, float scale)
        {
            Color nameColor = GetLeagueColor(Tier.Name, global) * alpha;
            Color offOutline = global.Palette_Off * alpha;

            Vector2 namePos = new Vector2(bounds.X + 3 * scale, bounds.Y + 2 * scale);
            spriteBatch.DrawStringOutlinedSnapped(defaultFont, Tier.Name.ToUpper(), namePos, nameColor, offOutline, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            Vector2 feePos = new Vector2(bounds.X + 3 * scale, bounds.Y + 14 * scale);
            spriteBatch.DrawStringOutlinedSnapped(secondaryFont, "FEE", feePos, global.Palette_Sun * alpha, offOutline, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            float feeWidth = secondaryFont.MeasureString("FEE").Width * scale;

            string valText = $"-{Tier.EntryFee}";
            Vector2 valPos = new Vector2(feePos.X + feeWidth + 4 * scale, feePos.Y);
            spriteBatch.DrawStringOutlinedSnapped(secondaryFont, valText, valPos, global.Palette_DarkRust * alpha, offOutline, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            float valWidth = secondaryFont.MeasureString(valText).Width * scale;

            float gYOffset = MathF.Round(MathF.Max(0, (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f)) + 1f;
            Vector2 gPos = new Vector2(valPos.X + valWidth + 1 * scale, feePos.Y + gYOffset * scale);
            spriteBatch.DrawStringOutlinedSnapped(tertiaryFont, "G", gPos, global.Palette_DarkSun * alpha, offOutline, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private Color GetLeagueColor(string name, Global global)
        {
            switch (name.ToUpper())
            {
                case "IRON": return global.LeagueColor_Iron;
                case "SILVER": return global.LeagueColor_Silver;
                case "GOLD": return global.LeagueColor_Gold;
                case "PLATINUM": return global.LeagueColor_Platinum;
                case "DIAMOND": return global.LeagueColor_Diamond;
                default: return global.Palette_Sun;
            }
        }
    }

    public class DayPrepScene : GameScene
    {
        private readonly Global _global;
        private readonly GameState _gameState;
        private readonly SceneManager _sceneManager;
        private readonly TransitionManager _transitionManager;
        private readonly InputManager _inputManager;
        private readonly HapticsManager _hapticsManager;

        private float _timer;
        private bool _isBankrupt;

        private List<LeagueButton> _tierButtons = new List<LeagueButton>();
        private NavigationGroup _navigationGroup;

        public DayPrepScene()
        {
            _global = ServiceLocator.Get<Global>();
            _gameState = ServiceLocator.Get<GameState>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _navigationGroup = new NavigationGroup(wrapNavigation: false);
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Initialize()
        {
            base.Initialize();

            _tierButtons.Clear();
            _navigationGroup.Clear();

            int startY = 35;
            int spacingY = 26;

            for (int i = 0; i < GameState.ArenaTiers.Count; i++)
            {
                var tier = GameState.ArenaTiers[i];
                var btn = new LeagueButton(
                    new Rectangle(Global.VIRTUAL_WIDTH / 2 - 72, startY + i * spacingY - 1, 144, 26),
                    tier
                );

                btn.OnClick += () =>
                {
                    if (_transitionManager.IsTransitioning) return;
                    if (btn.IsClosed) return;

                    if (!btn.IsAffordable)
                    {
                        btn.TriggerShake();
                        _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                        return;
                    }

                    _gameState.SelectedTier = tier;
                    _gameState.PlayerState.Gold -= tier.EntryFee;

                    _sceneManager.ChangeScene(GameSceneState.Arena, _transitionManager.GetRandomTransition(), _transitionManager.GetRandomTransition());
                };

                _tierButtons.Add(btn);
                _navigationGroup.Add(btn);
            }
        }

        public override void Enter()
        {
            base.Enter();
            _timer = 0f;

            int dailyFloor = _gameState.GetDailyFloor(_gameState.CurrentDay);
            _isBankrupt = _gameState.PlayerState.Gold < dailyFloor;

            int playerGold = _gameState.PlayerState.Gold;

            for (int i = 0; i < _tierButtons.Count; i++)
            {
                var tier = GameState.ArenaTiers[i];
                var btn = _tierButtons[i];

                btn.IsClosed = tier.EntryFee < dailyFloor;
                btn.IsAffordable = playerGold >= tier.EntryFee;
                btn.IsEnabled = !btn.IsClosed;

                btn.SetHiddenForEntrance();
                btn.PlayEntrance(2.0f + (i * 0.1f));
            }

            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse)
            {
                _navigationGroup.SelectFirst();
            }
            else
            {
                _navigationGroup.DeselectAll();
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);

            if (_transitionManager.IsTransitioning) return;

            if (_isBankrupt)
            {
                _sceneManager.ChangeScene(GameSceneState.GameOver, TransitionType.FadeOff, TransitionType.FadeOff);
                return;
            }

            float dt = (float)effectiveGameTime.ElapsedGameTime.TotalSeconds;
            _timer += dt;

            var mouseState = _inputManager.GetEffectiveMouseState();
            foreach (var btn in _tierButtons)
            {
                btn.Update(mouseState);
            }

            if (_inputManager.CurrentInputDevice == InputDeviceType.Mouse)
            {
                _navigationGroup.DeselectAll();
            }
            else
            {
                _navigationGroup.UpdateInput(_inputManager);
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);

            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.GameBg);

            var defaultFont = ServiceLocator.Get<Core>().DefaultFont;
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

            string dayText = $"DAY {_gameState.CurrentDay}";
            Vector2 daySize = defaultFont.MeasureString(dayText);

            float startY = MathF.Round(Global.VIRTUAL_HEIGHT / 2f - daySize.Y / 2f);
            float endY = 15f;
            float currentY = startY;

            if (_timer > 1.0f)
            {
                float progress = Math.Clamp((_timer - 1.0f) / 1.0f, 0f, 1f);
                currentY = MathF.Round(MathHelper.Lerp(startY, endY, Easing.EaseInOutCubic(progress)));
            }

            Vector2 dayPos = new Vector2(MathF.Round(Global.VIRTUAL_WIDTH / 2f - daySize.X / 2f), currentY);
            spriteBatch.DrawStringSnapped(defaultFont, dayText, dayPos, _global.Palette_Sun);

            foreach (var btn in _tierButtons)
            {
                btn.Draw(spriteBatch, defaultFont, effectiveGameTime, transform);
            }

            if (_timer > 1.0f)
            {
                int currentGold = _gameState.PlayerState.Gold;
                int displayGold = currentGold;
                bool isPreviewing = false;
                bool isNegativeOrZero = false;

                LeagueButton hoveredBtn = null;
                foreach (var btn in _tierButtons)
                {
                    if (btn.IsHovered || btn.IsSelected)
                    {
                        hoveredBtn = btn;
                        break;
                    }
                }

                if (hoveredBtn != null)
                {
                    isPreviewing = true;
                    displayGold = currentGold - hoveredBtn.Tier.EntryFee;
                    if (displayGold <= 0)
                    {
                        isNegativeOrZero = true;
                    }
                }

                string amountText = displayGold.ToString();
                string gText = "G";

                Color amountColor = _global.Palette_Sun;
                Color gColor = _global.Palette_DarkSun;

                if (isPreviewing)
                {
                    float flash = (float)(Math.Sin(effectiveGameTime.TotalGameTime.TotalSeconds * 15f) + 1f) / 2f;
                    if (isNegativeOrZero)
                    {
                        amountColor = Color.Lerp(_global.Palette_Rust, _global.Palette_DarkRust, flash);
                        gColor = amountColor;
                    }
                    else
                    {
                        amountColor = Color.Lerp(_global.Palette_Sun, _global.Palette_DarkSun, flash);
                        gColor = amountColor;
                    }
                }

                Vector2 amountPos = new Vector2(10, 10);
                spriteBatch.DrawStringSnapped(defaultFont, amountText, amountPos, amountColor);

                float amountWidth = MathF.Round(defaultFont.MeasureString(amountText).Width);
                float gYOffset = MathF.Round(MathF.Max(0, defaultFont.LineHeight - tertiaryFont.LineHeight));

                Vector2 gPos = new Vector2(amountPos.X + amountWidth + 2, 10 + gYOffset);
                spriteBatch.DrawStringSnapped(tertiaryFont, gText, gPos, gColor);
            }
        }
    }
}
