using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Items;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class LootScreen
    {
        // Dependencies
        private Global _global;
        private SpriteManager _spriteManager;
        private GameState _gameState;
        private HapticsManager _hapticsManager;
        private ItemTooltipRenderer _tooltipRenderer;
        private Core _core;

        // State
        public bool IsActive { get; private set; }

        public LootScreen()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _tooltipRenderer = ServiceLocator.Get<ItemTooltipRenderer>();
            _core = ServiceLocator.Get<Core>();
        }

        public void Show(List<BaseItem> loot)
        {
            // Loot screen is disabled for now
            IsActive = false;
        }

        public void Close()
        {
            IsActive = false;
        }

        public void Reset()
        {
            Close();
        }

        private void AddItemToInventory(BaseItem item)
        {
            // No-op
        }

        public void Update(GameTime gameTime)
        {
            // No-op
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // No-op
        }
    }
}
