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
        private const int TIMELINE_HEIGHT = 240;
        private const int PANEL_PADDING = 5;
        private const float HAND_IDLE_X_OFFSET_FROM_CENTER = 116f;
        private const float HAND_IDLE_Y_OFFSET_EDITOR = -80f; // Adjusted for editor view
        private const float HAND_CAST_Y_OFFSET = -30f;
        private const float HAND_THROW_Y_OFFSET = -20f;
        private const float TIMELINE_KEY_SCRUB_AMOUNT = 0.01f;

        private enum EditorSubMode { Normal, AwaitingKeySelection }

        private HandRenderer _leftHand;
        private HandRenderer _rightHand;
        private ActionAnimator _actionAnimator;
        private FileBrowser _fileBrowser;
        private TimelineUI _timelineUI;
        private TransformGizmo _transformGizmo;
        private ContextMenu _animationContextMenu;

        private ActionData _loadedAction;
        private string _loadedActionPath;
        private CombatAction _dummyCombatAction;
        private bool IsDirty => _loadedAction?.Timeline?.Tracks.Any(t => t.Keyframes.Any(k => k.State != KeyframeState.Unmodified)) ?? false;


        // --- Interactive Editing State ---
        private HandRenderer _selectedHand;
        private bool _isEditMode = false;
        private EditorSubMode _subMode = EditorSubMode.Normal;
        private readonly HashSet<int> _selectedKeyTracks = new HashSet<int>();

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
            _timelineUI.OnReset += OnPlaybackReset;
            _timelineUI.OnScrub += OnPlaybackScrub;
            _timelineUI.OnSetKeyframe += ToggleSetKeyMode; // Changed from OnSetKeyframe
            _timelineUI.OnSave += OnSave;
            _timelineUI.OnKeyframeClicked += OnKeyframeClicked;
            _timelineUI.OnToggleEditMode += ToggleEditMode;

            _transformGizmo = new TransformGizmo();
            _transformGizmo.OnAnimationGizmoClicked += ShowAnimationMenu;

            _animationContextMenu = new ContextMenu();
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
            _transformGizmo.OnAnimationGizmoClicked -= ShowAnimationMenu;
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
            var ms = Mouse.GetState();
            var vms = Core.TransformMouse(ms.Position);
            var font = ServiceLocator.Get<BitmapFont>();

            if (kbs.IsKeyDown(Keys.F12) && _previousKeyboardState.IsKeyUp(Keys.F12))
            {
                // TODO: Add "are you sure you want to exit" prompt
                ServiceLocator.Get<SceneManager>().ChangeScene(GameSceneState.MainMenu);
                return;
            }

            if (kbs.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                if (_subMode == EditorSubMode.AwaitingKeySelection)
                {
                    _subMode = EditorSubMode.Normal; // Cancel key selection
                }
                else
                {
                    ToggleEditMode();
                }
            }

            // Handle keyboard scrubbing in both modes
            if (_loadedAction?.Timeline != null)
            {
                bool leftPressed = (kbs.IsKeyDown(Keys.Left) && _previousKeyboardState.IsKeyUp(Keys.Left)) || (kbs.IsKeyDown(Keys.A) && _previousKeyboardState.IsKeyUp(Keys.A));
                bool rightPressed = (kbs.IsKeyDown(Keys.Right) && _previousKeyboardState.IsKeyUp(Keys.Right)) || (kbs.IsKeyDown(Keys.D) && _previousKeyboardState.IsKeyUp(Keys.D));

                if (leftPressed || rightPressed)
                {
                    float increment = rightPressed ? TIMELINE_KEY_SCRUB_AMOUNT : -TIMELINE_KEY_SCRUB_AMOUNT;
                    float newTime = _timelineUI.CurrentTime + increment;
                    OnPlaybackScrub(newTime);
                }
            }

            _animationContextMenu.Update(ms, previousMouseState, vms, font);

            if (_isEditMode)
            {
                if (_subMode == EditorSubMode.Normal)
                {
                    _transformGizmo.Update(ms, previousMouseState);
                    HandleHandSelection();
                }
                else if (_subMode == EditorSubMode.AwaitingKeySelection)
                {
                    HandleKeySelectionInput(kbs);
                }

                if (kbs.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space))
                {
                    ToggleSetKeyMode();
                }
            }
            else
            {
                // Ensure gizmo is not active when not in edit mode
                if (_selectedHand != null)
                {
                    _selectedHand = null;
                    _transformGizmo.Detach();
                }

                // Handle playback controls when not editing
                if (kbs.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space))
                {
                    if (_actionAnimator.IsPlaying)
                    {
                        if (_actionAnimator.IsPaused) { OnPlaybackPlay(); }
                        else { OnPlaybackPause(); }
                    }
                    else
                    {
                        OnPlaybackPlay();
                    }
                }
            }

            _fileBrowser.Update(gameTime, _isEditMode);
            _timelineUI.Update(gameTime, previousMouseState, IsDirty, _isEditMode);

            // Only update the animator if we are NOT in edit mode.
            // This prevents it from overriding the user's manual transforms.
            if (!_isEditMode)
            {
                _actionAnimator.Update(gameTime);
            }

            _leftHand.Update(gameTime);
            _rightHand.Update(gameTime);

            // Only sync the UI time from the animator when not in edit mode.
            // In edit mode, the UI is the source of truth for the current time.
            if (_actionAnimator.IsPlaying && !_isEditMode)
            {
                _timelineUI.CurrentTime = _actionAnimator.PlaybackTime;
            }

            // Base update must be last to correctly handle previous input states for the next frame.
            base.Update(gameTime);
        }

        private void ToggleEditMode()
        {
            _isEditMode = !_isEditMode;
            if (!_isEditMode)
            {
                _selectedHand = null;
                _transformGizmo.Detach();
                // When exiting edit mode, snap the hands back to their correct timeline position.
                OnPlaybackScrub(_timelineUI.CurrentTime);
            }
        }

        private void HandleHandSelection()
        {
            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);

            bool leftClickPressed = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;

            // If the gizmo is active and being dragged, it handles all input.
            // This prevents re-selection while manipulating a handle.
            if (_transformGizmo.IsDragging) return;

            // When a click occurs, check for selection on a hand. If no hand is clicked,
            // deselect the current one. This logic now correctly ignores UI panels in edit mode.
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
            _timelineUI.Draw(spriteBatch, font, gameTime, _subMode == EditorSubMode.AwaitingKeySelection ? _selectedKeyTracks : null);
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

            Color handColor = _isEditMode ? Color.White * 0.8f : Color.White * 0.2f;
            _leftHand.Draw(spriteBatch, font, gameTime, handColor);
            _rightHand.Draw(spriteBatch, font, gameTime, handColor);

            if (_isEditMode)
            {
                _transformGizmo.Draw(spriteBatch);
            }

            _animationContextMenu.Draw(spriteBatch, font);

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

            // If it's already playing but paused, just resume.
            if (_actionAnimator.IsPlaying && _actionAnimator.IsPaused)
            {
                _actionAnimator.Resume();
            }
            else // If it's not playing at all (i.e., it was stopped), start from the beginning.
            {
                _leftHand.ForcePositionAndRotation(AnimationAnchors["LeftHandIdle"], 0);
                _rightHand.ForcePositionAndRotation(AnimationAnchors["RightHandIdle"], 0);
                _leftHand.ForceScale(1.0f);
                _rightHand.ForceScale(1.0f);
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

        private void OnPlaybackReset()
        {
            if (_loadedAction == null) return;
            // Prime the animator if it's not already playing/paused
            if (!_actionAnimator.IsPlaying)
            {
                _actionAnimator.Play(_dummyCombatAction);
                _actionAnimator.Pause();
            }
            _actionAnimator.Seek(0f);
            _timelineUI.CurrentTime = 0f;
        }

        private void OnPlaybackScrub(float newTime)
        {
            if (_loadedAction == null) return;

            // Clamp and snap the time to the nearest 0.01s increment.
            float duration = _loadedAction.Timeline.Duration;
            float snappedTime = MathF.Round(Math.Clamp(newTime, 0, duration) * 100f) / 100f;

            // Always update the UI's time.
            _timelineUI.CurrentTime = snappedTime;

            // Only update the animation preview if not in edit mode.
            if (!_isEditMode)
            {
                // Prime the animator if it's not already playing/paused
                if (!_actionAnimator.IsPlaying)
                {
                    _actionAnimator.Play(_dummyCombatAction);
                    _actionAnimator.Pause();
                }
                _actionAnimator.Seek(snappedTime);
            }
        }

        private void ToggleSetKeyMode()
        {
            if (_subMode == EditorSubMode.Normal)
            {
                _subMode = EditorSubMode.AwaitingKeySelection;
                _selectedKeyTracks.Clear();
            }
            else // Was AwaitingKeySelection
            {
                PlaceSelectedKeyframes();
                _subMode = EditorSubMode.Normal;
            }
        }

        private void PlaceSelectedKeyframes()
        {
            if (_loadedAction?.Timeline == null || !_selectedKeyTracks.Any()) return;

            float time = _timelineUI.CurrentTime / _loadedAction.Timeline.Duration;

            var trackMap = new Dictionary<int, (HandRenderer hand, string trackName, string property)>
            {
                { 1, (_leftHand, "LeftHand", "MoveTo") },
                { 2, (_leftHand, "LeftHand", "RotateTo") },
                { 3, (_leftHand, "LeftHand", "ScaleTo") },
                { 4, (_leftHand, "LeftHand", "PlayAnimation") },
                { 5, (_rightHand, "RightHand", "MoveTo") },
                { 6, (_rightHand, "RightHand", "RotateTo") },
                { 7, (_rightHand, "RightHand", "ScaleTo") },
                { 8, (_rightHand, "RightHand", "PlayAnimation") }
            };

            foreach (int trackIndex in _selectedKeyTracks)
            {
                if (trackMap.TryGetValue(trackIndex, out var info))
                {
                    SetKeyframeForHandProperty(info.hand, info.trackName, time, info.property);
                }
            }

            _timelineUI.SetTimeline(_loadedAction.Timeline); // Refresh UI
        }

        private void SetKeyframeForHandProperty(HandRenderer hand, string trackName, float time, string property)
        {
            var timeline = _loadedAction.Timeline;
            var track = timeline.Tracks.FirstOrDefault(t => t.Target == trackName);
            if (track == null)
            {
                track = new AnimationTrack { Target = trackName };
                timeline.Tracks.Add(track);
            }

            Keyframe newKey = null;
            switch (property)
            {
                case "MoveTo":
                    string closestAnchor = FindClosestAnchor(hand.CurrentPosition);
                    newKey = new Keyframe { Time = time, Type = "MoveTo", Position = closestAnchor, Easing = "EaseOutCubic" };
                    break;
                case "RotateTo":
                    newKey = new Keyframe { Time = time, Type = "RotateTo", Rotation = MathHelper.ToDegrees(hand.CurrentRotation), Easing = "EaseOutCubic" };
                    break;
                case "ScaleTo":
                    newKey = new Keyframe { Time = time, Type = "ScaleTo", Scale = hand.CurrentScale, Easing = "EaseOutCubic" };
                    break;
                case "PlayAnimation":
                    // This type is set differently, perhaps via a popup in the future. For now, it's a placeholder.
                    // newKey = new Keyframe { Time = time, Type = "PlayAnimation", AnimationName = "idle_loop" };
                    break;
            }

            if (newKey != null)
            {
                track.AddOrUpdateKeyframe(newKey);
            }
        }

        private void HandleKeySelectionInput(KeyboardState kbs)
        {
            var keyMap = new Dictionary<Keys, int>
            {
                { Keys.D1, 1 }, { Keys.D2, 2 }, { Keys.D3, 3 }, { Keys.D4, 4 },
                { Keys.D5, 5 }, { Keys.D6, 6 }, { Keys.D7, 7 }, { Keys.D8, 8 }
            };

            foreach (var pair in keyMap)
            {
                if (kbs.IsKeyDown(pair.Key) && _previousKeyboardState.IsKeyUp(pair.Key))
                {
                    if (_selectedKeyTracks.Contains(pair.Value))
                    {
                        _selectedKeyTracks.Remove(pair.Value);
                    }
                    else
                    {
                        _selectedKeyTracks.Add(pair.Value);
                    }
                }
            }

            if (kbs.IsKeyDown(Keys.Tab) && _previousKeyboardState.IsKeyUp(Keys.Tab))
            {
                // If not all tracks are selected, select all. Otherwise, clear selection.
                if (_selectedKeyTracks.Count < 8)
                {
                    for (int i = 1; i <= 8; i++) _selectedKeyTracks.Add(i);
                }
                else
                {
                    _selectedKeyTracks.Clear();
                }
            }
        }

        private string FindClosestAnchor(Vector2 position)
        {
            if (AnimationAnchors == null || !AnimationAnchors.Any())
            {
                return "LeftHandIdle";
            }

            return AnimationAnchors.OrderBy(kvp => Vector2.DistanceSquared(kvp.Value, position)).First().Key;
        }

        private void OnKeyframeClicked(AnimationTrack track, Keyframe keyframe)
        {
            if (keyframe.State == KeyframeState.Added)
            {
                track.Keyframes.Remove(keyframe);
            }
            else if (keyframe.State == KeyframeState.Deleted)
            {
                keyframe.State = KeyframeState.Unmodified;
            }
            else
            {
                keyframe.State = KeyframeState.Deleted;
            }
            // Immediately update the preview to reflect the change
            _actionAnimator.Seek(_timelineUI.CurrentTime);
        }

        private void OnSave()
        {
            if (!IsDirty || string.IsNullOrEmpty(_loadedActionPath)) return;

            var dataToSave = new ActionData
            {
                Id = _loadedAction.Id,
                Name = _loadedAction.Name,
                Priority = _loadedAction.Priority,
                TargetType = _loadedAction.TargetType,
                Effects = _loadedAction.Effects,
                Timeline = new AnimationTimeline { Duration = _loadedAction.Timeline.Duration }
            };

            foreach (var track in _loadedAction.Timeline.Tracks)
            {
                var newTrack = new AnimationTrack { Target = track.Target };
                newTrack.Keyframes.AddRange(track.Keyframes.Where(k => k.State != KeyframeState.Deleted));
                if (newTrack.Keyframes.Any())
                {
                    dataToSave.Timeline.Tracks.Add(newTrack);
                }
            }

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

                foreach (var track in _loadedAction.Timeline.Tracks)
                {
                    track.Keyframes.RemoveAll(k => k.State == KeyframeState.Deleted);
                    foreach (var keyframe in track.Keyframes)
                    {
                        keyframe.State = KeyframeState.Unmodified;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Editor] [ERROR] Failed to save file: {ex.Message}");
            }
        }

        private void ShowAnimationMenu(HandRenderer hand)
        {
            var font = ServiceLocator.Get<BitmapFont>();
            var mousePos = Core.TransformMouse(Mouse.GetState().Position);
            var animNames = hand.GetAvailableAnimationNames();

            var menuItems = new List<ContextMenuItem>();
            foreach (var name in animNames.OrderBy(n => n))
            {
                // Capture the loop variable for the lambda
                var currentName = name;
                menuItems.Add(new ContextMenuItem
                {
                    Text = currentName,
                    OnClick = () => hand.PlayAnimation(currentName)
                });
            }

            _animationContextMenu.Show(mousePos, menuItems, font);
        }
    }
}