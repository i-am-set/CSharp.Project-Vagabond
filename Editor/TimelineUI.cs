using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Editor
{
    /// <summary>
    /// A UI component for controlling and visualizing animation playback.
    /// </summary>
    public class TimelineUI
    {
        public Rectangle Bounds { get; set; }
        public float CurrentTime { get; set; }

        public event Action OnPlay;
        public event Action OnPause;
        public event Action OnStop;
        public event Action OnReset;
        public event Action<float> OnScrub;
        public event Action OnSetKeyframe;
        public event Action OnSave;
        public event Action<AnimationTrack, Keyframe> OnKeyframeClicked;
        public event Action OnToggleEditMode;


        private AnimationTimeline _timeline;
        private Button _resetButton;
        private Button _playButton;
        private Button _pauseButton;
        private Button _stopButton;
        private Button _setKeyframeButton;
        private Button _saveButton;
        private ToggleButton _editButton;

        private bool _isScrubbing = false;

        // --- Interactive State ---
        private Keyframe _hoveredKeyframe;
        private AnimationTrack _hoveredTrack;
        private Rectangle _scrubberGrabberRect;
        private Keyframe _draggedKeyframe;
        private AnimationTrack _draggedKeyframeTrack;
        private bool _isDraggingKeyframe;


        // --- Layout & Tuning ---
        private const int SUB_TRACK_HEIGHT = 18;
        private const int TRACK_SPACING = 10;
        private const int TRACK_LABEL_WIDTH = 70;
        private const int TIMELINE_START_X_OFFSET = 10;
        private const int TOP_CONTROLS_AREA_HEIGHT = 35;
        private const int KEYFRAME_SIZE = 6;
        private const int SCRUBBER_GRABBER_WIDTH = 10;
        private const int SCRUBBER_GRABBER_HEIGHT = 8;


        private Rectangle _mainTimelineArea;

        // Data structure to define the layout of sub-tracks
        private readonly Dictionary<string, (string Label, int Order)> _subTrackLayout = new()
        {
            { "MoveTo", ("Position", 0) },
            { "RotateTo", ("Rotation", 1) },
            { "ScaleTo", ("Scale", 2) },
            { "PlayAnimation", ("Animation", 3) }
        };

        public TimelineUI()
        {
            _resetButton = new Button(Rectangle.Empty, "[]");
            _resetButton.OnClick += () => OnReset?.Invoke();

            _playButton = new Button(Rectangle.Empty, "Play");
            _playButton.OnClick += () => OnPlay?.Invoke();

            _pauseButton = new Button(Rectangle.Empty, "Pause");
            _pauseButton.OnClick += () => OnPause?.Invoke();

            _stopButton = new Button(Rectangle.Empty, "Stop");
            _stopButton.OnClick += () => OnStop?.Invoke();

            _setKeyframeButton = new Button(Rectangle.Empty, "Set Key");
            _setKeyframeButton.OnClick += () => OnSetKeyframe?.Invoke();

            _saveButton = new Button(Rectangle.Empty, "Save");
            _saveButton.OnClick += () => OnSave?.Invoke();

            _editButton = new ToggleButton(Rectangle.Empty, "Edit");
            _editButton.OnClick += () => OnToggleEditMode?.Invoke();
        }

        public void SetTimeline(AnimationTimeline timeline)
        {
            _timeline = timeline;
            CurrentTime = 0;
        }

        public void Update(GameTime gameTime, MouseState previousMouseState, bool isDirty, bool isEditMode)
        {
            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);

            // The Edit button is always interactive.
            _editButton.IsSelected = isEditMode;
            _editButton.Update(mouseState);

            // Other controls are only interactive if not in edit mode.
            if (!isEditMode)
            {
                _resetButton.Update(mouseState);
                _playButton.Update(mouseState);
                _pauseButton.Update(mouseState);
                _stopButton.Update(mouseState);
                _setKeyframeButton.Update(mouseState);
                _saveButton.IsEnabled = isDirty;
                _saveButton.Update(mouseState);
            }

            bool leftClickPressed = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickHeld = mouseState.LeftButton == ButtonState.Pressed;
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;
            bool rightClickPressed = mouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released;

            // --- Layout Calculation ---
            _mainTimelineArea = new Rectangle(Bounds.X + TIMELINE_START_X_OFFSET, Bounds.Y + TOP_CONTROLS_AREA_HEIGHT, Bounds.Width - (TIMELINE_START_X_OFFSET * 2), Bounds.Height - TOP_CONTROLS_AREA_HEIGHT - 10);
            if (_timeline != null && _timeline.Duration > 0)
            {
                float progress = CurrentTime / _timeline.Duration;
                int scrubberX = _mainTimelineArea.X + TRACK_LABEL_WIDTH + (int)(progress * (_mainTimelineArea.Width - TRACK_LABEL_WIDTH));
                _scrubberGrabberRect = new Rectangle(scrubberX - SCRUBBER_GRABBER_WIDTH / 2, _mainTimelineArea.Y - SCRUBBER_GRABBER_HEIGHT, SCRUBBER_GRABBER_WIDTH, SCRUBBER_GRABBER_HEIGHT);
            }
            else
            {
                _scrubberGrabberRect = Rectangle.Empty;
            }

            // --- Handle Keyframe Dragging ---
            if (_isDraggingKeyframe)
            {
                if (leftClickHeld)
                {
                    float timelinePixelWidth = _mainTimelineArea.Width - TRACK_LABEL_WIDTH;
                    if (timelinePixelWidth > 0)
                    {
                        float newTimeRatio = (virtualMousePos.X - (_mainTimelineArea.X + TRACK_LABEL_WIDTH)) / timelinePixelWidth;
                        float absoluteTime = newTimeRatio * _timeline.Duration;
                        float snappedTime = MathF.Round(absoluteTime * 100f) / 100f;
                        _draggedKeyframe.Time = Math.Clamp(snappedTime / _timeline.Duration, 0f, 1f);

                        if (_draggedKeyframe.State == KeyframeState.Unmodified)
                        {
                            _draggedKeyframe.State = KeyframeState.Added; // Mark as modified
                        }
                        OnScrub?.Invoke(snappedTime);
                    }
                }
                else // Mouse was released
                {
                    _draggedKeyframeTrack.Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
                    _isDraggingKeyframe = false;
                    _draggedKeyframe = null;
                    _draggedKeyframeTrack = null;
                }
            }

            // --- Hover and New Clicks (only if not already dragging a keyframe) ---
            if (!_isDraggingKeyframe)
            {
                _hoveredKeyframe = null;
                _hoveredTrack = null;

                if (_mainTimelineArea.Contains(virtualMousePos) || _scrubberGrabberRect.Contains(virtualMousePos))
                {
                    if (_timeline != null)
                    {
                        float currentY = _mainTimelineArea.Y;
                        foreach (var mainTrack in _timeline.Tracks.OrderBy(t => t.Target))
                        {
                            currentY += SUB_TRACK_HEIGHT;
                            foreach (var subTrackDef in _subTrackLayout.OrderBy(l => l.Value.Order))
                            {
                                var subTrackRect = new Rectangle(_mainTimelineArea.X, (int)currentY, _mainTimelineArea.Width, SUB_TRACK_HEIGHT);
                                if (subTrackRect.Contains(virtualMousePos))
                                {
                                    foreach (var keyframe in mainTrack.Keyframes.Where(k => k.Type == subTrackDef.Key))
                                    {
                                        int keyframeX = _mainTimelineArea.X + TRACK_LABEL_WIDTH + (int)(keyframe.Time * (_mainTimelineArea.Width - TRACK_LABEL_WIDTH));
                                        int keyframeY = subTrackRect.Y + (subTrackRect.Height / 2);
                                        var keyframeHitbox = new Rectangle(keyframeX - KEYFRAME_SIZE, keyframeY - KEYFRAME_SIZE, KEYFRAME_SIZE * 2, KEYFRAME_SIZE * 2);
                                        if (keyframeHitbox.Contains(virtualMousePos))
                                        {
                                            _hoveredKeyframe = keyframe;
                                            _hoveredTrack = mainTrack;
                                            goto HoverFound;
                                        }
                                    }
                                }
                                currentY += SUB_TRACK_HEIGHT;
                            }
                            currentY += TRACK_SPACING;
                        }
                    }
                HoverFound:;

                    if (UIInputManager.CanProcessMouseClick())
                    {
                        if (leftClickPressed)
                        {
                            if (_hoveredKeyframe != null)
                            {
                                _isDraggingKeyframe = true;
                                _draggedKeyframe = _hoveredKeyframe;
                                _draggedKeyframeTrack = _hoveredTrack;
                                UIInputManager.ConsumeMouseClick();
                            }
                            else if (_scrubberGrabberRect.Contains(virtualMousePos))
                            {
                                _isScrubbing = true;
                                UIInputManager.ConsumeMouseClick();
                            }
                        }
                        else if (rightClickPressed)
                        {
                            if (_hoveredKeyframe != null)
                            {
                                OnKeyframeClicked?.Invoke(_hoveredTrack, _hoveredKeyframe);
                                UIInputManager.ConsumeMouseClick();
                            }
                        }
                    }
                }
            }

            if (leftClickReleased)
            {
                _isScrubbing = false;
            }

            if (_isScrubbing && _timeline != null && _timeline.Duration > 0)
            {
                float timelinePixelWidth = _mainTimelineArea.Width - TRACK_LABEL_WIDTH;
                float progress = (virtualMousePos.X - (_mainTimelineArea.X + TRACK_LABEL_WIDTH)) / timelinePixelWidth;
                float newTime = progress * _timeline.Duration;
                OnScrub?.Invoke(newTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, HashSet<int> selectedKeyTracks = null)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var global = ServiceLocator.Get<Global>();

            // Draw panel background and border
            spriteBatch.Draw(pixel, Bounds, global.TerminalBg);
            spriteBatch.Draw(pixel, new Rectangle(Bounds.Left, Bounds.Top, Bounds.Width, 2), global.Palette_White);
            spriteBatch.Draw(pixel, new Rectangle(Bounds.Left, Bounds.Bottom, Bounds.Width + 2, 2), global.Palette_White);
            spriteBatch.Draw(pixel, new Rectangle(Bounds.Left, Bounds.Top, 2, Bounds.Height), global.Palette_White);
            spriteBatch.Draw(pixel, new Rectangle(Bounds.Right, Bounds.Top, 2, Bounds.Height), global.Palette_White);


            // Layout and draw buttons
            int buttonWidth = 60;
            int buttonHeight = 20;
            int buttonY = Bounds.Y + (TOP_CONTROLS_AREA_HEIGHT - buttonHeight) / 2;
            int currentX = Bounds.X + 10;

            _resetButton.Bounds = new Rectangle(currentX, buttonY, buttonHeight, buttonHeight); // Square button
            currentX += buttonHeight + 5;
            _playButton.Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight);
            currentX += buttonWidth + 5;
            _pauseButton.Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight);
            currentX += buttonWidth + 5;
            _stopButton.Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight);
            currentX += buttonWidth + 5;
            _setKeyframeButton.Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight);
            currentX += buttonWidth + 5;
            _editButton.Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight);
            _saveButton.Bounds = new Rectangle(Bounds.Right - buttonWidth - 10, buttonY, buttonWidth, buttonHeight);

            _resetButton.Draw(spriteBatch, font, gameTime);
            _playButton.Draw(spriteBatch, font, gameTime);
            _pauseButton.Draw(spriteBatch, font, gameTime);
            _stopButton.Draw(spriteBatch, font, gameTime);
            _setKeyframeButton.Draw(spriteBatch, font, gameTime);
            _saveButton.Draw(spriteBatch, font, gameTime);
            _editButton.Draw(spriteBatch, font, gameTime);

            // Draw timeline area
            if (_timeline != null && _timeline.Duration > 0)
            {
                float currentY = _mainTimelineArea.Y;
                float timelinePixelWidth = _mainTimelineArea.Width - TRACK_LABEL_WIDTH;
                int trackIndex = 1;

                foreach (var mainTrack in _timeline.Tracks.OrderBy(t => t.Target))
                {
                    spriteBatch.DrawString(font, mainTrack.Target, new Vector2(_mainTimelineArea.X, currentY), global.Palette_White);
                    currentY += SUB_TRACK_HEIGHT;

                    foreach (var subTrackDef in _subTrackLayout.OrderBy(l => l.Value.Order))
                    {
                        var subTrackRect = new Rectangle(_mainTimelineArea.X + TRACK_LABEL_WIDTH, (int)currentY, (int)timelinePixelWidth, SUB_TRACK_HEIGHT);
                        spriteBatch.Draw(pixel, new Rectangle(subTrackRect.X, subTrackRect.Y + subTrackRect.Height / 2, subTrackRect.Width, 1), global.Palette_DarkGray);
                        spriteBatch.DrawString(font, subTrackDef.Value.Label, new Vector2(_mainTimelineArea.X + 5, currentY), global.Palette_LightGray);

                        // Draw the numbered indicator for keyframe selection mode
                        if (selectedKeyTracks != null)
                        {
                            string indexStr = $"{trackIndex}";
                            Vector2 indexPos = new Vector2(_mainTimelineArea.X - 15, currentY);
                            Color indexColor = selectedKeyTracks.Contains(trackIndex) ? global.Palette_Red : global.Palette_Gray;
                            spriteBatch.DrawString(font, indexStr, indexPos, indexColor);
                        }

                        foreach (var keyframe in mainTrack.Keyframes.Where(k => k.Type == subTrackDef.Key))
                        {
                            Color keyColor;
                            if (keyframe == _draggedKeyframe) keyColor = Color.White;
                            else if (keyframe == _hoveredKeyframe) keyColor = global.Palette_LightBlue;
                            else if (keyframe.State == KeyframeState.Added) keyColor = global.Palette_Yellow;
                            else if (keyframe.State == KeyframeState.Deleted) keyColor = global.Palette_Red;
                            else keyColor = global.Palette_LightGray;

                            int keyframeX = subTrackRect.X + (int)(keyframe.Time * subTrackRect.Width);
                            int keyframeY = subTrackRect.Y + (subTrackRect.Height / 2);
                            var keyframeRect = new Rectangle(keyframeX - KEYFRAME_SIZE / 2, keyframeY - KEYFRAME_SIZE / 2, KEYFRAME_SIZE, KEYFRAME_SIZE);
                            spriteBatch.Draw(pixel, keyframeRect, keyColor);
                        }
                        currentY += SUB_TRACK_HEIGHT;
                        trackIndex++;
                    }
                    currentY += TRACK_SPACING;
                }

                // Draw scrubber line
                float progress = CurrentTime / _timeline.Duration;
                int scrubberX = _mainTimelineArea.X + TRACK_LABEL_WIDTH + (int)(progress * timelinePixelWidth);
                var scrubberLineRect = new Rectangle(scrubberX - 1, _mainTimelineArea.Y, 2, _mainTimelineArea.Height);
                spriteBatch.Draw(pixel, scrubberLineRect, global.Palette_Yellow);

                // Draw scrubber grabber
                Color grabberColor = _isScrubbing ? global.Palette_LightYellow : global.Palette_Yellow;
                spriteBatch.Draw(pixel, _scrubberGrabberRect, grabberColor);


                // Draw time text
                string timeText = $"{CurrentTime:F2}s / {_timeline.Duration:F2}s";
                var textSize = font.MeasureString(timeText);
                spriteBatch.DrawString(font, timeText, new Vector2(Bounds.Right - textSize.Width - 15, Bounds.Y + (TOP_CONTROLS_AREA_HEIGHT - textSize.Height) / 2), global.Palette_White);
            }
        }
    }
}