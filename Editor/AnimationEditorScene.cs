using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Editor;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Editor
{
    public class AnimationEditorScene : GameScene, IAnimationPlaybackContext
    {
        // --- TUNING ---
        private const int FILE_BROWSER_WIDTH = 180;
        private const int CONTROL_PANEL_HEIGHT = 100;
        private const int PANEL_PADDING = 5;

        private HandRenderer _leftHand;
        private HandRenderer _rightHand;
        private FileBrowser _fileBrowser;
        private TransformGizmo _transformGizmo;
        private ContextMenu _animationContextMenu;
        private ContextMenu _particleAnchorContextMenu;
        private ContextMenu _particleEffectContextMenu;
        private List<Button> _controlButtons = new List<Button>();

        private PoseData _loadedPose;
        private string _loadedPosePath;
        private bool _isDataDirty = false;

        // --- Interactive Editing State ---
        private HandRenderer _selectedHand;
        private bool _isEditMode = true; // Default to edit mode in the editor
        private float _combatScreenBottomY;

        // --- Coordinate Transformation ---
        private Matrix _previewTransformMatrix;
        private Matrix _inversePreviewTransformMatrix;
        private Rectangle _previewArea;


        public Dictionary<string, Vector2> AnimationAnchors { get; private set; }

        public override bool UsesLetterboxing => true;

        public override void Initialize()
        {
            base.Initialize();

            _leftHand = new HandRenderer(HandType.Left, Vector2.Zero);
            _rightHand = new HandRenderer(HandType.Right, Vector2.Zero);

            _fileBrowser = new FileBrowser();
            _fileBrowser.OnFileSelected += OnPoseFileSelected;

            _transformGizmo = new TransformGizmo();
            _transformGizmo.OnAnimationGizmoClicked += ShowAnimationMenu;

            _animationContextMenu = new ContextMenu();
            _particleAnchorContextMenu = new ContextMenu();
            _particleEffectContextMenu = new ContextMenu();

            InitializeControlPanel();
        }

        private void InitializeControlPanel()
        {
            var saveButton = new Button(Rectangle.Empty, "Save (Ctrl+S)");
            saveButton.OnClick += OnSave;
            _controlButtons.Add(saveButton);

            var particleAnchorButton = new Button(Rectangle.Empty, "Set Particle Anchor");
            particleAnchorButton.OnClick += ShowParticleAnchorMenu;
            _controlButtons.Add(particleAnchorButton);

            var particleEffectButton = new Button(Rectangle.Empty, "Set Particle Effect");
            particleEffectButton.OnClick += ShowParticleEffectMenu;
            _controlButtons.Add(particleEffectButton);

            var leftHandLayerButton = new Button(Rectangle.Empty, "L-Hand: Front");
            leftHandLayerButton.OnClick += () => ToggleRenderLayer(_leftHand);
            _controlButtons.Add(leftHandLayerButton);

            var rightHandLayerButton = new Button(Rectangle.Empty, "R-Hand: Front");
            rightHandLayerButton.OnClick += () => ToggleRenderLayer(_rightHand);
            _controlButtons.Add(rightHandLayerButton);
        }

        public override void Enter()
        {
            base.Enter();
            EventBus.Subscribe<GameEvents.UIThemeOrResolutionChanged>(OnResolutionChanged);
            _leftHand.LoadContent();
            _rightHand.LoadContent();

            CalculateLayouts();
            _leftHand.EnterScene();
            _rightHand.EnterScene();

            // Move hands to idle position for visibility
            _leftHand.MoveTo(AnimationAnchors["LeftHandIdle"], 0.5f, Easing.EaseOutCubic);
            _rightHand.MoveTo(AnimationAnchors["RightHandIdle"], 0.5f, Easing.EaseOutCubic);

            string sourceContentPath = ProjectDirectoryResolver.Resolve("Content/Poses");
            _fileBrowser.Populate(sourceContentPath);
        }

        public override void Exit()
        {
            base.Exit();
            EventBus.Unsubscribe<GameEvents.UIThemeOrResolutionChanged>(OnResolutionChanged);
        }

        private void OnResolutionChanged(GameEvents.UIThemeOrResolutionChanged e)
        {
            CalculateLayouts();
            if (_loadedPose != null)
            {
                SnapHandsToPose(_loadedPose);
            }
            else
            {
                _leftHand.MoveTo(AnimationAnchors["LeftHandIdle"], 0.3f, Easing.EaseOutCubic);
                _rightHand.MoveTo(AnimationAnchors["RightHandIdle"], 0.3f, Easing.EaseOutCubic);
            }
        }

        private void CalculateLayouts()
        {
            var bounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

            _fileBrowser.Bounds = new Rectangle(
                bounds.X + PANEL_PADDING,
                bounds.Y + PANEL_PADDING,
                FILE_BROWSER_WIDTH,
                bounds.Height - (PANEL_PADDING * 2)
            );

            // Calculate the preview area bounds first
            _previewArea = GetPreviewAreaBounds();

            // Calculate the transformation matrix to map the full virtual screen into the preview area
            float scaleX = (float)_previewArea.Width / Global.VIRTUAL_WIDTH;
            float scaleY = (float)_previewArea.Height / Global.VIRTUAL_HEIGHT;
            float scale = Math.Min(scaleX, scaleY); // Maintain aspect ratio

            // Center the scaled content within the preview area
            float scaledWidth = Global.VIRTUAL_WIDTH * scale;
            float scaledHeight = Global.VIRTUAL_HEIGHT * scale;
            float offsetX = _previewArea.X + (_previewArea.Width - scaledWidth) / 2f;
            float offsetY = _previewArea.Y + (_previewArea.Height - scaledHeight) / 2f;

            _previewTransformMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(offsetX, offsetY, 0);
            _inversePreviewTransformMatrix = Matrix.Invert(_previewTransformMatrix);


            AnimationAnchors = AnimationAnchorCalculator.CalculateAnchors(isEditor: true, out _combatScreenBottomY);

            _leftHand.SetIdlePosition(AnimationAnchors["LeftHandIdle"]);
            _rightHand.SetIdlePosition(AnimationAnchors["RightHandIdle"]);
            _leftHand.SetOffscreenPosition(AnimationAnchors["LeftHandOffscreen"]);
            _rightHand.SetOffscreenPosition(AnimationAnchors["RightHandOffscreen"]);

            // Layout control panel buttons
            var controlPanelBounds = GetControlPanelBounds();
            int buttonWidth = 140;
            int buttonHeight = 20;
            int buttonSpacing = 10;
            int currentX = controlPanelBounds.X + 10;
            int buttonY = controlPanelBounds.Y + 10;

            _controlButtons[0].Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight); // Save
            currentX += buttonWidth + buttonSpacing;
            _controlButtons[1].Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight); // Anchor
            currentX += buttonWidth + buttonSpacing;
            _controlButtons[2].Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight); // Effect

            // Second row for layer buttons
            currentX = controlPanelBounds.X + 10;
            buttonY += buttonHeight + 5;
            _controlButtons[3].Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight); // Left Layer
            currentX += buttonWidth + buttonSpacing;
            _controlButtons[4].Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight); // Right Layer
        }

        public override void Update(GameTime gameTime)
        {
            var kbs = Keyboard.GetState();
            var ms = Mouse.GetState();
            var vms = Core.TransformMouse(ms.Position);
            var font = ServiceLocator.Get<BitmapFont>();

            bool isCtrlDown = kbs.IsKeyDown(Keys.LeftControl) || kbs.IsKeyDown(Keys.RightControl);
            if (isCtrlDown && kbs.IsKeyDown(Keys.S) && _previousKeyboardState.IsKeyUp(Keys.S))
            {
                OnSave();
            }

            // Transform mouse into the preview area's coordinate space for interaction checks
            var transformedMousePos = Vector2.Transform(vms, _inversePreviewTransformMatrix);

            _animationContextMenu.Update(ms, previousMouseState, vms, font);
            _particleAnchorContextMenu.Update(ms, previousMouseState, vms, font);
            _particleEffectContextMenu.Update(ms, previousMouseState, vms, font);

            if (_isEditMode)
            {
                _transformGizmo.Update(ms, previousMouseState, transformedMousePos);
                HandleHandSelection(transformedMousePos);
                if (_transformGizmo.IsDragging)
                {
                    UpdatePoseFromGizmo();
                }
            }

            _fileBrowser.Update(gameTime, _isEditMode);
            foreach (var button in _controlButtons)
            {
                button.Update(ms);
            }

            _leftHand.Update(gameTime);
            _rightHand.Update(gameTime);

            base.Update(gameTime);
        }

        private void HandleHandSelection(Vector2 transformedMousePos)
        {
            var mouseState = Mouse.GetState();
            bool leftClickPressed = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;

            if (_transformGizmo.IsDragging) return;

            if (leftClickPressed && UIInputManager.CanProcessMouseClick())
            {
                // Use the transformed mouse position for checking bounds in world space
                if (_leftHand.GetInteractionBounds().Contains(transformedMousePos))
                {
                    _selectedHand = _leftHand;
                }
                else if (_rightHand.GetInteractionBounds().Contains(transformedMousePos))
                {
                    _selectedHand = _rightHand;
                }
                else
                {
                    _selectedHand = null;
                }

                if (_selectedHand != null)
                {
                    _transformGizmo.Attach(_selectedHand);
                    UIInputManager.ConsumeMouseClick();
                }
                else
                {
                    _transformGizmo.Detach();
                }
            }
        }

        private void UpdatePoseFromGizmo()
        {
            if (_loadedPose == null || _selectedHand == null) return;

            _isDataDirty = true;
            HandState stateToUpdate = (_selectedHand == _leftHand) ? _loadedPose.LeftHand : _loadedPose.RightHand;

            stateToUpdate.Position = _selectedHand.CurrentPosition;
            stateToUpdate.Rotation = MathHelper.ToDegrees(_selectedHand.CurrentRotation);
            stateToUpdate.Scale = _selectedHand.CurrentScale;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            DrawSceneContent(spriteBatch, font, gameTime);
            spriteBatch.End();

            _fileBrowser.Draw(spriteBatch, font, gameTime, transform);
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var global = ServiceLocator.Get<Global>();

            spriteBatch.Draw(pixel, _previewArea, new Color(20, 20, 25));

            // Draw faint compositional grid lines
            var lineColor = Color.White * 0.1f;
            float thirdWidth = _previewArea.Width / 3f;
            float thirdHeight = _previewArea.Height / 3f;
            spriteBatch.DrawLineSnapped(new Vector2(_previewArea.X + thirdWidth, _previewArea.Y), new Vector2(_previewArea.X + thirdWidth, _previewArea.Bottom), lineColor);
            spriteBatch.DrawLineSnapped(new Vector2(_previewArea.X + thirdWidth * 2, _previewArea.Y), new Vector2(_previewArea.X + thirdWidth * 2, _previewArea.Bottom), lineColor);
            spriteBatch.DrawLineSnapped(new Vector2(_previewArea.X, _previewArea.Y + thirdHeight), new Vector2(_previewArea.Right, _previewArea.Y + thirdHeight), lineColor);
            spriteBatch.DrawLineSnapped(new Vector2(_previewArea.X, _previewArea.Y + thirdHeight * 2), new Vector2(_previewArea.Right, _previewArea.Y + thirdHeight * 2), lineColor);

            // Transform the combat floor line into the preview space
            var transformedCombatFloorY = Vector2.Transform(new Vector2(0, _combatScreenBottomY), _previewTransformMatrix).Y;
            spriteBatch.Draw(pixel, new Rectangle(_previewArea.X, (int)transformedCombatFloorY, _previewArea.Width, 1), Color.LimeGreen);

            var controlPanelBounds = GetControlPanelBounds();
            spriteBatch.Draw(pixel, controlPanelBounds, new Color(30, 30, 40));
            spriteBatch.DrawRectangle(controlPanelBounds, Color.Gray, 1f);

            // Update button text before drawing
            if (_loadedPose != null)
            {
                _controlButtons[3].Text = $"L-Hand: {(_loadedPose.LeftHand.RenderLayer == RenderLayer.InFrontOfParticles ? "Front" : "Back")}";
                _controlButtons[4].Text = $"R-Hand: {(_loadedPose.RightHand.RenderLayer == RenderLayer.InFrontOfParticles ? "Front" : "Back")}";
            }

            foreach (var button in _controlButtons)
            {
                button.Draw(spriteBatch, font, gameTime);
            }

            // Draw the "Currently Editing" text
            string currentPoseText = _loadedPose != null ? $"Editing: {_loadedPose.Id}" : "*SELECT A POSE*";
            Color currentPoseColor = _loadedPose != null ? (_isDataDirty ? global.Palette_Teal : global.Palette_BrightWhite) : global.Palette_Gray;
            Vector2 textPosition = new Vector2(controlPanelBounds.X + 10, controlPanelBounds.Y + 70);
            spriteBatch.DrawStringSnapped(font, currentPoseText, textPosition, currentPoseColor);
        }

        public override void DrawFullscreenUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // Combine the main letterboxing transform with our local preview area transform
            Matrix finalTransform = _previewTransformMatrix * transform;

            // --- Pass 1: Draw Hands Behind Particles ---
            spriteBatch.Begin(sortMode: SpriteSortMode.Deferred, blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: finalTransform);
            if (_loadedPose != null)
            {
                if (_loadedPose.LeftHand.RenderLayer == RenderLayer.BehindParticles)
                {
                    _leftHand.Draw(spriteBatch, font, gameTime, Color.White);
                }
                if (_loadedPose.RightHand.RenderLayer == RenderLayer.BehindParticles)
                {
                    _rightHand.Draw(spriteBatch, font, gameTime, Color.White);
                }
            }
            else // If no pose is loaded, draw them in a default state
            {
                _leftHand.Draw(spriteBatch, font, gameTime, Color.White);
                _rightHand.Draw(spriteBatch, font, gameTime, Color.White);
            }
            spriteBatch.End();

            // --- Pass 2: Draw Particle Effects (if any) ---
            // In the editor, we might not have a particle system, so this is a placeholder.
            // For a full implementation, you would call the particle manager here.

            // --- Pass 3: Draw Hands In Front of Particles ---
            spriteBatch.Begin(sortMode: SpriteSortMode.Deferred, blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: finalTransform);
            if (_loadedPose != null)
            {
                if (_loadedPose.LeftHand.RenderLayer == RenderLayer.InFrontOfParticles)
                {
                    _leftHand.Draw(spriteBatch, font, gameTime, Color.White);
                }
                if (_loadedPose.RightHand.RenderLayer == RenderLayer.InFrontOfParticles)
                {
                    _rightHand.Draw(spriteBatch, font, gameTime, Color.White);
                }
            }

            if (_isEditMode)
            {
                _transformGizmo.Draw(spriteBatch);
            }
            spriteBatch.End();

            // --- Pass 4: Draw Context Menus on top of everything (using the main screen transform) ---
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            _animationContextMenu.Draw(spriteBatch, font);
            _particleAnchorContextMenu.Draw(spriteBatch, font);
            _particleEffectContextMenu.Draw(spriteBatch, font);
            spriteBatch.End();
        }


        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        private Rectangle GetPreviewAreaBounds()
        {
            var bounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            int previewX = bounds.X + FILE_BROWSER_WIDTH + (PANEL_PADDING * 2);
            int previewY = bounds.Y + PANEL_PADDING;
            int previewWidth = bounds.Width - FILE_BROWSER_WIDTH - (PANEL_PADDING * 3);
            int previewHeight = bounds.Height - CONTROL_PANEL_HEIGHT - (PANEL_PADDING * 3);
            return new Rectangle(previewX, previewY, previewWidth, previewHeight);
        }

        private Rectangle GetControlPanelBounds()
        {
            var previewArea = GetPreviewAreaBounds();
            return new Rectangle(
                previewArea.X,
                previewArea.Bottom + PANEL_PADDING,
                previewArea.Width,
                CONTROL_PANEL_HEIGHT
            );
        }

        private void OnPoseFileSelected(string path, PoseData poseData)
        {
            Debug.WriteLine($"[Editor] Loaded pose: {poseData.Id} from {path}");
            _loadedPosePath = path;
            _loadedPose = poseData;
            _isDataDirty = false;
            _selectedHand = null;
            _transformGizmo.Detach();
            SnapHandsToPose(poseData);
        }

        private void SnapHandsToPose(PoseData pose)
        {
            SnapHandToState(_leftHand, pose.LeftHand);
            SnapHandToState(_rightHand, pose.RightHand);
        }

        private void SnapHandToState(HandRenderer hand, HandState state)
        {
            hand.ForcePositionAndRotation(state.Position, MathHelper.ToRadians(state.Rotation));
            hand.ForceScale(state.Scale);
            if (!string.IsNullOrEmpty(state.AnimationName))
            {
                hand.PlayAnimation(state.AnimationName);
            }
        }

        private void OnSave()
        {
            if (!_isDataDirty || string.IsNullOrEmpty(_loadedPosePath) || _loadedPose == null) return;

            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = {
                        new JsonStringEnumConverter(),
                        new Vector2JsonConverter()
                    }
                };
                string jsonString = JsonSerializer.Serialize(_loadedPose, jsonOptions);
                File.WriteAllText(_loadedPosePath, jsonString);

                Debug.WriteLine($"[Editor] Saved changes to {_loadedPosePath}.");
                _isDataDirty = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Editor] [ERROR] Failed to save file: {ex.Message}");
            }
        }

        private void ShowAnimationMenu(HandRenderer hand)
        {
            if (_loadedPose == null) return;
            var font = ServiceLocator.Get<BitmapFont>();
            var mousePos = Core.TransformMouse(Mouse.GetState().Position);
            var animNames = hand.GetAvailableAnimationNames();

            var menuItems = new List<ContextMenuItem>();
            foreach (var name in animNames.OrderBy(n => n))
            {
                var currentName = name;
                menuItems.Add(new ContextMenuItem
                {
                    Text = currentName,
                    OnClick = () =>
                    {
                        hand.PlayAnimation(currentName);
                        var stateToUpdate = (hand == _leftHand) ? _loadedPose.LeftHand : _loadedPose.RightHand;
                        stateToUpdate.AnimationName = currentName;
                        _isDataDirty = true;
                    }
                });
            }
            _animationContextMenu.Show(mousePos, menuItems, font);
        }

        private void ShowParticleAnchorMenu()
        {
            if (_loadedPose == null) return;
            var font = ServiceLocator.Get<BitmapFont>();
            var mousePos = Core.TransformMouse(Mouse.GetState().Position);
            var anchorTypes = Enum.GetValues(typeof(ParticleAnchorType)).Cast<ParticleAnchorType>();

            var menuItems = new List<ContextMenuItem>();
            foreach (var type in anchorTypes)
            {
                menuItems.Add(new ContextMenuItem
                {
                    Text = type.ToString(),
                    OnClick = () =>
                    {
                        _loadedPose.ParticleAnchor = type;
                        _isDataDirty = true;
                    }
                });
            }
            _particleAnchorContextMenu.Show(mousePos, menuItems, font);
        }

        private void ShowParticleEffectMenu()
        {
            if (_loadedPose == null) return;
            var font = ServiceLocator.Get<BitmapFont>();
            var mousePos = Core.TransformMouse(Mouse.GetState().Position);
            var effectNames = ParticleEffectRegistry.GetEffectNames();

            var menuItems = new List<ContextMenuItem>();
            menuItems.Add(new ContextMenuItem
            {
                Text = "None",
                OnClick = () =>
                {
                    _loadedPose.ParticleEffectName = null;
                    _isDataDirty = true;
                }
            });

            foreach (var name in effectNames)
            {
                menuItems.Add(new ContextMenuItem
                {
                    Text = name,
                    OnClick = () =>
                    {
                        _loadedPose.ParticleEffectName = name;
                        _isDataDirty = true;
                    }
                });
            }
            _particleEffectContextMenu.Show(mousePos, menuItems, font);
        }

        private void ToggleRenderLayer(HandRenderer hand)
        {
            if (_loadedPose == null) return;

            HandState stateToUpdate = (hand == _leftHand) ? _loadedPose.LeftHand : _loadedPose.RightHand;
            stateToUpdate.RenderLayer = (stateToUpdate.RenderLayer == RenderLayer.BehindParticles)
                ? RenderLayer.InFrontOfParticles
                : RenderLayer.BehindParticles;

            _isDataDirty = true;
        }
    }
}
