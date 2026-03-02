using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    public class LevelUpDialog : Dialog
    {
        public enum LevelUpChoice { Spell1, Spell2, Spell3, Skip }

        private PartyMember _member;
        private Action<LevelUpChoice, string> _onChoiceMade;
        private List<Button> _buttons = new List<Button>();
        private string _titleText;
        private string _newMoveId;

        public LevelUpDialog(GameScene scene, PartyMember member, string newMoveId, Action<LevelUpChoice, string> onChoiceMade) : base(scene)
        {
            _member = member;
            _newMoveId = newMoveId;
            _onChoiceMade = onChoiceMade;
            IsActive = true;

            string moveName = BattleDataCache.Moves.TryGetValue(newMoveId, out var m) ? m.MoveName : "Unknown Move";
            _titleText = $"{member.Name} learned {moveName}!\nChoose a spell to replace:";

            int btnWidth = 200;
            int btnHeight = 20;
            int btnX = (Global.VIRTUAL_WIDTH - btnWidth) / 2;
            int startY = Global.VIRTUAL_HEIGHT / 2 - 20;

            void AddReplaceButton(string label, MoveEntry currentMove, LevelUpChoice choice)
            {
                if (currentMove != null && BattleDataCache.Moves.TryGetValue(currentMove.MoveID, out var data))
                {
                    var btn = new Button(new Rectangle(btnX, startY, btnWidth, btnHeight), $"Replace {label}: {data.MoveName}")
                    {
                        OnClick = () => MakeChoice(choice, _newMoveId)
                    };
                    _buttons.Add(btn);
                    startY += 25;
                }
            }

            AddReplaceButton("Spell 1", _member.Spell1, LevelUpChoice.Spell1);
            AddReplaceButton("Spell 2", _member.Spell2, LevelUpChoice.Spell2);
            AddReplaceButton("Spell 3", _member.Spell3, LevelUpChoice.Spell3);

            var skipBtn = new Button(new Rectangle(btnX, startY, btnWidth, btnHeight), "Skip learning")
            {
                OnClick = () => MakeChoice(LevelUpChoice.Skip, null)
            };
            _buttons.Add(skipBtn);
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
            spriteBatch.DrawStringSnapped(font, _titleText, new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, Global.VIRTUAL_HEIGHT / 2 - 60), _global.Palette_Sun);
            foreach (var btn in _buttons) btn.Draw(spriteBatch, font, gameTime, transform);
        }
    }
}