using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class PayoutScene : GameScene
    {
        private class PayoutItem
        {
            public string Description;
            public int Amount;
            public PlinkAnimator ReasonPlink;
            public PlinkAnimator AmountPlink;
            public bool ReasonVisible;
            public bool AmountVisible;
        }

        private readonly Global _global;
        private readonly GameState _gameState;
        private readonly SceneManager _sceneManager;
        private readonly TransitionManager _transitionManager;
        private readonly InputManager _inputManager;
        private readonly HapticsManager _hapticsManager;

        private List<PayoutItem> _payouts = new List<PayoutItem>();
        private int _totalGold = 0;

        private Queue<(Action action, float delay)> _plinkQueue = new Queue<(Action, float)>();
        private float _plinkTimer = 0f;
        private bool _isFinishedPlinking = false;

        private Button _continueButton;
        private NavigationGroup _navigationGroup;
        private PlinkAnimator _totalPlink;

        public PayoutScene()
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

            var textureFactory = ServiceLocator.Get<TextureFactory>();
            Texture2D btnBg = textureFactory.CreateColoredTexture(100, 20, _global.Palette_Sun);

            _continueButton = new TextOverImageButton(
                new Rectangle(Global.VIRTUAL_WIDTH / 2 - 50, Global.VIRTUAL_HEIGHT - 35, 100, 20),
                "CONTINUE",
                btnBg,
                font: ServiceLocator.Get<Core>().DefaultFont,
                startVisible: false
            )
            {
                CustomDefaultTextColor = _global.Palette_Off,
                CustomHoverTextColor = _global.Palette_Off,
                TintBackgroundOnHover = true,
                HoverAnimation = HoverAnimationType.Hop,
                TriggerHapticOnHover = true
            };

            _continueButton.OnClick += OnContinueClicked;
            _navigationGroup.Add(_continueButton);
        }

        public override void Enter()
        {
            base.Enter();
            _payouts.Clear();
            _totalGold = 0;
            _totalPlink = new PlinkAnimator();
            _isFinishedPlinking = false;

            _continueButton.SetHiddenForEntrance();

            CalculatePayouts();
            QueueAnimations();

            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse)
            {
                _navigationGroup.SelectFirst();
            }
            else
            {
                _navigationGroup.DeselectAll();
            }
        }

        private void CalculatePayouts()
        {
            if (_gameState.LastMatchWizards == null) return;

            var playerWiz = _gameState.LastMatchWizards.FirstOrDefault(w => w.Data.Stats.IsPlayer);
            if (playerWiz == null) return;

            var allWiz = _gameState.LastMatchWizards;

            var dmgRank = allWiz.OrderByDescending(w => w.Data.Metrics.DamageDealt).ToList().IndexOf(playerWiz) + 1;
            var killsRank = allWiz.OrderByDescending(w => w.Data.Metrics.Kills).ToList().IndexOf(playerWiz) + 1;
            var blockRank = allWiz.OrderByDescending(w => w.Data.Metrics.DamageBlocked).ToList().IndexOf(playerWiz) + 1;

            int fee = _gameState.CurrentEntryFee;

            if (playerWiz.Data.Metrics.Placement == 1) AddPayout("1ST PLACE", (int)(fee * 1.2f));
            else if (playerWiz.Data.Metrics.Placement == 2) AddPayout("2ND PLACE", (int)(fee * 0.6f));
            else if (playerWiz.Data.Metrics.Placement == 3) AddPayout("3RD PLACE", (int)(fee * 0.2f));

            if (dmgRank == 1 && playerWiz.Data.Metrics.DamageDealt > 0) AddPayout("MOST DAMAGE", (int)(fee * 0.4f));
            else if (dmgRank == 2 && playerWiz.Data.Metrics.DamageDealt > 0) AddPayout("2ND MOST DAMAGE", (int)(fee * 0.2f));

            if (killsRank == 1 && playerWiz.Data.Metrics.Kills > 0) AddPayout("MOST KILLS", (int)(fee * 0.4f));

            if (blockRank == 1 && playerWiz.Data.Metrics.DamageBlocked > 0) AddPayout("MOST DAMAGE BLOCKED", (int)(fee * 0.3f));

            int survivalGold = (int)(playerWiz.Data.Metrics.TimeSurvived * (fee * 0.01f));
            if (survivalGold > 0) AddPayout($"SURVIVED {(int)playerWiz.Data.Metrics.TimeSurvived}s", survivalGold);

            if (_payouts.Count == 0) AddPayout("PARTICIPATION", Math.Max(1, (int)(fee * 0.1f)));

            _gameState.PlayerState.Gold += _totalGold;
        }

        private void AddPayout(string desc, int amount)
        {
            _payouts.Add(new PayoutItem
            {
                Description = desc,
                Amount = amount,
                ReasonPlink = new PlinkAnimator { MaxScale = 1.2f, HapticStrength = 0.2f },
                AmountPlink = new PlinkAnimator { MaxScale = 1.5f, HapticStrength = 0.5f },
                ReasonVisible = false,
                AmountVisible = false
            });
            _totalGold += amount;
        }

        private void QueueAnimations()
        {
            _plinkQueue.Clear();
            _plinkTimer = 0.5f;

            foreach (var p in _payouts)
            {
                _plinkQueue.Enqueue((() => { p.ReasonVisible = true; p.ReasonPlink.Start(0f, 0.3f); }, 0.25f));
                _plinkQueue.Enqueue((() => { p.AmountVisible = true; p.AmountPlink.Start(0f, 0.3f); }, 0.4f));
            }

            _plinkQueue.Enqueue((() =>
            {
                _totalPlink.Start(0f, 0.4f);
                _totalPlink.HapticStrength = 1.0f;
                _continueButton.PlayEntrance(0.2f);
                _isFinishedPlinking = true;
            }, 0f));
        }

        private void OnContinueClicked()
        {
            if (_transitionManager.IsTransitioning) return;
            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
            _gameState.AdvanceDay();
            _sceneManager.ChangeScene(GameSceneState.DayPrep, _transitionManager.GetRandomTransition(), _transitionManager.GetRandomTransition());
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);
            float dt = (float)effectiveGameTime.ElapsedGameTime.TotalSeconds;

            if (_transitionManager.IsTransitioning) return;

            if (_plinkQueue.Count > 0)
            {
                _plinkTimer -= dt;
                if (_plinkTimer <= 0)
                {
                    var item = _plinkQueue.Dequeue();
                    item.action.Invoke();
                    _plinkTimer = item.delay;
                }
            }

            foreach (var p in _payouts)
            {
                if (p.ReasonPlink.IsActive) p.ReasonPlink.Update(effectiveGameTime, new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f));
                if (p.AmountPlink.IsActive) p.AmountPlink.Update(effectiveGameTime, new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f));
            }

            if (_totalPlink.IsActive) _totalPlink.Update(effectiveGameTime, new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f));

            if (_isFinishedPlinking)
            {
                var mouseState = _inputManager.GetEffectiveMouseState();
                _continueButton.Update(mouseState);

                if (_inputManager.CurrentInputDevice == InputDeviceType.Mouse)
                {
                    _navigationGroup.DeselectAll();
                }
                else
                {
                    _navigationGroup.UpdateInput(_inputManager);
                }
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);

            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.GameBg);

            var defaultFont = ServiceLocator.Get<Core>().DefaultFont;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

            string title = "MATCH PAYOUT";
            Vector2 titleSize = defaultFont.MeasureString(title);
            spriteBatch.DrawStringSnapped(defaultFont, title, new Vector2(Global.VIRTUAL_WIDTH / 2f - titleSize.X / 2f, 15), _global.Palette_Sun);

            int startY = 45;
            int spacingY = 16;

            for (int i = 0; i < _payouts.Count; i++)
            {
                var p = _payouts[i];
                if (!p.ReasonVisible && !p.AmountVisible) continue;

                string reasonText = p.Description;
                bool hasTimeSuffix = reasonText.StartsWith("SURVIVED") && reasonText.EndsWith("s");
                string mainReasonText = hasTimeSuffix ? reasonText.Substring(0, reasonText.Length - 1) : reasonText;
                string suffixText = hasTimeSuffix ? "s" : "";

                string amountText = $"+{p.Amount}";
                string gText = "G";

                Vector2 mainRSize = secondaryFont.MeasureString(mainReasonText);
                Vector2 sufRSize = hasTimeSuffix ? tertiaryFont.MeasureString(suffixText) : Vector2.Zero;
                Vector2 rSize = new Vector2(mainRSize.X + sufRSize.X, Math.Max(mainRSize.Y, sufRSize.Y));

                Vector2 aSize = defaultFont.MeasureString(amountText);
                Vector2 gSize = secondaryFont.MeasureString(gText);

                float totalWidth = rSize.X + 10 + aSize.X + 2 + gSize.X;
                float startX = Global.VIRTUAL_WIDTH / 2f - totalWidth / 2f;
                float lineY = startY + i * spacingY;

                if (p.ReasonVisible)
                {
                    float rScale = p.ReasonPlink.IsActive ? p.ReasonPlink.Scale : 1f;
                    float rRot = p.ReasonPlink.IsActive ? p.ReasonPlink.Rotation : 0f;

                    if (rScale > 0.01f)
                    {
                        Vector2 rPos = new Vector2(MathF.Round(startX + rSize.X / 2f), MathF.Round(lineY + rSize.Y / 2f + (defaultFont.LineHeight - secondaryFont.LineHeight) / 2f));
                        Vector2 rOrigin = new Vector2(MathF.Round(rSize.X / 2f), MathF.Round(rSize.Y / 2f));
                        Color rColor = p.ReasonPlink.IsActive && p.ReasonPlink.FlashTint.HasValue ? Color.White : _global.Palette_DarkPale;

                        spriteBatch.DrawStringSnapped(secondaryFont, mainReasonText, rPos, rColor, rRot, rOrigin, rScale, SpriteEffects.None, 0f);

                        if (hasTimeSuffix)
                        {
                            Vector2 sOrigin = new Vector2(rOrigin.X - mainRSize.X, rOrigin.Y - (mainRSize.Y - sufRSize.Y));
                            Color sColor = p.ReasonPlink.IsActive && p.ReasonPlink.FlashTint.HasValue ? Color.White : _global.Palette_DarkestPale;
                            spriteBatch.DrawStringSnapped(tertiaryFont, suffixText, rPos, sColor, rRot, sOrigin, rScale, SpriteEffects.None, 0f);
                        }
                    }
                }

                if (p.AmountVisible)
                {
                    float aScale = p.AmountPlink.IsActive ? p.AmountPlink.Scale : 1f;
                    float aRot = p.AmountPlink.IsActive ? p.AmountPlink.Rotation : 0f;

                    if (aScale > 0.01f)
                    {
                        Vector2 aPos = new Vector2(MathF.Round(startX + rSize.X + 10 + aSize.X / 2f), MathF.Round(lineY + aSize.Y / 2f));
                        Vector2 aOrigin = new Vector2(MathF.Round(aSize.X / 2f), MathF.Round(aSize.Y / 2f));
                        Color aColor = p.AmountPlink.IsActive && p.AmountPlink.FlashTint.HasValue ? Color.White : _global.Palette_Sun;

                        spriteBatch.DrawStringSnapped(defaultFont, amountText, aPos, aColor, aRot, aOrigin, aScale, SpriteEffects.None, 0f);

                        Vector2 gPos = new Vector2(MathF.Round(startX + rSize.X + 10 + aSize.X + 2 + gSize.X / 2f), MathF.Round(lineY + gSize.Y / 2f + (defaultFont.LineHeight - secondaryFont.LineHeight) / 2f));
                        Vector2 gOrigin = new Vector2(MathF.Round(gSize.X / 2f), MathF.Round(gSize.Y / 2f));
                        Color gColor = p.AmountPlink.IsActive && p.AmountPlink.FlashTint.HasValue ? Color.White : _global.Palette_DarkSun;

                        spriteBatch.DrawStringSnapped(secondaryFont, gText, gPos, gColor, aRot, gOrigin, aScale, SpriteEffects.None, 0f);
                    }
                }
            }

            if (_isFinishedPlinking || _totalPlink.IsActive)
            {
                float tScale = _totalPlink.IsActive ? _totalPlink.Scale : 1f;
                float tRot = _totalPlink.IsActive ? _totalPlink.Rotation : 0f;

                if (tScale > 0.01f)
                {
                    int totalY = startY + _payouts.Count * spacingY + 10;
                    spriteBatch.Draw(pixel, new Rectangle(Global.VIRTUAL_WIDTH / 2 - 60, totalY - 5, 120, 1), _global.Palette_Black);

                    string totalDesc = "TOTAL PROFIT:";
                    string totalAmt = $"+{_totalGold}";
                    string totalG = "G";

                    Vector2 tdSize = secondaryFont.MeasureString(totalDesc);
                    Vector2 taSize = defaultFont.MeasureString(totalAmt);
                    Vector2 tgSize = secondaryFont.MeasureString(totalG);

                    float tWidth = tdSize.X + 10 + taSize.X + 2 + tgSize.X;
                    float tStartX = Global.VIRTUAL_WIDTH / 2f - tWidth / 2f;

                    Vector2 tdPos = new Vector2(MathF.Round(tStartX + tdSize.X / 2f), MathF.Round(totalY + 5 + tdSize.Y / 2f + (defaultFont.LineHeight - secondaryFont.LineHeight) / 2f));
                    Vector2 tdOrigin = new Vector2(MathF.Round(tdSize.X / 2f), MathF.Round(tdSize.Y / 2f));
                    Color tdColor = _totalPlink.IsActive && _totalPlink.FlashTint.HasValue ? Color.White : _global.Palette_DarkPale;
                    spriteBatch.DrawStringSnapped(secondaryFont, totalDesc, tdPos, tdColor, tRot, tdOrigin, tScale, SpriteEffects.None, 0f);

                    Vector2 taPos = new Vector2(MathF.Round(tStartX + tdSize.X + 10 + taSize.X / 2f), MathF.Round(totalY + 5 + taSize.Y / 2f));
                    Vector2 taOrigin = new Vector2(MathF.Round(taSize.X / 2f), MathF.Round(taSize.Y / 2f));
                    Color taColor = _totalPlink.IsActive && _totalPlink.FlashTint.HasValue ? Color.White : _global.Palette_Sky;
                    spriteBatch.DrawStringSnapped(defaultFont, totalAmt, taPos, taColor, tRot, taOrigin, tScale, SpriteEffects.None, 0f);

                    Vector2 tgPos = new Vector2(MathF.Round(tStartX + tdSize.X + 10 + taSize.X + 2 + tgSize.X / 2f), MathF.Round(totalY + 5 + tgSize.Y / 2f + (defaultFont.LineHeight - secondaryFont.LineHeight) / 2f));
                    Vector2 tgOrigin = new Vector2(MathF.Round(tgSize.X / 2f), MathF.Round(tgSize.Y / 2f));
                    Color tgColor = _totalPlink.IsActive && _totalPlink.FlashTint.HasValue ? Color.White : _global.Palette_DarkSun;
                    spriteBatch.DrawStringSnapped(secondaryFont, totalG, tgPos, tgColor, tRot, tgOrigin, tScale, SpriteEffects.None, 0f);
                }
            }

            if (_isFinishedPlinking)
            {
                _continueButton.Draw(spriteBatch, defaultFont, effectiveGameTime, transform);
            }
        }
    }
}