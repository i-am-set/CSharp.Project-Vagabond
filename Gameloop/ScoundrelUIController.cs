using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class ScoundrelUIController
    {
        public Button TryAgainButton { get; private set; }
        public Button ExitButton { get; private set; }
        public List<Button> PauseButtons { get; } = new List<Button>();
        public NavigationGroup PauseNavGroup { get; } = new NavigationGroup(wrapNavigation: true);
        public List<Button> ShopButtons { get; } = new List<Button>();
        public NavigationGroup ShopNavGroup { get; } = new NavigationGroup(wrapNavigation: true);
        public ConfirmationDialog ConfirmationDialog { get; private set; }

        public PlinkAnimator DeckCountPlink { get; } = new PlinkAnimator { MaxScale = 1.5f, RestScale = 1.0f };
        public PlinkAnimator DiscardCountPlink { get; } = new PlinkAnimator { MaxScale = 1.5f, RestScale = 1.0f };
        public PlinkAnimator HealthPlink { get; } = new PlinkAnimator { MaxScale = 1.5f, RestScale = 1.0f };
        public PlinkAnimator GoldPlink { get; } = new PlinkAnimator { MaxScale = 1.5f, RestScale = 1.0f };

        public float HpTextFlashTimer { get; set; }
        public Color HpTextFlashColor { get; set; } = Color.White;
        public List<FloatingText> FloatingTexts { get; } = new List<FloatingText>();

        public float[] HeartFlashTimers { get; } = new float[20];
        public int[] HeartFlashFrames { get; } = new int[20];
        public const float HEART_FLASH_DURATION = 0.75f;
        public const float HEART_FLASH_BLINK_INTERVAL = 0.15f;
        public const float HEART_FLASH_BLINK_HALF = 0.075f;

        public float FloorClearedTextTimer { get; set; }
        public float ShopFadeTimer { get; set; }
        public float GoldFlashTimer { get; set; }

        public float HealthBarOpacity { get; set; } = 1f;
        public Vector2 TimerPosition { get; set; } = new Vector2(Global.VIRTUAL_WIDTH / 2f, 12f);
        public string TimerOverrideText { get; set; } = null;
        public Color TimerColor { get; set; } = Color.White;
        public float TimerOpacity { get; set; } = 1f;
        public float TimerScale { get; set; } = 1f;

        private Global _global;
        private Core _core;
        private Texture2D _pixel;
        private ScoundrelScene _scene;
        private int _previousGold = -1;

        public ScoundrelUIController(ScoundrelScene scene)
        {
            _scene = scene;
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
            _pixel = ServiceLocator.Get<Texture2D>();
            ConfirmationDialog = new ConfirmationDialog(scene);
        }

        private Button CreatePauseBtn(string text, int y, BitmapFont font)
        {
            int w = (int)font.MeasureString(text).Width + 16;
            int x = Global.VIRTUAL_WIDTH / 2 - w / 2;
            return new Button(new Rectangle(x, y, w, 16), text, font: font) { DrawBorderOnHover = true };
        }

        public void Initialize()
        {
            var secFont = _core.SecondaryFont;
            var defFont = _core.DefaultFont;

            Vector2 tryAgainSize = secFont.MeasureString("TRY AGAIN");
            int taWidth = (int)tryAgainSize.X + 8;
            int taHeight = (int)tryAgainSize.Y + 7;
            TryAgainButton = new Button(new Rectangle(Global.VIRTUAL_WIDTH / 2 - taWidth / 2, 110, taWidth, taHeight), "TRY AGAIN", font: secFont) { DrawBorderOnHover = true, TextRenderOffset = new Vector2(0, 0.5f) };

            Vector2 exitSize = secFont.MeasureString("MAIN MENU");
            int exWidth = (int)exitSize.X + 8;
            int exHeight = (int)exitSize.Y + 7;
            ExitButton = new Button(new Rectangle(Global.VIRTUAL_WIDTH / 2 - exWidth / 2, 125, exWidth, exHeight), "MAIN MENU", font: secFont) { DrawBorderOnHover = true, TextRenderOffset = new Vector2(0, 0.5f) };

            int startY = Global.VIRTUAL_HEIGHT / 2 - 30;
            int spacing = 18;

            var resumeBtn = CreatePauseBtn("RESUME", startY, defFont);
            PauseButtons.Add(resumeBtn);
            PauseNavGroup.Add(resumeBtn);

            var settingsBtn = CreatePauseBtn("SETTINGS", startY + spacing, defFont);
            PauseButtons.Add(settingsBtn);
            PauseNavGroup.Add(settingsBtn);

            var menuBtn = CreatePauseBtn("EXIT TO MAIN MENU", startY + spacing * 2, defFont);
            PauseButtons.Add(menuBtn);
            PauseNavGroup.Add(menuBtn);

            var desktopBtn = CreatePauseBtn("EXIT TO DESKTOP", startY + spacing * 3, defFont);
            PauseButtons.Add(desktopBtn);
            PauseNavGroup.Add(desktopBtn);

            TryAgainButton.OnClick += () => _scene.ResetBoard();
            ExitButton.OnClick += () => _scene.ExitToMainMenu();

            PauseButtons[0].OnClick += () => _scene.TogglePause();
            PauseButtons[1].OnClick += () => ServiceLocator.Get<SceneManager>().ShowModal(GameSceneState.Settings);
            PauseButtons[2].OnClick += () => ConfirmationDialog.Show("Return to Main Menu?\n\n\n[cred]Current run will be lost.[/]", new List<Tuple<string, Action>>
            {
                Tuple.Create("YES", new Action(() => _scene.ExitToMainMenu())),
                Tuple.Create("[chighlight]NO", new Action(() => ConfirmationDialog.Hide()))
            });
            PauseButtons[3].OnClick += () => ConfirmationDialog.Show("Exit to Desktop?\n\n\n[cred]Current run will be lost.[/]", new List<Tuple<string, Action>>
            {
                Tuple.Create("YES", new Action(() => _core.ExitApplication())),
                Tuple.Create("[chighlight]NO", new Action(() => ConfirmationDialog.Hide()))
            });
        }

        public void Reset()
        {
            FloatingTexts.Clear();
            Array.Clear(HeartFlashTimers, 0, 20);
            Array.Clear(HeartFlashFrames, 0, 20);
            HpTextFlashTimer = 0f;
            FloorClearedTextTimer = 0f;
            ShopFadeTimer = 0f;
            GoldFlashTimer = 0f;
            _previousGold = -1;
        }

        public void Update(float dt, GameTime gameTime, Vector2 deckPos, Vector2 discardPos)
        {
            if (HpTextFlashTimer > 0) HpTextFlashTimer -= dt;
            if (GoldFlashTimer > 0) GoldFlashTimer -= dt;
            FloorClearedTextTimer += dt;
            ShopFadeTimer += dt;

            for (int i = 0; i < 20; i++)
            {
                if (HeartFlashTimers[i] > 0) HeartFlashTimers[i] -= dt;
            }

            for (int i = FloatingTexts.Count - 1; i >= 0; i--)
            {
                FloatingTexts[i].Timer -= dt;
                FloatingTexts[i].LocalOffset.Y -= 5f * dt;
                if (FloatingTexts[i].Timer <= 0) FloatingTexts.RemoveAt(i);
            }

            Vector2 hpCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, 24f);
            DeckCountPlink.Update(gameTime, deckPos + new Vector2(0, 32));
            DiscardCountPlink.Update(gameTime, discardPos + new Vector2(0, -32));
            HealthPlink.Update(gameTime, hpCenter);
        }

        public void AddFloatingText(int amount, bool isHealing, bool isGold = false, Vector2? position = null)
        {
            Vector2 startPos = position ?? new Vector2(Global.VIRTUAL_WIDTH / 2f, 24f);
            var ft = Pool<FloatingText>.Get();
            ft.Number = amount;
            ft.IsHealing = isHealing;
            ft.IsGold = isGold;
            ft.Timer = 1.0f;
            ft.LocalOffset = startPos;
            ft.Plink.Start(0f, 0.3f);
            FloatingTexts.Add(ft);
        }

        public void GenerateShop(ScoundrelScene scene, RunContext runContext, ScoundrelCombatController combat)
        {
            ShopButtons.Clear();
            ShopNavGroup.Clear();

            var defFont = _core.DefaultFont;
            int btnWidth = 160;
            int btnHeight = 20;
            int startY = Global.VIRTUAL_HEIGHT / 2 - 20;
            int spacing = 24;
            int centerX = Global.VIRTUAL_WIDTH / 2 - btnWidth / 2;

            var healBtn = new Button(new Rectangle(centerX, startY, btnWidth, btnHeight), "HEAL 5 HP (3g)", font: defFont);
            healBtn.OnClick += () =>
            {
                if (runContext.Gold >= 3 && combat.Health < runContext.MaxHealth)
                {
                    runContext.Gold -= 3;
                    combat.Health = Math.Min(runContext.MaxHealth, combat.Health + 5);
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_confirm");
                }
                else
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_alert");
                }
            };
            ShopButtons.Add(healBtn);
            ShopNavGroup.Add(healBtn);

            var leaveBtn = new Button(new Rectangle(centerX, startY + spacing, btnWidth, btnHeight), "LEAVE SHOP", font: defFont);
            leaveBtn.OnClick += () => scene.ApplyRewardAndAdvance(null);
            ShopButtons.Add(leaveBtn);
            ShopNavGroup.Add(leaveBtn);

            float delay = 0f;
            foreach (var btn in ShopButtons)
            {
                btn.PlayEntrance(delay);
                delay += 0.1f;
            }

            if (ServiceLocator.Get<InputManager>().CurrentInputDevice != InputDeviceType.Mouse)
            {
                ShopNavGroup.SelectFirst();
            }
            else
            {
                ShopNavGroup.DeselectAll();
            }
        }

        public void DrawCounters(SpriteBatch spriteBatch, int deckCount, int discardCount, Vector2 deckPos, Vector2 discardPos)
        {
            var secFont = _core.SecondaryFont;
            if (deckCount > 0 && DeckCountPlink.Scale > 0.01f)
            {
                string deckText = deckCount.ToString();
                Vector2 deckSize = secFont.MeasureString(deckText);
                Vector2 origin = new Vector2(MathF.Round(deckSize.X / 2f), MathF.Round(deckSize.Y / 2f));
                Vector2 pos = deckPos + new Vector2(0, 32);
                spriteBatch.DrawStringOutlinedSnapped(secFont, deckText, pos, _global.Palette_DarkestPale, _global.Palette_Off, DeckCountPlink.Rotation, origin, DeckCountPlink.Scale, SpriteEffects.None, 0f);
            }

            if (discardCount > 0 && DiscardCountPlink.Scale > 0.01f)
            {
                string discardText = discardCount.ToString();
                Vector2 discardSize = secFont.MeasureString(discardText);
                Vector2 origin = new Vector2(MathF.Round(discardSize.X / 2f), MathF.Round(discardSize.Y / 2f));
                Vector2 pos = discardPos + new Vector2(0, -32);
                spriteBatch.DrawStringOutlinedSnapped(secFont, discardText, pos, _global.Palette_DarkestPale, _global.Palette_Off, DiscardCountPlink.Rotation, origin, DiscardCountPlink.Scale, SpriteEffects.None, 0f);
            }
        }

        public void DrawHoverIndicators(SpriteBatch spriteBatch, Card? hoveredCard, ScoundrelState state, ScoundrelBoardController board, ScoundrelCombatController combat, int maxHealth, float previewFlashTimer)
        {
            if (hoveredCard == null || !hoveredCard.IsFaceUp) return;

            var defFont = _core.DefaultFont;
            var tertFont = _core.TertiaryFont;

            if (state == ScoundrelState.Focused)
            {
                if (hoveredCard == board.WeaponSlot)
                {
                    int wDmg = Math.Max(0, board.FocusedCard!.Value - board.WeaponSlot.Value);
                    string wText = $"-{wDmg}";
                    Color wColor = wDmg == 0 ? _global.Palette_DarkSun : _global.Palette_Rust;
                    DrawHoverText(spriteBatch, defFont, wText, hoveredCard.Position + new Vector2(0, -32), wColor);
                }
                else if (hoveredCard == board.FistCard)
                {
                    string fText = $"-{board.FocusedCard!.Value}";
                    DrawHoverText(spriteBatch, defFont, fText, hoveredCard.Position + new Vector2(0, -32), _global.Palette_Rust);
                }
            }
            else
            {
                if (hoveredCard.Type == CardType.Monster)
                {
                    bool canUseWeapon = board.WeaponSlot != null && hoveredCard.Value <= combat.LastSlainValue;
                    DrawMonsterDamageText(spriteBatch, defFont, tertFont, hoveredCard, canUseWeapon, board.WeaponSlot, previewFlashTimer);
                }
                else if (hoveredCard.Type == CardType.Potion)
                {
                    int baseHeal = combat.PotionsUsedThisRoom == 0 ? hoveredCard.Value : 0;
                    int actualHeal = Math.Min(baseHeal, maxHealth - combat.Health);
                    string healText = $"+{actualHeal}";
                    Color hColor = actualHeal == 0 ? _global.Palette_DarkSun : _global.Palette_Leaf;
                    DrawHoverText(spriteBatch, defFont, healText, hoveredCard.Position + new Vector2(0, -32), hColor);
                }
                else if (hoveredCard.Type == CardType.Weapon)
                {
                    DrawHoverText(spriteBatch, defFont, "EQUIP", hoveredCard.Position + new Vector2(0, -32), _global.Palette_DarkSun);
                }
            }
        }

        public void DrawFloatingTexts(SpriteBatch spriteBatch, GameTime gameTime)
        {
            var secFont = _core.SecondaryFont;
            foreach (var ft in FloatingTexts)
            {
                ft.Plink.Update(gameTime, ft.LocalOffset);

                float alpha = Math.Clamp(ft.Timer / 0.3f, 0f, 1f);

                Color c = ft.IsGold ? _global.Palette_Sun : (ft.IsHealing ? _global.Palette_Leaf : _global.Palette_Rust);
                c *= alpha;
                Color outline = _global.Palette_Off * alpha;

                string text = ft.IsGold ? $"+{ft.Number}g" : ((ft.IsHealing ? "+" : "-") + ft.Number);
                Vector2 size = secFont.MeasureString(text);
                Vector2 origin = new Vector2(MathF.Round(size.X / 2f), MathF.Round(size.Y / 2f));
                Vector2 drawPos = new Vector2(MathF.Round(ft.LocalOffset.X), MathF.Round(ft.LocalOffset.Y));

                spriteBatch.DrawStringOutlinedSnapped(secFont, text, drawPos, c, outline, ft.Plink.Rotation, origin, ft.Plink.Scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawHoverText(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 pos, Color color)
        {
            Vector2 size = font.MeasureString(text);
            Vector2 startX = new Vector2(MathF.Round(pos.X - size.X / 2f), MathF.Round(pos.Y - size.Y / 2f));
            spriteBatch.DrawStringOutlinedSnapped(font, text, startX, color, _global.Palette_Off);
        }

        private void DrawMonsterDamageText(SpriteBatch spriteBatch, BitmapFont defFont, BitmapFont tertFont, Card monsterCard, bool showWeaponDamage, Card? weaponSlot, float previewFlashTimer)
        {
            Vector2 hoverPos = monsterCard.Position + new Vector2(0, -32);
            string dmgText = $"-{monsterCard.Value}";
            Vector2 dmgSize = defFont.MeasureString(dmgText);

            bool showWeapon = showWeaponDamage && weaponSlot != null && (previewFlashTimer % 1.0f) < 0.5f;

            if (showWeapon)
            {
                int wDmg = Math.Max(0, monsterCard.Value - weaponSlot!.Value);
                string wText = $"(-{wDmg})";
                Vector2 wSize = tertFont.MeasureString(wText);

                float totalW = dmgSize.X + 2 + wSize.X;
                Vector2 startX = new Vector2(MathF.Round(hoverPos.X - totalW / 2f), MathF.Round(hoverPos.Y - dmgSize.Y / 2f));

                spriteBatch.DrawStringOutlinedSnapped(defFont, dmgText, startX, _global.Palette_Rust, _global.Palette_Off);

                Color wColor = wDmg == 0 ? _global.Palette_DarkSun : _global.Palette_Rust;
                Vector2 wPos = new Vector2(startX.X + dmgSize.X + 2, startX.Y + (dmgSize.Y - wSize.Y) / 2f);
                spriteBatch.DrawStringOutlinedSnapped(tertFont, wText, wPos, wColor, _global.Palette_Off);
            }
            else
            {
                Vector2 startX = new Vector2(MathF.Round(hoverPos.X - dmgSize.X / 2f), MathF.Round(hoverPos.Y - dmgSize.Y / 2f));
                spriteBatch.DrawStringOutlinedSnapped(defFont, dmgText, startX, _global.Palette_Rust, _global.Palette_Off);
            }
        }

        public void DrawTimer(SpriteBatch spriteBatch, float floorTimer)
        {
            if (TimerOpacity <= 0.01f) return;

            var secFont = _core.SecondaryFont;
            string timeStr = TimerOverrideText;
            if (timeStr == null)
            {
                TimeSpan time = TimeSpan.FromSeconds(floorTimer);
                timeStr = time.ToString(@"mm\:ss");
            }
            Vector2 size = secFont.MeasureString(timeStr);
            Vector2 origin = new Vector2(MathF.Round(size.X / 2f), MathF.Round(size.Y / 2f));
            Vector2 drawPos = new Vector2(MathF.Round(TimerPosition.X), MathF.Round(TimerPosition.Y));
            spriteBatch.DrawStringOutlinedSnapped(secFont, timeStr, drawPos, TimerColor * TimerOpacity, _global.Palette_Off * TimerOpacity, 0f, origin, TimerScale, SpriteEffects.None, 0f);
        }

        public void DrawHealthBar(SpriteBatch spriteBatch, int health, int previewHealth, int maxHealth, SpriteManager spriteManager)
        {
            if (HealthBarOpacity <= 0.01f) return;

            var heartSheet = spriteManager.HealthHearts7x6SpriteSheet;
            float hpScale = HealthPlink.IsActive ? HealthPlink.Scale : 1f;

            if (heartSheet != null && hpScale > 0.01f)
            {
                int maxHearts = maxHealth / 2;
                int heartWidth = 7;
                int heartHeight = 6;
                int spacing = 1;
                int totalWidth = maxHearts * heartWidth + (maxHearts - 1) * spacing;

                Vector2 barCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, 24f);

                Color offColor = _global.Palette_Off * HealthBarOpacity;
                Color whiteColor = Color.White * HealthBarOpacity;

                for (int i = 0; i < maxHearts; i++)
                {
                    int currentHeartVal = Math.Clamp(health - i * 2, 0, 2);
                    int previewHeartVal = Math.Clamp(previewHealth - i * 2, 0, 2);

                    int frameIndex = 2;
                    if (currentHeartVal == 2) frameIndex = 0;
                    else if (currentHeartVal == 1) frameIndex = 1;

                    if (HeartFlashTimers[i] > 0)
                    {
                        bool isFlashFrame = (HeartFlashTimers[i] % HEART_FLASH_BLINK_INTERVAL) > HEART_FLASH_BLINK_HALF;
                        if (isFlashFrame) frameIndex = HeartFlashFrames[i];
                    }
                    else if (currentHeartVal != previewHeartVal)
                    {
                        if ((currentHeartVal == 2 && previewHeartVal == 0) || (currentHeartVal == 0 && previewHeartVal == 2)) frameIndex = 3;
                        else if ((currentHeartVal == 2 && previewHeartVal == 1) || (currentHeartVal == 1 && previewHeartVal == 2)) frameIndex = 4;
                        else if ((currentHeartVal == 1 && previewHeartVal == 0) || (currentHeartVal == 0 && previewHeartVal == 1)) frameIndex = 5;
                        else frameIndex = 3;
                    }

                    Rectangle sourceRect = new Rectangle(frameIndex * heartWidth, 0, heartWidth, heartHeight);

                    Vector2 offset = new Vector2(i * (heartWidth + spacing) + (heartWidth / 2f), heartHeight / 2f) - new Vector2(totalWidth / 2f, heartHeight / 2f);
                    Vector2 finalPos = barCenter + offset * hpScale;
                    Vector2 origin = new Vector2(heartWidth / 2f, heartHeight / 2f);

                    spriteBatch.DrawSnapped(heartSheet, finalPos + new Vector2(-1, 0), sourceRect, offColor, 0f, origin, hpScale, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(heartSheet, finalPos + new Vector2(1, 0), sourceRect, offColor, 0f, origin, hpScale, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(heartSheet, finalPos + new Vector2(0, -1), sourceRect, offColor, 0f, origin, hpScale, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(heartSheet, finalPos + new Vector2(0, 1), sourceRect, offColor, 0f, origin, hpScale, SpriteEffects.None, 0f);

                    spriteBatch.DrawSnapped(heartSheet, finalPos, sourceRect, whiteColor, 0f, origin, hpScale, SpriteEffects.None, 0f);
                }

                string hpLabel = "HP ";
                string currentHpText = health.ToString();
                string maxHpText = $"/{maxHealth}";

                Color valColor;
                if (health >= maxHealth * 0.7f) valColor = _global.Palette_Leaf;
                else if (health >= maxHealth * 0.35f) valColor = _global.Palette_Fruit;
                else valColor = _global.Palette_Rust;

                Color currentHpTextColor = valColor;

                if (previewHealth != health)
                {
                    currentHpText = previewHealth.ToString();
                    currentHpTextColor = _global.Palette_Sun;
                }
                else if (HpTextFlashTimer > 0)
                {
                    float flashLerp = HpTextFlashTimer / 0.3f;
                    currentHpTextColor = Color.Lerp(currentHpTextColor, HpTextFlashColor, flashLerp);
                }

                var tertFont = _core.TertiaryFont;
                var defFont = _core.DefaultFont;

                Vector2 hpLabelSize = tertFont.MeasureString(hpLabel);
                Vector2 currentHpSize = defFont.MeasureString(currentHpText);
                Vector2 maxHpSize = tertFont.MeasureString(maxHpText);

                float totalTextWidth = hpLabelSize.X + currentHpSize.X + maxHpSize.X;
                float textStartX = MathF.Round(barCenter.X - totalTextWidth / 2f);
                float textY = MathF.Round(barCenter.Y + heartHeight / 2f + 4f);

                float baselineY = MathF.Round(textY + currentHpSize.Y);

                Vector2 pos1 = new Vector2(MathF.Round(textStartX), MathF.Round(baselineY - hpLabelSize.Y));
                Vector2 pos2 = new Vector2(MathF.Round(textStartX + hpLabelSize.X), MathF.Round(textY) + 1);
                Vector2 pos3 = new Vector2(MathF.Round(textStartX + hpLabelSize.X + currentHpSize.X), MathF.Round(baselineY - maxHpSize.Y));

                Vector2 hpLabelOrigin = new Vector2(MathF.Round(hpLabelSize.X / 2f), MathF.Round(hpLabelSize.Y / 2f));
                Vector2 hpTextOrigin = new Vector2(MathF.Round(currentHpSize.X / 2f), MathF.Round(currentHpSize.Y / 2f));
                Vector2 maxHpOrigin = new Vector2(MathF.Round(maxHpSize.X / 2f), MathF.Round(maxHpSize.Y / 2f));

                float hpRot = HealthPlink.IsActive ? HealthPlink.Rotation : 0f;

                Color labelColor = _global.Palette_DarkestPale * HealthBarOpacity;
                Color valColorOp = valColor * HealthBarOpacity;
                Color currentHpTextColorOp = currentHpTextColor * HealthBarOpacity;
                Color outlineColor = _global.Palette_Off * HealthBarOpacity;

                spriteBatch.DrawStringOutlinedSnapped(tertFont, hpLabel, pos1 + hpLabelOrigin, labelColor, outlineColor, hpRot, hpLabelOrigin, hpScale, SpriteEffects.None, 0f);
                spriteBatch.DrawStringOutlinedSnapped(defFont, currentHpText, pos2 + hpTextOrigin, currentHpTextColorOp, outlineColor, hpRot, hpTextOrigin, hpScale, SpriteEffects.None, 0f);
                spriteBatch.DrawStringOutlinedSnapped(tertFont, maxHpText, pos3 + maxHpOrigin, valColorOp, outlineColor, hpRot, maxHpOrigin, hpScale, SpriteEffects.None, 0f);
            }
        }

        public void DrawGold(SpriteBatch spriteBatch, int gold, GameTime gameTime)
        {
            if (_previousGold == -1) _previousGold = gold;
            if (gold > _previousGold)
            {
                GoldPlink.Start(0f, 0.3f);
            }
            else if (gold < _previousGold)
            {
                GoldFlashTimer = 0.4f;
            }
            _previousGold = gold;

            var defFont = _core.DefaultFont;
            var tertFont = _core.TertiaryFont;

            string goldText = gold.ToString();
            string gText = "g";

            Vector2 goldSize = defFont.MeasureString(goldText);
            Vector2 gSize = tertFont.MeasureString(gText);

            float totalWidth = goldSize.X + gSize.X + 1;
            float startX = Global.VIRTUAL_WIDTH - totalWidth - 10;
            float startY = 10;

            Vector2 goldPos = new Vector2(MathF.Round(startX), MathF.Round(startY));
            Vector2 gPos = new Vector2(MathF.Round(startX + goldSize.X + 1), MathF.Round(startY + goldSize.Y - gSize.Y));

            GoldPlink.Update(gameTime, goldPos + new Vector2(totalWidth / 2f, goldSize.Y / 2f));

            Color goldColor = _global.Palette_Sun;
            if (GoldFlashTimer > 0)
            {
                goldColor = Color.Lerp(_global.Palette_Sun, _global.Palette_Rust, GoldFlashTimer / 0.4f);
            }

            Vector2 goldOrigin = new Vector2(MathF.Round(goldSize.X / 2f), MathF.Round(goldSize.Y / 2f));
            Vector2 gOrigin = new Vector2(MathF.Round(gSize.X / 2f), MathF.Round(gSize.Y / 2f));

            Vector2 goldDrawPos = goldPos + goldOrigin;
            Vector2 gDrawPos = gPos + gOrigin;

            spriteBatch.DrawStringOutlinedSnapped(defFont, goldText, goldDrawPos, goldColor, _global.Palette_Off, GoldPlink.Rotation, goldOrigin, GoldPlink.Scale, SpriteEffects.None, 0f);
            spriteBatch.DrawStringOutlinedSnapped(tertFont, gText, gDrawPos, goldColor, _global.Palette_Off, GoldPlink.Rotation, gOrigin, GoldPlink.Scale, SpriteEffects.None, 0f);
        }

        public void DrawRestartBar(SpriteBatch spriteBatch, float restartHoldTimer, float holdDuration)
        {
            if (restartHoldTimer <= 0f) return;

            var secFont = _core.SecondaryFont;
            float progress = Math.Clamp(restartHoldTimer / holdDuration, 0f, 1f);
            string restartText = "HOLD R TO RESTART";
            Vector2 rSize = secFont.MeasureString(restartText);
            Vector2 rPos = new Vector2(Global.VIRTUAL_WIDTH / 2f - rSize.X / 2f, Global.VIRTUAL_HEIGHT - 20);

            spriteBatch.DrawStringOutlinedSnapped(secFont, restartText, rPos, _global.Palette_LightPale, _global.Palette_Off);

            int barWidth = 100;
            int barHeight = 4;
            int barX = Global.VIRTUAL_WIDTH / 2 - barWidth / 2;
            int barY = (int)rPos.Y + (int)rSize.Y + 4;

            spriteBatch.Draw(_pixel, new Rectangle(barX - 1, barY - 1, barWidth + 2, barHeight + 2), _global.Palette_Off);
            spriteBatch.Draw(_pixel, new Rectangle(barX, barY, barWidth, barHeight), _global.Palette_DarkShadow);
            spriteBatch.Draw(_pixel, new Rectangle(barX, barY, (int)(barWidth * progress), barHeight), _global.Palette_Sun);
        }

        public void DrawFloorCleared(SpriteBatch spriteBatch)
        {
            var defFont = _core.DefaultFont;
            string text = "FLOOR CLEARED";
            Vector2 size = defFont.MeasureString(text);

            float startY = Global.VIRTUAL_HEIGHT / 2f;
            Vector2 pos = new Vector2(Global.VIRTUAL_WIDTH / 2f - size.X / 2f, startY - size.Y / 2f);

            float alpha = 1f;
            if (FloorClearedTextTimer > 1.0f)
            {
                alpha = 1.0f - Math.Clamp((FloorClearedTextTimer - 1.0f) / 0.5f, 0f, 1f);
            }

            if (alpha > 0f)
            {
                TextAnimator.DrawTextWithEffectOutlined(
                    spriteBatch,
                    defFont,
                    text,
                    pos,
                    _global.Palette_Sun * alpha,
                    _global.Palette_Off * alpha,
                    TextEffectType.TypewriterPop,
                    FloorClearedTextTimer
                );
            }
        }

        public void DrawShop(SpriteBatch spriteBatch, GameTime gameTime, Matrix transform)
        {
            var defFont = _core.DefaultFont;

            float alpha = Math.Clamp(ShopFadeTimer / 0.3f, 0f, 1f);
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black * (0.8f * alpha));

            string text = "SHOP";
            Vector2 size = defFont.MeasureString(text);

            TextAnimator.DrawTextWithEffectOutlined(
                spriteBatch,
                defFont,
                text,
                new Vector2(Global.VIRTUAL_WIDTH / 2f - size.X / 2f, 30),
                _global.Palette_Sun * alpha,
                _global.Palette_Off * alpha,
                TextEffectType.TypewriterPop,
                ShopFadeTimer
            );

            foreach (var btn in ShopButtons)
            {
                btn.Draw(spriteBatch, defFont, gameTime, transform);
            }
        }

        public void DrawGameOver(SpriteBatch spriteBatch, GameTime gameTime, Matrix transform, int health, int displayScore)
        {
            var defFont = _core.DefaultFont;
            var secFont = _core.SecondaryFont;

            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black * 0.8f);

            string result = health > 0 ? "VICTORY" : "DEFEAT";
            Color resColor = health > 0 ? _global.Palette_Sun : _global.Palette_Rust;

            Vector2 rSize = defFont.MeasureString(result);
            spriteBatch.DrawStringOutlinedSnapped(defFont, result, new Vector2(Global.VIRTUAL_WIDTH / 2f - rSize.X / 2f, 60), resColor, _global.Palette_Off);

            string scoreText = $"SCORE: {displayScore}";
            Vector2 sSize = secFont.MeasureString(scoreText);
            spriteBatch.DrawStringOutlinedSnapped(secFont, scoreText, new Vector2(Global.VIRTUAL_WIDTH / 2f - sSize.X / 2f, 90), _global.Palette_LightPale, _global.Palette_Off);

            TryAgainButton.Draw(spriteBatch, secFont, gameTime, transform);
            ExitButton.Draw(spriteBatch, secFont, gameTime, transform);
        }

        public void DrawPauseMenu(SpriteBatch spriteBatch, GameTime gameTime, Matrix transform)
        {
            var defFont = _core.DefaultFont;
            var secFont = _core.SecondaryFont;
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black * 0.7f);

            string pauseText = "PAUSED";
            Vector2 pSize = secFont.MeasureString(pauseText);
            spriteBatch.DrawStringOutlinedSnapped(secFont, pauseText, new Vector2(Global.VIRTUAL_WIDTH / 2f - pSize.X / 2f, 30), _global.Palette_Sun, _global.Palette_Off);

            foreach (var btn in PauseButtons)
            {
                btn.Draw(spriteBatch, defFont, gameTime, transform);
            }

            if (ConfirmationDialog.IsActive)
            {
                ConfirmationDialog.DrawContent(spriteBatch, defFont, gameTime, transform);
            }
        }
    }
}