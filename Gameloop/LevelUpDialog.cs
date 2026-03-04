using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class LevelUpDialog : Dialog
    {
        private class TokenOptionButton
        {
            public Button Btn;
            public Rectangle Bounds;
            public ModifierToken Token;
            public int MoveIndex; // 1 = Strike, 2 = Alt
        }

        private LevelUpDraftData _draftData;
        private Action<int, ModifierToken> _onChoiceMade;
        private List<TokenOptionButton> _buttons = new List<TokenOptionButton>();
        private string _titleText;

        private ModifierToken _hoveredToken;
        private int _hoveredMoveIndex;

        public LevelUpDialog(GameScene scene, LevelUpDraftData draftData, Action<int, ModifierToken> onChoiceMade) : base(scene)
        {
            _draftData = draftData;
            _onChoiceMade = onChoiceMade;
            IsActive = true;

            _titleText = $"{_draftData.Member.Name} is leveling up!\nDraft a modifier token:";

            int btnWidth = 300;
            int btnHeight = 25;
            int btnX = (Global.VIRTUAL_WIDTH - btnWidth) / 2;
            int startY = Global.VIRTUAL_HEIGHT / 2 - 75;

            if (_draftData.StrikeOptions != null && _draftData.StrikeOptions.Any() && _draftData.Member.StrikeMove != null)
            {
                foreach (var t in _draftData.StrikeOptions)
                {
                    var rect = new Rectangle(btnX, startY, btnWidth, btnHeight);
                    var btn = new Button(rect, $"Strike: {t.Name}")
                    {
                        OnClick = () => MakeChoice(1, t)
                    };
                    _buttons.Add(new TokenOptionButton { Btn = btn, Bounds = rect, Token = t, MoveIndex = 1 });
                    startY += 30;
                }
            }

            if (_draftData.AltOptions != null && _draftData.AltOptions.Any() && _draftData.Member.AltMove != null)
            {
                foreach (var t in _draftData.AltOptions)
                {
                    var rect = new Rectangle(btnX, startY, btnWidth, btnHeight);
                    var btn = new Button(rect, $"Alt: {t.Name}")
                    {
                        OnClick = () => MakeChoice(2, t)
                    };
                    _buttons.Add(new TokenOptionButton { Btn = btn, Bounds = rect, Token = t, MoveIndex = 2 });
                    startY += 30;
                }
            }

            var skipRect = new Rectangle(btnX, startY + 10, btnWidth, btnHeight);
            var skipBtn = new Button(skipRect, "Skip drafting")
            {
                OnClick = () => MakeChoice(0, null)
            };
            _buttons.Add(new TokenOptionButton { Btn = skipBtn, Bounds = skipRect, Token = null, MoveIndex = 0 });
        }

        private void MakeChoice(int moveIndex, ModifierToken chosenToken)
        {
            IsActive = false;

            if (chosenToken != null)
            {
                var targetMove = moveIndex == 1 ? _draftData.Member.StrikeMove : _draftData.Member.AltMove;
                if (targetMove != null)
                {
                    targetMove.CompiledMove.Tokens.Add(chosenToken);
                    targetMove.CompiledMove = new CompiledMove(targetMove.CompiledMove.BaseTemplate, targetMove.CompiledMove.Tokens);
                }
            }

            _onChoiceMade?.Invoke(moveIndex, chosenToken);
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;
            var mouseState = Mouse.GetState();

            _hoveredToken = null;
            _hoveredMoveIndex = 0;

            foreach (var tb in _buttons)
            {
                tb.Btn.Update(mouseState);
                if (tb.Bounds.Contains(mouseState.Position) && tb.Token != null)
                {
                    _hoveredToken = tb.Token;
                    _hoveredMoveIndex = tb.MoveIndex;
                }
            }
        }

        private ModifierToken CloneToken(ModifierToken t)
        {
            return new ModifierToken
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                ModifiedCategories = new HashSet<ModifierCategory>(t.ModifiedCategories),
                IsDisabled = t.IsDisabled,
                TargetOverride = t.TargetOverride,
                FlatDamageBonus = t.FlatDamageBonus,
                DamageMultiplier = t.DamageMultiplier,
                CooldownModifier = t.CooldownModifier,
                AnimationIdOverride = t.AnimationIdOverride,
                AppendedAbilities = t.AppendedAbilities != null ? new List<IAbility>(t.AppendedAbilities) : new List<IAbility>()
            };
        }

        public override void DrawContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (!IsActive) return;

            Vector2 titleSize = font.MeasureString(_titleText);
            spriteBatch.DrawStringSnapped(font, _titleText, new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, Global.VIRTUAL_HEIGHT / 2 - 120), _global.Palette_Sun);

            foreach (var tb in _buttons)
            {
                tb.Btn.Draw(spriteBatch, font, gameTime, transform);
            }

            if (_hoveredToken != null)
            {
                var targetMove = _hoveredMoveIndex == 1 ? _draftData.Member.StrikeMove : _draftData.Member.AltMove;
                var currentMove = targetMove.CompiledMove;

                var simulatedTokens = currentMove.Tokens.Select(t => CloneToken(t)).ToList();
                simulatedTokens.Add(CloneToken(_hoveredToken));

                ModifierToken.ResolveConflicts(simulatedTokens);

                var simulatedCompiled = new CompiledMove(currentMove.BaseTemplate, simulatedTokens);

                string statsText = $"PWR: {currentMove.FinalPower} -> {simulatedCompiled.FinalPower}   CD: {currentMove.FinalCooldown} -> {simulatedCompiled.FinalCooldown}";
                Vector2 statsSize = font.MeasureString(statsText);
                spriteBatch.DrawStringSnapped(font, statsText, new Vector2((Global.VIRTUAL_WIDTH - statsSize.X) / 2, Global.VIRTUAL_HEIGHT / 2 + 100), Color.White);

                var disabledNames = new List<string>();
                for (int i = 0; i < currentMove.Tokens.Count; i++)
                {
                    if (!currentMove.Tokens[i].IsDisabled && simulatedTokens[i].IsDisabled)
                    {
                        disabledNames.Add(currentMove.Tokens[i].Name);
                    }
                }

                if (disabledNames.Any())
                {
                    string warning = $"WARNING: Drafting this disables {string.Join(", ", disabledNames)}";
                    Vector2 warningSize = font.MeasureString(warning);
                    spriteBatch.DrawStringSnapped(font, warning, new Vector2((Global.VIRTUAL_WIDTH - warningSize.X) / 2, Global.VIRTUAL_HEIGHT / 2 + 125), Color.Red);
                }
            }
        }
    }
}