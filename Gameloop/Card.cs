using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Scenes
{
    public enum CardSuit { Hearts, Diamonds, Spades, Clubs, None }
    public enum CardType { Potion, Weapon, Monster, Blank, Outline, BackRed, BackBlue }

    public class Card
    {
        public CardSuit Suit { get; set; }
        public CardType Type { get; set; }
        public int Rank { get; set; }
        public int Value { get; set; }

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

        private bool _isFlipping;
        private const float LERP_SPEED = 10f;

        public Card(CardSuit suit, CardType type, int rank, int value)
        {
            Suit = suit;
            Type = type;
            Rank = rank;
            Value = value;
            Scale = Vector2.One;
            TargetScale = Vector2.One;
            IsFaceUp = false;
        }

        public void Flip()
        {
            _isFlipping = true;
            TargetScale = new Vector2(0f, TargetScale.Y);
        }

        public void Update(float dt)
        {
            Position = Vector2.Lerp(Position, TargetPosition, LERP_SPEED * dt);
            Scale = Vector2.Lerp(Scale, TargetScale, LERP_SPEED * dt);
            Rotation = MathHelper.Lerp(Rotation, TargetRotation, LERP_SPEED * dt);

            if (_isFlipping && Scale.X < 0.05f)
            {
                IsFaceUp = !IsFaceUp;
                _isFlipping = false;
                TargetScale = new Vector2(1f, TargetScale.Y);
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
            Vector2 drawPos = new Vector2(MathF.Round(Position.X), MathF.Round(Position.Y));
            if (IsHovered) drawPos.Y -= 1f;

            spriteBatch.DrawSnapped(spriteManager.ScoundrelCardsSpriteSheet, drawPos, sourceRect, Color.White, Rotation, origin, Scale, SpriteEffects.None, 0f);
        }

        public Rectangle GetBounds()
        {
            int width = (int)(36 * Scale.X);
            int height = (int)(50 * Scale.Y);
            int yOffset = IsHovered ? -1 : 0;
            return new Rectangle((int)Position.X - width / 2, (int)Position.Y + yOffset - height / 2, width, height);
        }
    }
}