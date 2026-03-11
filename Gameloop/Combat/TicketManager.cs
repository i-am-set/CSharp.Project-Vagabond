using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle
{
    public class MatchTicket
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
        public bool IsBlank;

        public float RotX;
        public float RotY;
        public float RotZ;
        public float VelRotX;
        public float VelRotY;
        public float VelRotZ;

        public float FlutterPhase;
        public float FlutterSpeed;
    }

    public class TicketManager
    {
        private readonly List<MatchTicket> _tickets = new List<MatchTicket>();
        private readonly Queue<MatchTicket> _pendingTickets = new Queue<MatchTicket>();
        private static readonly Random _random = new Random();

        public IReadOnlyList<MatchTicket> Tickets => _tickets;
        public bool IsHoveringTicket { get; private set; }
        public bool IsDraggingTicket => _draggedTicket != null;

        private MatchTicket _draggedTicket;

        public void Clear()
        {
            _tickets.Clear();
            _pendingTickets.Clear();
            _draggedTicket = null;
            IsHoveringTicket = false;
        }

        public void PrintTicket(int wizardNumber, int placement)
        {
            _pendingTickets.Enqueue(new MatchTicket
            {
                Placement = placement,
                WizardNumber = wizardNumber,
                AnimTimer = 0f,
                TargetX = 44,
                Position = new Vector2(44, -16f),
                Scale = 1.0f
            });
        }

        public void DebugPrintTicket()
        {
            _pendingTickets.Enqueue(new MatchTicket
            {
                Placement = 0,
                WizardNumber = 0,
                AnimTimer = 0f,
                TargetX = 44,
                Position = new Vector2(44, -16f),
                Scale = 1.0f,
                IsBlank = true
            });
        }

        private float WrapAngle(float angle)
        {
            angle %= MathHelper.TwoPi;
            if (angle <= -MathHelper.Pi) angle += MathHelper.TwoPi;
            else if (angle > MathHelper.Pi) angle -= MathHelper.TwoPi;
            return angle;
        }

        private void UpdateTicketDispense(MatchTicket ticket, float dt)
        {
            ticket.AnimTimer += dt;
            float startY = -16f;
            float dispenseY = 14.5f;

            float t1 = 0.25f;
            float t2 = t1 + 0.5f;
            float t3 = t2 + 0.25f;
            float t4 = t3 + 0.5f;
            float t5 = t4 + 1.0f;

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

        public void Update(float dt, Vector2 virtualMousePos, bool justClicked, bool isClicking, InputManager inputManager)
        {
            IsHoveringTicket = false;

            bool isPrinting = _tickets.Any(t => !t.IsDispensed);
            if (!isPrinting && _pendingTickets.Count > 0)
            {
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

            if (justClicked && _draggedTicket == null && inputManager.IsMouseClickAvailable())
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
                        _draggedTicket = t;
                        inputManager.ConsumeMouseClick();

                        _tickets.RemoveAt(i);
                        _tickets.Add(t);
                        break;
                    }
                }
            }

            if (_draggedTicket != null)
            {
                if (isClicking)
                {
                    Vector2 prevPos = _draggedTicket.Position;
                    _draggedTicket.Position = virtualMousePos + _draggedTicket.DragOffset;

                    if (dt > 0)
                    {
                        _draggedTicket.Velocity = (_draggedTicket.Position - prevPos) / dt;
                    }

                    _draggedTicket.RotX = WrapAngle(_draggedTicket.RotX);
                    _draggedTicket.RotY = WrapAngle(_draggedTicket.RotY);
                    _draggedTicket.RotZ = WrapAngle(_draggedTicket.RotZ);

                    _draggedTicket.RotX = MathHelper.Lerp(_draggedTicket.RotX, Math.Clamp(_draggedTicket.Velocity.Y * 0.002f, -0.3f, 0.3f), 15f * dt);
                    _draggedTicket.RotY = MathHelper.Lerp(_draggedTicket.RotY, Math.Clamp(_draggedTicket.Velocity.X * 0.002f, -0.3f, 0.3f), 15f * dt);
                    _draggedTicket.RotZ = MathHelper.Lerp(_draggedTicket.RotZ, Math.Clamp(_draggedTicket.Velocity.X * 0.001f, -0.2f, 0.2f), 15f * dt);

                    _draggedTicket.VelRotX = 0f;
                    _draggedTicket.VelRotY = 0f;
                    _draggedTicket.VelRotZ = 0f;
                }
                else
                {
                    _draggedTicket.IsDragging = false;

                    _draggedTicket.Velocity.X = Math.Clamp(_draggedTicket.Velocity.X, -600f, 600f);
                    _draggedTicket.Velocity.Y = Math.Clamp(_draggedTicket.Velocity.Y, -600f, 600f);

                    _draggedTicket.VelRotX = Math.Clamp(_draggedTicket.Velocity.Y * 0.015f, -8f, 8f) + (float)(_random.NextDouble() * 4.0 - 2.0);
                    _draggedTicket.VelRotY = Math.Clamp(_draggedTicket.Velocity.X * 0.015f, -8f, 8f) + (float)(_random.NextDouble() * 4.0 - 2.0);
                    _draggedTicket.VelRotZ = Math.Clamp(_draggedTicket.Velocity.X * 0.005f, -3f, 3f) + (float)(_random.NextDouble() * 2.0 - 1.0);

                    _draggedTicket.FlutterPhase = (float)(_random.NextDouble() * MathHelper.TwoPi);
                    _draggedTicket.FlutterSpeed = (float)(_random.NextDouble() * 2.0 + 1.5);
                    _draggedTicket = null;
                }
            }

            for (int i = _tickets.Count - 1; i >= 0; i--)
            {
                var t = _tickets[i];
                t.Scale = 1.0f;

                if (!t.IsDispensed)
                {
                    UpdateTicketDispense(t, dt);
                }
                else
                {
                    if (!t.IsDragging && !t.IsHanging)
                    {
                        float ny = MathF.Sin(t.RotX) * MathF.Cos(t.RotZ) - MathF.Cos(t.RotX) * MathF.Sin(t.RotY) * MathF.Sin(t.RotZ);
                        float flatProfile = Math.Abs(ny);

                        float dragY = MathHelper.Lerp(0.5f, 8.0f, flatProfile);
                        float terminalVelocityY = MathHelper.Lerp(300f, 35f, flatProfile);

                        t.Velocity.X *= MathF.Max(0f, 1f - 2.0f * dt);
                        t.Velocity.Y *= MathF.Max(0f, 1f - dragY * dt);

                        t.Velocity.Y += 500f * dt;

                        if (t.Velocity.Y > terminalVelocityY) t.Velocity.Y = terminalVelocityY;

                        t.FlutterPhase += t.FlutterSpeed * dt;
                        float flutterMagnitude = MathHelper.Lerp(30f, 200f, flatProfile);
                        t.Velocity.X += MathF.Sin(t.FlutterPhase) * flutterMagnitude * dt;

                        t.VelRotX *= MathF.Max(0f, 1f - 1.5f * dt);
                        t.VelRotY *= MathF.Max(0f, 1f - 1.5f * dt);
                        t.VelRotZ *= MathF.Max(0f, 1f - 1.5f * dt);

                        t.VelRotX += MathF.Sin(t.FlutterPhase * 1.3f) * 6f * dt;
                        t.VelRotY += MathF.Cos(t.FlutterPhase * 1.1f) * 6f * dt;
                        t.VelRotZ += MathF.Sin(t.FlutterPhase * 0.8f) * 2.5f * dt;

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

                    if (!IsHoveringTicket && _draggedTicket == null)
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
                            IsHoveringTicket = true;
                        }
                    }
                }
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

        public void Draw(SpriteBatch spriteBatch, SpriteManager spriteManager, Global global, BitmapFont mainFont, BitmapFont tertFont)
        {
            var ticketSheet = spriteManager.BetTicketSpriteSheet;
            var pixel = ServiceLocator.Get<Texture2D>();

            foreach (var ticket in _tickets)
            {
                Vector2 origin = new Vector2(10f, 16f);

                float cosX = MathF.Cos(ticket.RotX);
                float cosY = MathF.Cos(ticket.RotY);

                bool isBackside = (cosX * cosY) < 0;

                Rectangle sourceRect = isBackside
                    ? new Rectangle(1 * 19, 0, 19, 31)
                    : new Rectangle(0 * 19, 0, 19, 31);

                float maxCos = Math.Max(Math.Abs(cosX), Math.Abs(cosY));
                float targetMaxScale = MathHelper.Lerp(1.2f, 1.0f, maxCos);
                float scaleBoost = targetMaxScale / Math.Max(0.01f, maxCos);

                float absScaleX = Math.Max(0.15f, Math.Abs(cosY) * scaleBoost) * ticket.Scale;
                float absScaleY = Math.Max(0.15f, Math.Abs(cosX) * scaleBoost) * ticket.Scale;
                Vector2 finalScale = new Vector2(absScaleX, absScaleY);

                SpriteEffects effects = SpriteEffects.None;
                if (cosY < 0) effects |= SpriteEffects.FlipHorizontally;
                if (cosX < 0) effects |= SpriteEffects.FlipVertically;

                float normalZ = Math.Abs(cosX * cosY);
                float brightness = 0.8f + 0.2f * normalZ;

                Color ticketColor = new Color((int)(255 * brightness), (int)(255 * brightness), (int)(255 * brightness), 255);
                Color textColor = new Color((int)(global.Palette_Black.R * brightness), (int)(global.Palette_Black.G * brightness), (int)(global.Palette_DarkestPale.B * brightness), 255);

                if (ticketSheet != null)
                {
                    spriteBatch.DrawSnapped(ticketSheet, ticket.Position, sourceRect, ticketColor, ticket.RotZ, origin, finalScale, effects, 0f);
                }
                else
                {
                    Color fallbackColor = new Color((int)(global.Palette_Pale.R * brightness), (int)(global.Palette_Pale.G * brightness), (int)(global.Palette_Pale.B * brightness), 255);
                    spriteBatch.DrawSnapped(pixel, ticket.Position, new Rectangle(0, 0, 19, 31), fallbackColor, ticket.RotZ, origin, finalScale, effects, 0f);
                }

                if (!isBackside && !ticket.IsBlank)
                {
                    string numText = ticket.Placement.ToString();
                    string sufText = GetOrdinalSuffix(ticket.Placement);

                    Vector2 numSize = mainFont.MeasureString(numText);
                    Vector2 sufSize = tertFont.MeasureString(sufText);

                    float totalWidth = numSize.X + sufSize.X;
                    Vector2 pivot = new Vector2(MathF.Round(totalWidth / 2f) + 1f, MathF.Round(numSize.Y / 2f));

                    Vector2 numOrigin = new Vector2(MathF.Round(pivot.X), MathF.Round(pivot.Y));
                    Vector2 sufOrigin = new Vector2(MathF.Round(pivot.X - numSize.X), MathF.Round(pivot.Y));

                    spriteBatch.DrawStringSnapped(mainFont, numText, ticket.Position, textColor, ticket.RotZ, numOrigin, finalScale, SpriteEffects.None, 0f);
                    spriteBatch.DrawStringSnapped(tertFont, sufText, ticket.Position, textColor, ticket.RotZ, sufOrigin, finalScale, SpriteEffects.None, 0f);
                }

                if (ticketSheet != null && !isBackside && !ticket.IsBlank && ticket.Placement >= 1 && ticket.Placement <= 3)
                {
                    int overlayFrameIndex = ticket.Placement + 1;
                    Rectangle overlayRect = new Rectangle(overlayFrameIndex * 19, 0, 19, 31);
                    spriteBatch.DrawSnapped(ticketSheet, ticket.Position, overlayRect, ticketColor, ticket.RotZ, origin, finalScale, effects, 0f);
                }
            }
        }
    }
}