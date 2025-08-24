using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.UI;
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
        private const int TIMELINE_HEIGHT = 200;
        private const int PANEL_PADDING = 5;
        private const float HAND_IDLE_X_OFFSET_FROM_CENTER = 116f;
        private const float HAND_IDLE_Y_OFFSET_EDITOR = -80f; // Adjusted for editor view
        private const float HAND_CAST_Y_OFFSET = -30f;
        private const float HAND_THROW_Y_OFFSET = -20f;


        private HandRenderer _leftHand;
        private HandRenderer _rightHand;
        private ActionAnimator _actionAnimator;
        private FileBrowser _fileBrowser;
        private TimelineUI _timelineUI;
        private TransformGizmo _transformGizmo;

        private ActionData _loadedAction;
        private string _loadedActionPath;
        private CombatAction _dummyCombatAction;
        private bool IsDirty => _loadedAction?.Timeline?.Tracks.Any(t => t.Keyframes.Any(k => k.State != KeyframeState.Unmodified)) ?? false;


        // --- Interactive Editing State ---
        private HandRenderer _selectedHand;
        private bool _isEditMode = false;

        public Dictionary<string, Vector2> AnimationAnchors { get; private set; }

        public override bool UsesLetterboxing => true;

        public override void Initialize()
        {
            base.Initialize();

            _leftHand = new HandRenderer(HandType.Left, Vector2.Zero);
            _rightHand = new HandRenderer(HandType.Right, Vector2.Zero);

            // The editor doesn't resolve logical effects, so the ActionResolver is null.
            _actionAnimator = new ActionAnimator(this, null, _leftHand, _rightHand);

            _fileBrowser = new FileBrowser();
            _fileBrowser.OnFileSelected += new Action<string, ActionData>(OnActionFileSelected);

            _timelineUI = new TimelineUI();
            _timelineUI.OnPlay += OnPlaybackPlay;
            _timelineUI.OnPause += OnPlaybackPause;
            _timelineUI.OnStop += OnPlaybackStop;
            _timelineUI.OnScrub += OnPlaybackScrub;
            _timelineUI.OnSetKeyframe += OnSetKeyframe;
            _timelineUI.OnSave += OnSave;
            _timelineUI.OnKeyframeClicked += OnKeyframeClicked;
            _timelineUI.OnToggleEditMode += ToggleEditMode;

            _transformGizmo = new TransformGizmo();
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

            _fileBrowser.Populate("Content/Actions");
        }

        public override void Exit()
        {
            base.Exit();
            EventBus.Unsubscribe<GameEvents.UIThemeOrResolutionChanged>(OnResolutionChanged);
            OnPlaybackStop(); // Ensure animator is stopped
        }

        private void OnResolutionChanged(GameEvents.UIThemeOrResolutionChanged e)
        {
            CalculateLayouts();
            // After a resize, smoothly move the hands to their new idle positions
            _leftHand.MoveTo(AnimationAnchors["LeftHandIdle"], 0.3f, Easing.EaseOutCubic);
            _rightHand.MoveTo(AnimationAnchors["RightHandIdle"], 0.3f, Easing.EaseOutCubic);
        }

        private void CalculateLayouts()
        {
            var bounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

            // File Browser on the left
            _fileBrowser.Bounds = new Rectangle(
                bounds.X + PANEL_PADDING,
                bounds.Y + PANEL_PADDING,
                FILE_BROWSER_WIDTH,
                bounds.Height - TIMELINE_HEIGHT - (PANEL_PADDING * 3)
            );

            // Timeline on the bottom
            _timelineUI.Bounds = new Rectangle(
                bounds.X + PANEL_PADDING,
                bounds.Bottom - TIMELINE_HEIGHT - PANEL_PADDING,
                bounds.Width - (PANEL_PADDING * 2),
                TIMELINE_HEIGHT
            );

            // --- Anchor Calculation (mimicking CombatScene for consistency) ---
            var core = ServiceLocator.Get<Core>();
            var windowBottomRight = new Point(core.GraphicsDevice.PresentationParameters.BackBufferWidth, core.GraphicsDevice.PresentationParameters.BackBufferHeight);
            float screenBottomInVirtualCoords = Core.TransformMouse(windowBottomRight).Y;
            float screenCenterX = Global.VIRTUAL_WIDTH / 2f;

            var leftHandIdle = new Vector2(screenCenterX - HAND_IDLE_X_OFFSET_FROM_CENTER, screenBottomInVirtualCoords + HAND_IDLE_Y_OFFSET_EDITOR);
            var rightHandIdle = new Vector2(screenCenterX + HAND_IDLE_X_OFFSET_FROM_CENTER, screenBottomInVirtualCoords + HAND_IDLE_Y_OFFSET_EDITOR);

            // Cast and Throw positions are relative to the Idle position's Y for consistency
            var leftHandCast = new Vector2(leftHandIdle.X + 60, leftHandIdle.Y + HAND_CAST_Y_OFFSET);
            var rightHandCast = new Vector2(rightHandIdle.X - 60, rightHandIdle.Y + HAND_CAST_Y_OFFSET);
            var leftHandThrow = leftHandCast + new Vector2(0, HAND_THROW_Y_OFFSET);
            var rightHandThrow = rightHandCast + new Vector2(0, HAND_THROW_Y_OFFSET);

            AnimationAnchors = new Dictionary<string, Vector2>
            {
                { "LeftHandIdle", leftHandIdle },
                { "RightHandIdle", rightHandIdle },
                { "LeftHandCast", leftHandCast },
                { "RightHandCast", rightHandCast },
                { "LeftHandRecoil", leftHandCast + new Vector2(-5, 8) },
                { "RightHandRecoil", rightHandCast + new Vector2(5, 8) },
                { "LeftHandThrow", leftHandThrow },
                { "RightHandThrow", rightHandThrow },
                { "LeftHandOffscreen", new Vector2(leftHandIdle.X, screenBottomInVirtualCoords + 100) },
                { "RightHandOffscreen", new Vector2(rightHandIdle.X, screenBottomInVirtualCoords + 100) }
            };

            // Update the renderers with their new initial positions
            _leftHand.SetIdlePosition(AnimationAnchors["LeftHandIdle"]);
            _rightHand.SetIdlePosition(AnimationAnchors["RightHandIdle"]);
            _leftHand.SetOffscreenPosition(AnimationAnchors["LeftHandOffscreen"]);
            _rightHand.SetOffscreenPosition(AnimationAnchors["RightHandOffscreen"]);
        }

        public override void Update(GameTime gameTime)
        {
            var kbs = Keyboard.GetState();
            if (kbs.IsKeyDown(Keys.F12) && _previousKeyboardState.IsKeyUp(Keys.F12))
            {
                // TODO: Add "are you sure you want to exit" prompt
                ServiceLocator.Get<SceneManager>().ChangeScene(GameSceneState.MainMenu);
                return;
            }

            if (kbs.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                ToggleEditMode();
            }

            if (_isEditMode)
            {
                _transformGizmo.Update(Mouse.GetState(), previousMouseState);
                HandleHandSelection();
            }
            else
            {
                // Ensure gizmo is not active when not in edit mode
                if (_selectedHand != null)
                {
                    _selectedHand = null;
                    _transformGizmo.Detach();
                }
            }

            _fileBrowser.Update(gameTime, _isEditMode);
            _timelineUI.Update(gameTime, previousMouseState, IsDirty, _isEditMode);

            _actionAnimator.Update(gameTime);
            _leftHand.Update(gameTime);
            _rightHand.Update(gameTime);

            if (_actionAnimator.IsPlaying)
            {
                _timelineUI.CurrentTime = _actionAnimator.PlaybackTime;
            }

            // Base update must be last to correctly handle previous input state for the next frame.
            base.Update(gameTime);
        }

        private void ToggleEditMode()
        {
            _isEditMode = !_isEditMode;
            if (!_isEditMode)
            {
                _selectedHand = null;
                _transformGizmo.Detach();
            }
        }

        private void HandleHandSelection()
        {
            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);

            bool leftClickPressed = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;

            // If the gizmo is active, it handles all input. Don't allow re-selection.
            if (_transformGizmo.IsDragging) return;

            // Don't allow selection if UI panels are being interacted with, unless the click has already been handled by the gizmo
            if (UIInputManager.CanProcessMouseClick() && (_fileBrowser.Bounds.Contains(virtualMousePos) || _timelineUI.Bounds.Contains(virtualMousePos)))
            {
                if (leftClickPressed)
                {
                    _selectedHand = null;
                    _transformGizmo.Detach();
                }
                return;
            }

            if (leftClickPressed && UIInputManager.CanProcessMouseClick())
            {
                // Prioritize left hand if overlapping
                if (_leftHand.GetInteractionBounds().Contains(virtualMousePos))
                {
                    _selectedHand = _leftHand;
                }
                else if (_rightHand.GetInteractionBounds().Contains(virtualMousePos))
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

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // Batch 1: Main scene content (background, hands, unclipped UI)
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            DrawSceneContent(spriteBatch, font, gameTime);
            _timelineUI.Draw(spriteBatch, font, gameTime);
            spriteBatch.End();

            // Batch 2: File browser with clipping
            _fileBrowser.Draw(spriteBatch, font, gameTime, transform);
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // The "world" content is the preview window background.
            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, GetPreviewAreaBounds(), new Color(20, 20, 25));
        }

        public override void DrawFullscreenUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // --- Hands (drawn last, on top of UI panels and letterboxing) ---
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);

            Color handColor = _isEditMode ? Color.White : Color.White * 0.2f;
            _leftHand.Draw(spriteBatch, font, gameTime, handColor);
            _rightHand.Draw(spriteBatch, font, gameTime, handColor);

            if (_isEditMode)
            {
                _transformGizmo.Draw(spriteBatch);
            }

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
            int previewHeight = bounds.Height - TIMELINE_HEIGHT - (PANEL_PADDING * 3);

            return new Rectangle(previewX, previewY, previewWidth, previewHeight);
        }

        private void OnActionFileSelected(string path, ActionData actionData)
        {
            System.Diagnostics.Debug.WriteLine($"[Editor] Loaded action: {actionData.Name} from {path}");
            _loadedActionPath = path;
            _loadedAction = actionData;

            // Ensure all loaded keyframes are marked as Unmodified
            foreach (var track in _loadedAction.Timeline.Tracks)
            {
                foreach (var keyframe in track.Keyframes)
                {
                    keyframe.State = KeyframeState.Unmodified;
                }
            }

            _dummyCombatAction = new CombatAction(0, _loadedAction, new List<int>());
            _timelineUI.SetTimeline(_loadedAction.Timeline);
            OnPlaybackStop();
        }

        private void OnPlaybackPlay()
        {
            if (_loadedAction == null) return;

            // Force hands to a known state before starting playback to prevent issues from stale positions.
            _leftHand.ForcePositionAndRotation(AnimationAnchors["LeftHandIdle"], 0);
            _rightHand.ForcePositionAndRotation(AnimationAnchors["RightHandIdle"], 0);
            _leftHand.ForceScale(1.0f);
            _rightHand.ForceScale(1.0f);

            if (_actionAnimator.IsPaused)
            {
                _actionAnimator.Resume();
            }
            else
            {
                _actionAnimator.Play(_dummyCombatAction);
            }
        }

        private void OnPlaybackPause()
        {
            _actionAnimator.Pause();
        }

        private void OnPlaybackStop()
        {
            _actionAnimator.Stop();
            _timelineUI.CurrentTime = 0;
            // Reset hands to a visible starting position for the next playback
            _leftHand.MoveTo(AnimationAnchors["LeftHandIdle"], 0.3f, Easing.EaseOutCubic);
            _rightHand.MoveTo(AnimationAnchors["RightHandIdle"], 0.3f, Easing.EaseOutCubic);
            _leftHand.RotateTo(0, 0.3f, Easing.EaseOutCubic);
            _rightHand.RotateTo(0, 0.3f, Easing.EaseOutCubic);
        }

        private void OnPlaybackScrub(float newTime)
        {
            if (_loadedAction == null) return;
            _actionAnimator.Seek(newTime);
            _timelineUI.CurrentTime = newTime;
        }

        private void OnSetKeyframe()
        {
            if (_loadedAction?.Timeline == null) return;

            float time = _timelineUI.CurrentTime / _loadedAction.Timeline.Duration;

            // Set keyframes for both hands based on their current state
            SetKeyframeForHand(_leftHand, "LeftHand", time);
            SetKeyframeForHand(_rightHand, "RightHand", time);

            // Refresh the timeline UI to show the new keyframe markers
            _timelineUI.SetTimeline(_loadedAction.Timeline);
        }

        private void SetKeyframeForHand(HandRenderer hand, string trackName, float time)
        {
            var timeline = _loadedAction.Timeline;
            var track = timeline.Tracks.FirstOrDefault(t => t.Target == trackName);
            if (track == null)
            {
                track = new AnimationTrack { Target = trackName };
                timeline.Tracks.Add(track);
            }

            // Find closest position anchor
            string closestAnchor = FindClosestAnchor(hand.CurrentPosition);

            // Create keyframes
            var moveKey = new Keyframe { Time = time, Type = "MoveTo", Position = closestAnchor, Duration = 0.3f, Easing = "EaseOutCubic" };
            var rotateKey = new Keyframe { Time = time, Type = "RotateTo", Rotation = MathHelper.ToDegrees(hand.CurrentRotation), Duration = 0.3f, Easing = "EaseOutCubic" };
            var scaleKey = new Keyframe { Time = time, Type = "ScaleTo", Scale = hand.CurrentScale, Duration = 0.3f, Easing = "EaseOutCubic" };

            // Add or update them in the track
            track.AddOrUpdateKeyframe(moveKey);
            track.AddOrUpdateKeyframe(rotateKey);
            track.AddOrUpdateKeyframe(scaleKey);
        }

        private string FindClosestAnchor(Vector2 position)
        {
            if (AnimationAnchors == null || !AnimationAnchors.Any())
            {
                return "LeftHandIdle"; // Failsafe
            }

            return AnimationAnchors.OrderBy(kvp => Vector2.DistanceSquared(kvp.Value, position)).First().Key;
        }

        private void OnKeyframeClicked(AnimationTrack track, Keyframe keyframe)
        {
            if (keyframe.State == KeyframeState.Added)
            {
                // If it's a newly added keyframe, remove it completely.
                track.Keyframes.Remove(keyframe);
            }
            else if (keyframe.State == KeyframeState.Deleted)
            {
                // If it's already marked for deletion, restore it.
                keyframe.State = KeyframeState.Unmodified;
            }
            else
            {
                // Otherwise, mark it for deletion.
                keyframe.State = KeyframeState.Deleted;
            }
            // Immediately update the preview to reflect the change
            _actionAnimator.Seek(_timelineUI.CurrentTime);
        }

        private void OnSave()
        {
            if (!IsDirty || string.IsNullOrEmpty(_loadedActionPath)) return;

            // 1. Create a clean copy of the ActionData for serialization.
            var dataToSave = new ActionData
            {
                Id = _loadedAction.Id,
                Name = _loadedAction.Name,
                Priority = _loadedAction.Priority,
                TargetType = _loadedAction.TargetType,
                Effects = _loadedAction.Effects,
                Timeline = new AnimationTimeline { Duration = _loadedAction.Timeline.Duration }
            };

            // 2. Copy only the non-deleted keyframes to the new timeline.
            foreach (var track in _loadedAction.Timeline.Tracks)
            {
                var newTrack = new AnimationTrack { Target = track.Target };
                newTrack.Keyframes.AddRange(track.Keyframes.Where(k => k.State != KeyframeState.Deleted));
                if (newTrack.Keyframes.Any())
                {
                    dataToSave.Timeline.Tracks.Add(newTrack);
                }
            }

            // 3. Serialize and save the clean data.
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };
                string jsonString = JsonSerializer.Serialize(dataToSave, jsonOptions);
                File.WriteAllText(_loadedActionPath, jsonString);
                Debug.WriteLine($"[Editor] Saved changes to {_loadedActionPath}");

                // 4. Post-save cleanup: apply changes to the in-memory _loadedAction.
                foreach (var track in _loadedAction.Timeline.Tracks)
                {
                    // Remove keyframes that were marked for deletion.
                    track.Keyframes.RemoveAll(k => k.State == KeyframeState.Deleted);
                    // Reset the state of all remaining keyframes to Unmodified.
                    foreach (var keyframe in track.Keyframes)
                    {
                        keyframe.State = KeyframeState.Unmodified;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Editor] [ERROR] Failed to save file: {ex.Message}");
                // Optionally, show an error message to the user.
            }
        }
    }
}