using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Particles;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Scenes
{
    public enum CardSuit { Hearts, Diamonds, Spades, Clubs, None }
    public enum CardType { Potion, Weapon, Monster, Blank, Outline, BackRed, BackBlue, Booster }

    public class Card
    {
        public CardSuit Suit { get; set; }
        public CardType Type { get; set; }
        public int Rank { get; set; }
        public int BaseValue { get; set; }
        public int Modifier { get; set; }
        public int Value => Math.Max(0, BaseValue + Modifier);

        public Vector2 Position { get; set; }
        public Vector2 TargetPosition { get; set; }
        public Vector2 Scale { get; set; }
        public Vector2 TargetScale { get; set; }
        public float Rotation { get; set; }
        public float TargetRotation { get; set; }

        public bool IsFaceUp { get; set; }
        public int ZIndex { get; set; }
        public int RoomSlotIndex { get; set; } = -1;
        public bool IsHovered { get; set; }
        public bool IsSelectable { get; set; }

        public bool IsFocused { get; set; }
        public bool ExpandHitboxX { get; set; }
        public Color? OutlineColor { get; set; }
        public bool ForceRenderAboveVeil { get; set; }
        public bool IsBeingReplaced { get; set; }
        public float VisualYOffset { get; set; }

        public Vector2 ShakeOffset { get; set; }
        public float FlashWhiteIntensity { get; set; }

        public float FlipYOffset { get; private set; }
        public float FlipRotation { get; private set; }

        public bool IsFlipping => _isFlipping;
        private bool _isFlipping;
        private bool _isFlippingHalf2;
        private float _flipTimer;
        private float _totalTime;
        private const float FLIP_HALF_DURATION = 0.075f;
        private const float LERP_SPEED = 25f;

        public Card(CardSuit suit, CardType type, int rank, int baseValue)
        {
            Suit = suit;
            Type = type;
            Rank = rank;
            BaseValue = baseValue;
            Scale = Vector2.One;
            TargetScale = Vector2.One;
            IsFaceUp = false;
        }

        public void Flip()
        {
            if (_isFlipping) return;
            _isFlipping = true;
            _isFlippingHalf2 = false;
            _flipTimer = 0f;
        }

        public void Update(float dt)
        {
            _totalTime += dt;
            float damping = 1.0f - MathF.Exp(-LERP_SPEED * dt);
            Position = Vector2.Lerp(Position, TargetPosition, damping);
            Rotation = MathHelper.Lerp(Rotation, TargetRotation, damping);

            FlashWhiteIntensity = Math.Max(0f, FlashWhiteIntensity - dt * 3f);

            if (_isFlipping)
            {
                _flipTimer += dt;
                float p = Math.Clamp(_flipTimer / FLIP_HALF_DURATION, 0f, 1f);
                float totalP = _isFlippingHalf2 ? 0.5f + (p * 0.5f) : p * 0.5f;

                // Add a juicy hop and tilt during the flip
                FlipYOffset = -MathF.Sin(totalP * MathHelper.Pi) * 12f;
                FlipRotation = MathF.Sin(totalP * MathHelper.Pi) * 0.1f;

                if (!_isFlippingHalf2)
                {
                    Scale = new Vector2(TargetScale.X * (1f - Easing.EaseInCubic(p)), MathHelper.Lerp(Scale.Y, TargetScale.Y, damping));
                    if (p >= 1f)
                    {
                        IsFaceUp = !IsFaceUp;
                        _isFlippingHalf2 = true;
                        _flipTimer = 0f;
                        FlashWhiteIntensity = 0.6f;
                    }
                }
                else
                {
                    Scale = new Vector2(TargetScale.X * Easing.EaseOutCubic(p), MathHelper.Lerp(Scale.Y, TargetScale.Y, damping));
                    if (p >= 1f)
                    {
                        _isFlipping = false;
                        Scale = new Vector2(TargetScale.X, Scale.Y);
                        FlipYOffset = 0f;
                        FlipRotation = 0f;
                    }
                }
            }
            else
            {
                FlipYOffset = 0f;
                FlipRotation = 0f;
                Scale = Vector2.Lerp(Scale, TargetScale, damping);
            }
        }

        public void Draw(SpriteBatch spriteBatch, SpriteManager spriteManager)
        {
            if (spriteManager.ScoundrelCardsSpriteSheet == null) return;

            Rectangle sourceRect;

            if (!IsFaceUp)
            {
                sourceRect = spriteManager.ScoundrelCardRects[2, 0];
            }
            else if (Type == CardType.Outline)
            {
                sourceRect = spriteManager.ScoundrelCardRects[1, 0];
            }
            else if (Type == CardType.Booster)
            {
                sourceRect = spriteManager.ScoundrelCardRects[4, Value - 1];
            }
            else
            {
                int row = 0;
                switch (Suit)
                {
                    case CardSuit.Hearts: row = 0; break;
                    case CardSuit.Diamonds: row = 1; break;
                    case CardSuit.Spades: row = 2; break;
                    case CardSuit.Clubs: row = 3; break;
                }

                int col = 0;
                if (Rank == 14) col = 1; // Ace
                else if (Rank >= 2 && Rank <= 13) col = Rank; // 2-10, J, Q, K

                sourceRect = spriteManager.ScoundrelCardRects[row, col];
            }

            Vector2 origin = new Vector2(18f, 25f);
            Vector2 drawPos = new Vector2(MathF.Round(Position.X + ShakeOffset.X), MathF.Round(Position.Y + VisualYOffset + FlipYOffset + ShakeOffset.Y));
            float finalRotation = Rotation + FlipRotation;

            if (IsHovered && !IsFocused) drawPos.Y -= 1f;

            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            if (OutlineColor.HasValue && Scale.X > 0.05f)
            {
                int w = (int)MathF.Round(38 * Scale.X);
                int h = (int)MathF.Round(52 * Scale.Y);
                int x = (int)MathF.Round(drawPos.X) - w / 2;
                int y = (int)MathF.Round(drawPos.Y) - h / 2;

                spriteBatch.Draw(pixel, new Rectangle(x + 1, y, w - 2, 1), OutlineColor.Value);
                spriteBatch.Draw(pixel, new Rectangle(x + 1, y + h - 1, w - 2, 1), OutlineColor.Value);
                spriteBatch.Draw(pixel, new Rectangle(x, y + 1, 1, h - 2), OutlineColor.Value);
                spriteBatch.Draw(pixel, new Rectangle(x + w - 1, y + 1, 1, h - 2), OutlineColor.Value);
            }

            spriteBatch.DrawSnapped(spriteManager.ScoundrelCardsSpriteSheet, drawPos, sourceRect, Color.White, finalRotation, origin, Scale, SpriteEffects.None, 0f);

            if (IsFaceUp && Type != CardType.Outline && Type != CardType.Booster && Modifier != 0)
            {
                var global = ServiceLocator.Get<Global>();
                var core = ServiceLocator.Get<Core>();
                var defFont = core.DefaultFont;

                Vector2 RotateOffset(Vector2 local)
                {
                    float cos = MathF.Cos(finalRotation);
                    float sin = MathF.Sin(finalRotation);
                    return new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos) * Scale;
                }

                string modText = Value.ToString();
                Vector2 textSize = defFont.MeasureString(modText);
                Vector2 textOrigin = Vector2.Zero;

                Color textOutlineColor = VisualYOffset < 0f ? global.Palette_DarkSun : global.Palette_Off;

                // Top Left
                Vector2 topLeftLocal = new Vector2(-16, -23);
                Vector2 topLeftOffset = RotateOffset(topLeftLocal);
                Vector2 topLeftDrawPos = new Vector2(MathF.Round(drawPos.X + topLeftOffset.X), MathF.Round(drawPos.Y + topLeftOffset.Y));

                spriteBatch.Draw(pixel, topLeftDrawPos, null, global.Palette_Off, finalRotation, textOrigin, textSize * Scale, SpriteEffects.None, 0f);
                spriteBatch.DrawStringOutlinedSnapped(defFont, modText, topLeftDrawPos, global.Palette_Sun, textOutlineColor, finalRotation, textOrigin, Scale, SpriteEffects.None, 0f);

                // Bottom Right (Rotated 180 degrees)
                Vector2 bottomRightLocal = new Vector2(16, 23);
                Vector2 bottomRightOffset = RotateOffset(bottomRightLocal);
                Vector2 bottomRightDrawPos = new Vector2(MathF.Round(drawPos.X + bottomRightOffset.X), MathF.Round(drawPos.Y + bottomRightOffset.Y));

                spriteBatch.Draw(pixel, bottomRightDrawPos, null, global.Palette_Off, finalRotation + MathHelper.Pi, textOrigin, textSize * Scale, SpriteEffects.None, 0f);
                spriteBatch.DrawStringOutlinedSnapped(defFont, modText, bottomRightDrawPos, global.Palette_Sun, textOutlineColor, finalRotation + MathHelper.Pi, textOrigin, Scale, SpriteEffects.None, 0f);
            }
        }

        public void DrawFlash(SpriteBatch spriteBatch, SpriteManager spriteManager)
        {
            if (FlashWhiteIntensity <= 0f || spriteManager.ScoundrelCardsSilhouetteSpriteSheet == null) return;

            Rectangle sourceRect;

            if (!IsFaceUp)
            {
                sourceRect = spriteManager.ScoundrelCardRects[2, 0];
            }
            else if (Type == CardType.Outline)
            {
                sourceRect = spriteManager.ScoundrelCardRects[1, 0];
            }
            else if (Type == CardType.Booster)
            {
                sourceRect = spriteManager.ScoundrelCardRects[4, Value - 1];
            }
            else
            {
                int row = 0;
                switch (Suit)
                {
                    case CardSuit.Hearts: row = 0; break;
                    case CardSuit.Diamonds: row = 1; break;
                    case CardSuit.Spades: row = 2; break;
                    case CardSuit.Clubs: row = 3; break;
                }

                int col = 0;
                if (Rank == 14) col = 1; // Ace
                else if (Rank >= 2 && Rank <= 13) col = Rank; // 2-10, J, Q, K

                sourceRect = spriteManager.ScoundrelCardRects[row, col];
            }

            Vector2 origin = new Vector2(18f, 25f);
            Vector2 drawPos = new Vector2(MathF.Round(Position.X + ShakeOffset.X), MathF.Round(Position.Y + VisualYOffset + FlipYOffset + ShakeOffset.Y));
            float finalRotation = Rotation + FlipRotation;

            spriteBatch.DrawSnapped(spriteManager.ScoundrelCardsSilhouetteSpriteSheet, drawPos, sourceRect, Color.White * FlashWhiteIntensity, finalRotation, origin, Scale, SpriteEffects.None, 0f);
        }

        public Rectangle GetBounds()
        {
            int width = (int)MathF.Round(36 * Scale.X);
            int height = (int)MathF.Round(50 * Scale.Y);
            int x = (int)MathF.Round(Position.X) - width / 2;
            int y = (int)MathF.Round(Position.Y) - height / 2;

            if (ExpandHitboxX)
            {
                x -= 1;
                width += 2;
            }

            return new Rectangle(x, y, width, height);
        }
    }
}