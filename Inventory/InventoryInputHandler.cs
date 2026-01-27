using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class InventoryInputHandler
    {
        private readonly Global _global;
        private readonly PartyStatusOverlay _overlay;

        public InventoryInputHandler(PartyStatusOverlay overlay)
        {
            _overlay = overlay;
            _global = ServiceLocator.Get<Global>();
        }

        public void InitializeInventoryUI()
        {
            // Initialize Close Button (Top Left)
            if (_overlay.CloseButton == null)
            {
                var closeIcon = _overlay.SpriteManager.SplitMapCloseInventoryButton;
                var rects = _overlay.SpriteManager.SplitMapCloseInventoryButtonSourceRects;
                // Position at 2,2 (Top Left)
                _overlay.CloseButton = new ImageButton(new Rectangle(2, 2, 16, 16), closeIcon, rects[0], rects[1], enableHoverSway: true);
                _overlay.CloseButton.OnClick += () => _overlay.TriggerCloseRequested();
            }
            _overlay.CloseButton.ResetAnimationState();

            // --- Setup Party Panels ---
            const int statsPanelHeight = 132;
            const int panelWidth = 76;

            // Center the 4 panels on screen
            int totalWidth = 4 * panelWidth;
            int panelStartX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;
            int statsPanelY = 200 + 6 + 32 + 1 - 1;

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

                // --- BOTTOM ALIGNED LAYOUT ---
                // Anchor spells to the bottom of the panel
                int bottomPadding = 4;
                int spellButtonHeight = 8;
                int numSpells = 4;
                int totalSpellHeight = numSpells * spellButtonHeight;

                int spellStartY = _overlay.PartyMemberPanelAreas[i].Bottom - bottomPadding - totalSpellHeight;

                int spellButtonWidth = 64;
                int spellButtonX = centerX - (spellButtonWidth / 2);
                int currentSpellY = spellStartY;

                for (int s = 0; s < 4; s++)
                {
                    // Create the button purely for visual layout and hover detection.
                    // No OnClick events are attached here.
                    var spellBtn = new SpellEquipButton(new Rectangle(spellButtonX, currentSpellY, spellButtonWidth, spellButtonHeight));
                    _overlay.PartySpellButtons.Add(spellBtn);
                    currentSpellY += spellButtonHeight;
                }
            }
        }

        public void Update(GameTime gameTime, MouseState currentMouseState, KeyboardState currentKeyboardState, bool allowAccess, Matrix cameraTransform)
        {
            // Only update Close Button if the menu is actually open
            if (_overlay.IsOpen && _overlay.CloseButton != null)
            {
                _overlay.CloseButton.IsEnabled = true;
                _overlay.CloseButton.Update(currentMouseState);
            }
            else if (_overlay.CloseButton != null)
            {
                _overlay.CloseButton.ResetAnimationState();
            }

            if (!_overlay.IsOpen) return;

            _overlay.HoveredItemData = null;
            _overlay.HoveredMemberIndex = -1;

            int partyCount = _overlay.GameState.PlayerState.Party.Count;

            // Update Spell Buttons (Read-Only Mode)
            for (int i = 0; i < _overlay.PartySpellButtons.Count; i++)
            {
                int memberIndex = i / 4;
                int slotIndex = i % 4;

                if (memberIndex < partyCount)
                {
                    var btn = _overlay.PartySpellButtons[i];
                    btn.IsEnabled = true; // Enabled so we can hover for tooltips

                    // We update the button to track mouse position, but we DO NOT process clicks for equipping.
                    btn.Update(currentMouseState, cameraTransform);

                    if (btn.IsHovered)
                    {
                        _overlay.HoveredMemberIndex = memberIndex;
                        var member = _overlay.GameState.PlayerState.Party[memberIndex];
                        var spellEntry = member.Spells[slotIndex];
                        if (spellEntry != null && BattleDataCache.Moves.TryGetValue(spellEntry.MoveID, out var moveData))
                        {
                            _overlay.HoveredItemData = moveData;
                            // Hint cursor shows "Info" is available, but not "Clickable"
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

            _overlay.StatCycleTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
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
                _overlay.InfoPanelNameWaveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
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