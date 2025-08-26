using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Graphics;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Editor
{
    public class TransformGizmo
    {
        private enum DragHandle { None, Body, TopLeft, TopRight, BottomLeft, BottomRight, TopRotation, BottomRotation, Animation }
        // --- Tuning ---
        private const int HANDLE_SIZE = 8;
        private const int ROTATION_HANDLE_OFFSET = 20;
        private const int ANIMATION_HANDLE_OFFSET = 35;

        private HandRenderer _attachedHand;
        private DragHandle _activeHandle = DragHandle.None;
        public bool IsDragging => _activeHandle != DragHandle.None;

        // Drag state
        private Vector2 _dragStartMousePos;
        private Vector2 _dragStartPosition;
        private float _dragStartRotation;
        private float _dragStartScale;
        private Vector2 _dragStartPivot;
        private float _dragStartDistance;

        // Cached state for drawing
        private readonly Dictionary<DragHandle, Rectangle> _handleRects = new();
        private Vector2[] _worldCorners = new Vector2[4];
        private bool _hasValidStateForDrawing = false;

        public event Action<HandRenderer> OnAnimationGizmoClicked;

        public void Attach(HandRenderer hand)
        {
            _attachedHand = hand;
            // Immediately update the state to prevent drawing with stale data for one frame
            UpdateHandlePositions();
        }

        public void Detach()
        {
            _attachedHand = null;
            _activeHandle = DragHandle.None;
            _hasValidStateForDrawing = false;
        }

        public void Update(MouseState mouse, MouseState prevMouse)
        {
            if (_attachedHand == null)
            {
                _hasValidStateForDrawing = false;
                return;
            }

            UpdateHandlePositions();

            var virtualMousePos = Core.TransformMouse(mouse.Position);
            bool leftClickPressed = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            bool leftClickReleased = mouse.LeftButton == ButtonState.Released;

            if (leftClickPressed && UIInputManager.CanProcessMouseClick())
            {
                // Check handles first, as they are on top
                foreach (var handle in _handleRects)
                {
                    if (handle.Value.Contains(virtualMousePos))
                    {
                        if (handle.Key == DragHandle.Animation)
                        {
                            OnAnimationGizmoClicked?.Invoke(_attachedHand);
                            UIInputManager.ConsumeMouseClick();
                            return;
                        }

                        _activeHandle = handle.Key;
                        _dragStartMousePos = virtualMousePos;
                        _dragStartPosition = _attachedHand.CurrentPosition;
                        _dragStartRotation = _attachedHand.CurrentRotation;
                        _dragStartScale = _attachedHand.CurrentScale;
                        _dragStartPivot = _attachedHand.GetPivotPoint();
                        _dragStartDistance = Vector2.Distance(_dragStartPivot, virtualMousePos);
                        UIInputManager.ConsumeMouseClick();
                        return;
                    }
                }
            }

            if (leftClickReleased)
            {
                _activeHandle = DragHandle.None;
            }

            if (_activeHandle != DragHandle.None)
            {
                ProcessDrag(virtualMousePos);
                // After dragging, the positions have changed, so we need to update the handles for the next frame's draw call.
                UpdateHandlePositions();
            }
        }

        private void ProcessDrag(Vector2 mousePos)
        {
            Vector2 delta = mousePos - _dragStartMousePos;

            switch (_activeHandle)
            {
                case DragHandle.Body:
                    _attachedHand.ForcePositionAndRotation(_dragStartPosition + delta, _dragStartRotation);
                    break;

                case DragHandle.TopRotation:
                case DragHandle.BottomRotation:
                    float startAngle = (float)Math.Atan2(_dragStartMousePos.Y - _dragStartPivot.Y, _dragStartMousePos.X - _dragStartPivot.X);
                    float currentAngle = (float)Math.Atan2(mousePos.Y - _dragStartPivot.Y, mousePos.X - _dragStartPivot.X);
                    _attachedHand.ForcePositionAndRotation(_dragStartPosition, _dragStartRotation + (currentAngle - startAngle));
                    break;

                case DragHandle.TopLeft:
                case DragHandle.TopRight:
                case DragHandle.BottomLeft:
                case DragHandle.BottomRight:
                    float currentDistance = Vector2.Distance(_dragStartPivot, mousePos);
                    if (_dragStartDistance > 1)
                    {
                        float scaleFactor = currentDistance / _dragStartDistance;
                        _attachedHand.ForceScale(_dragStartScale * scaleFactor);
                    }
                    break;
            }
        }

        private void UpdateHandlePositions()
        {
            _handleRects.Clear();
            _hasValidStateForDrawing = false;
            if (_attachedHand == null) return;

            _worldCorners = _attachedHand.GetWorldCorners();
            var center = _attachedHand.GetPivotPoint();
            var halfHandle = HANDLE_SIZE / 2;

            _handleRects[DragHandle.Body] = _attachedHand.GetInteractionBounds();
            _handleRects[DragHandle.TopLeft] = new Rectangle((int)_worldCorners[0].X - halfHandle, (int)_worldCorners[0].Y - halfHandle, HANDLE_SIZE, HANDLE_SIZE);
            _handleRects[DragHandle.TopRight] = new Rectangle((int)_worldCorners[1].X - halfHandle, (int)_worldCorners[1].Y - halfHandle, HANDLE_SIZE, HANDLE_SIZE);
            _handleRects[DragHandle.BottomRight] = new Rectangle((int)_worldCorners[2].X - halfHandle, (int)_worldCorners[2].Y - halfHandle, HANDLE_SIZE, HANDLE_SIZE);
            _handleRects[DragHandle.BottomLeft] = new Rectangle((int)_worldCorners[3].X - halfHandle, (int)_worldCorners[3].Y - halfHandle, HANDLE_SIZE, HANDLE_SIZE);

            // Calculate rotation and animation handle positions
            Vector2 up = new Vector2((float)Math.Sin(_attachedHand.CurrentRotation), -(float)Math.Cos(_attachedHand.CurrentRotation));
            Vector2 topCenter = (_worldCorners[0] + _worldCorners[1]) / 2f;

            _handleRects[DragHandle.TopRotation] = new Rectangle((int)(topCenter.X + up.X * ROTATION_HANDLE_OFFSET) - halfHandle, (int)(topCenter.Y + up.Y * ROTATION_HANDLE_OFFSET) - halfHandle, HANDLE_SIZE, HANDLE_SIZE);
            _handleRects[DragHandle.BottomRotation] = new Rectangle((int)center.X - halfHandle, (int)center.Y - halfHandle, HANDLE_SIZE, HANDLE_SIZE);
            _handleRects[DragHandle.Animation] = new Rectangle((int)(topCenter.X + up.X * ANIMATION_HANDLE_OFFSET) - halfHandle, (int)(topCenter.Y + up.Y * ANIMATION_HANDLE_OFFSET) - halfHandle, HANDLE_SIZE, HANDLE_SIZE);

            _hasValidStateForDrawing = true;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!_hasValidStateForDrawing) return;

            var global = ServiceLocator.Get<Global>();
            var corners = _worldCorners; // Use the cached corners

            // Draw bounding box
            spriteBatch.DrawLine(corners[0], corners[1], global.Palette_White, 1f);
            spriteBatch.DrawLine(corners[1], corners[2], global.Palette_White, 1f);
            spriteBatch.DrawLine(corners[2], corners[3], global.Palette_White, 1f);
            spriteBatch.DrawLine(corners[3], corners[0], global.Palette_White, 1f);

            // Draw handles
            foreach (var handle in _handleRects)
            {
                if (handle.Key != DragHandle.Body)
                {
                    Color handleColor = (handle.Key == DragHandle.Animation) ? global.Palette_Teal : global.Palette_White;
                    spriteBatch.DrawRectangle(handle.Value, handleColor, 1f);
                }
            }

            // Draw lines to top handles
            var topCenter = (corners[0] + corners[1]) / 2f;
            if (_handleRects.TryGetValue(DragHandle.TopRotation, out var topRotationRect))
            {
                spriteBatch.DrawLine(topCenter, topRotationRect.Center.ToVector2(), global.Palette_White, 1f);
            }
            if (_handleRects.TryGetValue(DragHandle.Animation, out var animRect))
            {
                spriteBatch.DrawLine(topRotationRect.Center.ToVector2(), animRect.Center.ToVector2(), global.Palette_Teal, 1f);
            }
        }
    }
}