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
            public int SpellIndex;
        }

        private PartyMember _member;
        private Action<int, ModifierToken> _onChoiceMade;
        private List<TokenOptionButton> _buttons = new List<TokenOptionButton>();
        private string _titleText;

        private ModifierToken _hoveredToken;
        private int _hoveredSpellIndex;

        public LevelUpDialog(GameScene scene, PartyMember member, List<ModifierToken> spell1Options, List<ModifierToken> spell2Options, Action<int, ModifierToken> onChoiceMade) : base(scene)
        {
            _member = member;
            _onChoiceMade = onChoiceMade;
            IsActive = true;

            _titleText = $"{member.Name} is leveling up!\nDraft a modifier token:";

            int btnWidth = 300;
            int btnHeight = 25;
            int btnX = (Global.VIRTUAL_WIDTH - btnWidth) / 2;
            int startY = Global.VIRTUAL_HEIGHT / 2 - 60;

            if (spell1Options != null && spell1Options.Any() && _member.Spell1 != null)
            {
                foreach (var t in spell1Options)
                {
                    var rect = new Rectangle(btnX, startY, btnWidth, btnHeight);
                    var btn = new Button(rect, $"Spell 1: {t.Name}")
                    {
                        OnClick = () => MakeChoice(1, t)
                    };
                    _buttons.Add(new TokenOptionButton { Btn = btn, Bounds = rect, Token = t, SpellIndex = 1 });
                    startY += 30;
                }
            }

            if (spell2Options != null && spell2Options.Any() && _member.Spell2 != null)
            {
                foreach (var t in spell2Options)
                {
                    var rect = new Rectangle(btnX, startY, btnWidth, btnHeight);
                    var btn = new Button(rect, $"Spell 2: {t.Name}")
                    {
                        OnClick = () => MakeChoice(2, t)
                    };
                    _buttons.Add(new TokenOptionButton { Btn = btn, Bounds = rect, Token = t, SpellIndex = 2 });
                    startY += 30;
                }
            }

            var skipRect = new Rectangle(btnX, startY + 10, btnWidth, btnHeight);
            var skipBtn = new Button(skipRect, "Skip drafting")
            {
                OnClick = () => MakeChoice(0, null)
            };
            _buttons.Add(new TokenOptionButton { Btn = skipBtn, Bounds = skipRect, Token = null, SpellIndex = 0 });
        }

        private void MakeChoice(int spellIndex, ModifierToken chosenToken)
        {
            IsActive = false;

            if (chosenToken != null)
            {
                var targetSpell = spellIndex == 1 ? _member.Spell1 : _member.Spell2;
                if (targetSpell != null)
                {
                    targetSpell.CompiledMove.Tokens.Add(chosenToken);
                    targetSpell.CompiledMove = new CompiledMove(targetSpell.CompiledMove.BaseTemplate, targetSpell.CompiledMove.Tokens);
                }
            }

            _onChoiceMade?.Invoke(spellIndex, chosenToken);
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;
            var mouseState = Mouse.GetState();

            _hoveredToken = null;
            _hoveredSpellIndex = 0;

            foreach (var tb in _buttons)
            {
                tb.Btn.Update(mouseState);
                if (tb.Bounds.Contains(mouseState.Position) && tb.Token != null)
                {
                    _hoveredToken = tb.Token;
                    _hoveredSpellIndex = tb.SpellIndex;
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
                var targetSpell = _hoveredSpellIndex == 1 ? _member.Spell1 : _member.Spell2;
                var currentMove = targetSpell.CompiledMove;

                var simulatedTokens = currentMove.Tokens.Select(t => CloneToken(t)).ToList();
                simulatedTokens.Add(CloneToken(_hoveredToken));

                var simulatedCompiled = new CompiledMove(currentMove.BaseTemplate, simulatedTokens);

                string statsText = $"PWR: {currentMove.FinalPower} -> {simulatedCompiled.FinalPower}   CD: {currentMove.FinalCooldown} -> {simulatedCompiled.FinalCooldown}";
                Vector2 statsSize = font.MeasureString(statsText);
                spriteBatch.DrawStringSnapped(font, statsText, new Vector2((Global.VIRTUAL_WIDTH - statsSize.X) / 2, Global.VIRTUAL_HEIGHT / 2 + 80), Color.White);

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
                    string warning = $"WARNING: Drafting this will disable {string.Join(", ", disabledNames)}";
                    Vector2 warningSize = font.MeasureString(warning);
                    spriteBatch.DrawStringSnapped(font, warning, new Vector2((Global.VIRTUAL_WIDTH - warningSize.X) / 2, Global.VIRTUAL_HEIGHT / 2 + 105), Color.Red);
                }
            }
        }
    }
}