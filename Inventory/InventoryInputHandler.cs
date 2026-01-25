using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class InventoryInputHandler
    {
        private readonly Global _global;
        private readonly SplitMapInventoryOverlay _overlay;

        // Updated to 1 argument
        public InventoryInputHandler(SplitMapInventoryOverlay overlay)
        {
            _overlay = overlay;
            _global = ServiceLocator.Get<Global>();
        }

        public void InitializeInventoryUI()
        {
            if (_overlay.InventoryButton == null)
            {
                var inventoryIcon = _overlay.SpriteManager.SplitMapInventoryButton;
                var rects = _overlay.SpriteManager.SplitMapInventoryButtonSourceRects;
                _overlay.InventoryButton = new ImageButton(new Rectangle(2, 2, 16, 16), inventoryIcon, rects[0], rects[1], enableHoverSway: true);
                _overlay.InventoryButton.OnClick += () => _overlay.TriggerInventoryButtonClicked();
            }
            _overlay.InventoryButton.ResetAnimationState();

            // --- Setup Party Panels ---
            const int statsPanelHeight = 132;
            const int panelWidth = 76;

            // Center the 4 panels on screen
            int totalWidth = 4 * panelWidth;
            int panelStartX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;
            int statsPanelY = 200 + 6 + 32 + 1 - 1; // Match previous Y positioning logic

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var defaultFont = ServiceLocator.Get<BitmapFont>();

            _overlay.PartySpellButtons.Clear();

            for (int i = 0; i < 4; i++)
            {
                _overlay.PartyMemberPanelAreas[i] = new Rectangle(
                    panelStartX + (i * panelWidth),
                    statsPanelY,
                    panelWidth,
                    statsPanelHeight
                );

                int centerX = _overlay.PartyMemberPanelAreas[i].Center.X;
                int currentY = _overlay.PartyMemberPanelAreas[i].Y + 4;

                // Simulate layout to find spell button Y
                currentY += defaultFont.LineHeight - 2; // Name
                currentY += 32 + 2 - 6; // Portrait
                currentY += 8 + secondaryFont.LineHeight + 4 - 3; // HP

                currentY += (int)secondaryFont.LineHeight + 1; // Str
                currentY += (int)secondaryFont.LineHeight + 1; // Int
                currentY += (int)secondaryFont.LineHeight + 1; // Ten
                currentY += (int)secondaryFont.LineHeight + 1; // Agi

                currentY += 2; // Gap

                int spellButtonWidth = 64;
                int spellButtonHeight = 8;
                int spellButtonX = centerX - (spellButtonWidth / 2);
                int spellButtonY = currentY + 2 - 8;

                for (int s = 0; s < 4; s++)
                {
                    var spellBtn = new SpellEquipButton(new Rectangle(spellButtonX, spellButtonY, spellButtonWidth, spellButtonHeight));
                    _overlay.PartySpellButtons.Add(spellBtn);
                    spellButtonY += spellButtonHeight;
                }
            }
        }

        public void Update(GameTime gameTime, MouseState currentMouseState, KeyboardState currentKeyboardState, bool allowAccess, Matrix cameraTransform)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_overlay.InventoryButton != null)
            {
                bool isVisible = _overlay.IsOpen || allowAccess;
                if (isVisible)
                {
                    _overlay.InventoryButton.IsEnabled = true;
                    _overlay.InventoryButton.Update(currentMouseState);
                }
                else
                {
                    _overlay.InventoryButton.ResetAnimationState();
                }
            }

            if (!_overlay.IsOpen) return;

            _overlay.HoveredItemData = null;
            _overlay.HoveredMemberIndex = -1;

            int partyCount = _overlay.GameState.PlayerState.Party.Count;

            // Update Spell Buttons
            for (int i = 0; i < _overlay.PartySpellButtons.Count; i++)
            {
                int memberIndex = i / 4;
                int slotIndex = i % 4;

                if (memberIndex < partyCount)
                {
                    var btn = _overlay.PartySpellButtons[i];
                    btn.IsEnabled = true;
                    btn.Update(currentMouseState, cameraTransform);

                    if (btn.IsHovered)
                    {
                        _overlay.HoveredMemberIndex = memberIndex;
                        var member = _overlay.GameState.PlayerState.Party[memberIndex];
                        var spellEntry = member.Spells[slotIndex];
                        if (spellEntry != null && BattleDataCache.Moves.TryGetValue(spellEntry.MoveID, out var moveData))
                        {
                            _overlay.HoveredItemData = moveData;
                            ServiceLocator.Get<CursorManager>().SetState(CursorState.Hint);
                        }
                    }
                }
                else
                {
                    _overlay.PartySpellButtons[i].IsEnabled = false;
                    _overlay.PartySpellButtons[i].ResetAnimationState();
                }
            }

            _overlay.StatCycleTimer += deltaTime;
            if (_overlay.HoveredItemData != _overlay.PreviousHoveredItemData)
            {
                _overlay.StatCycleTimer = 0f;
                _overlay.PreviousHoveredItemData = _overlay.HoveredItemData;
                _overlay.InfoPanelNameWaveTimer = 0f;
            }

            int nameLength = 0;
            if (_overlay.HoveredItemData is MoveData md) nameLength = md.MoveName.Length;
            else if (_overlay.HoveredItemData is RelicData rd) nameLength = rd.RelicName.Length;

            if (_overlay.HoveredItemData != null)
            {
                _overlay.InfoPanelNameWaveTimer += deltaTime;
                float duration = TextAnimator.GetSmallWaveDuration(nameLength);
                if (_overlay.InfoPanelNameWaveTimer > duration + 0.1f) _overlay.InfoPanelNameWaveTimer = 0f;
            }
            else
            {
                _overlay.InfoPanelNameWaveTimer = 0f;
            }

            _overlay.PreviousMouseState = currentMouseState;
            _overlay.PreviousKeyboardState = currentKeyboardState;
        }
    }
}