using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class LevelUpDialog : Dialog
    {
        public enum LevelUpChoice { Core, Alt, Omni }

        private PartyMember _member;
        private Action<LevelUpChoice, string> _onChoiceMade;
        private List<Button> _buttons = new List<Button>();
        private string _titleText;

        public LevelUpDialog(GameScene scene, PartyMember member, Action<LevelUpChoice, string> onChoiceMade) : base(scene)
        {
            _member = member;
            _onChoiceMade = onChoiceMade;
            IsActive = true;
            _titleText = $"{member.Name} Leveled Up!";

            var pmData = BattleDataCache.PartyMembers.Values.FirstOrDefault(p => p.Name == member.Name);
            var rng = new Random();

            int btnWidth = 200;
            int btnHeight = 20;
            int btnX = (Global.VIRTUAL_WIDTH - btnWidth) / 2;
            int startY = Global.VIRTUAL_HEIGHT / 2 - 20;

            if (pmData != null)
            {
                if (pmData.CoreMovePool != null && pmData.CoreMovePool.Any())
                {
                    var coreEntry = pmData.CoreMovePool[rng.Next(pmData.CoreMovePool.Count)];
                    if (BattleDataCache.Moves.TryGetValue(coreEntry.MoveId, out var coreMove))
                    {
                        string prefix = coreMove.IsRare ? "[rainbow]RARE[/] " : "";
                        string text = $"Replace Core: {prefix}{coreMove.MoveName}";
                        var btn = new Button(new Rectangle(btnX, startY, btnWidth, btnHeight), text)
                        {
                            OnClick = () => MakeChoice(LevelUpChoice.Core, coreEntry.MoveId)
                        };
                        _buttons.Add(btn);
                        startY += 25;
                    }
                }

                if (pmData.AltMovePool != null && pmData.AltMovePool.Any())
                {
                    var altEntry = pmData.AltMovePool[rng.Next(pmData.AltMovePool.Count)];
                    if (BattleDataCache.Moves.TryGetValue(altEntry.MoveId, out var altMove))
                    {
                        string prefix = altMove.IsRare ? "[rainbow]RARE[/] " : "";
                        string text = $"Replace Alt: {prefix}{altMove.MoveName}";
                        var btn = new Button(new Rectangle(btnX, startY, btnWidth, btnHeight), text)
                        {
                            OnClick = () => MakeChoice(LevelUpChoice.Alt, altEntry.MoveId)
                        };
                        _buttons.Add(btn);
                        startY += 25;
                    }
                }
            }

            var omniBtn = new Button(new Rectangle(btnX, startY, btnWidth, btnHeight), "Omni-Stat Boost (+1 to all)")
            {
                OnClick = () => MakeChoice(LevelUpChoice.Omni, null)
            };
            _buttons.Add(omniBtn);
        }

        private void MakeChoice(LevelUpChoice choice, string moveId)
        {
            IsActive = false;
            _onChoiceMade?.Invoke(choice, moveId);
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;
            var mouseState = Mouse.GetState();
            foreach (var btn in _buttons) btn.Update(mouseState);
        }

        public override void DrawContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (!IsActive) return;

            Vector2 titleSize = font.MeasureString(_titleText);
            spriteBatch.DrawStringSnapped(font, _titleText, new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, Global.VIRTUAL_HEIGHT / 2 - 50), _global.Palette_Sun);

            foreach (var btn in _buttons) btn.Draw(spriteBatch, font, gameTime, transform);
        }
    }
}