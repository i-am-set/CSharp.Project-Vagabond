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
        private enum DragHandle { None, Body, TopLeft, TopRight, BottomLeft, BottomRight, TopRotation, BottomRotation }

        // --- Tuning ---
        private const int HANDLE_SIZE = 8;
        private const int ROTATION_HANDLE_OFFSET = 20;

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

        private readonly Dictionary<DragHandle, Rectangle> _handleRects = new();

        public void Attach(HandRenderer hand)
        {
            _attachedHand = hand;
        }

        public void Detach()
        {
            _attachedHand = null;
            _activeHandle = DragHandle.None;
        }

        public void Update(MouseState mouse, MouseState prevMouse)
        {
            if (_attachedHand == null) return;

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
            if (_attachedHand == null) return;

            var corners = _attachedHand.GetWorldCorners();
            var center = _attachedHand.GetPivotPoint();
            var halfHandle = HANDLE_SIZE / 2;

            _handleRects[DragHandle.Body] = _attachedHand.GetInteractionBounds();
            _handleRects[DragHandle.TopLeft] = new Rectangle((int)corners[0].X - halfHandle, (int)corners[0].Y - halfHandle, HANDLE_SIZE, HANDLE_SIZE);
            _handleRects[DragHandle.TopRight] = new Rectangle((int)corners[1].X - halfHandle, (int)corners[1].Y - halfHandle, HANDLE_SIZE, HANDLE_SIZE);
            _handleRects[DragHandle.BottomRight] = new Rectangle((int)corners[2].X - halfHandle, (int)corners[2].Y - halfHandle, HANDLE_SIZE, HANDLE_SIZE);
            _handleRects[DragHandle.BottomLeft] = new Rectangle((int)corners[3].X - halfHandle, (int)corners[3].Y - halfHandle, HANDLE_SIZE, HANDLE_SIZE);

            // Calculate rotation handle positions
            Vector2 up = new Vector2((float)Math.Sin(_attachedHand.CurrentRotation), -(float)Math.Cos(_attachedHand.CurrentRotation));
            Vector2 topCenter = (corners[0] + corners[1]) / 2f;

            _handleRects[DragHandle.TopRotation] = new Rectangle((int)(topCenter.X + up.X * ROTATION_HANDLE_OFFSET) - halfHandle, (int)(topCenter.Y + up.Y * ROTATION_HANDLE_OFFSET) - halfHandle, HANDLE_SIZE, HANDLE_SIZE);
            _handleRects[DragHandle.BottomRotation] = new Rectangle((int)center.X - halfHandle, (int)center.Y - halfHandle, HANDLE_SIZE, HANDLE_SIZE);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_attachedHand == null) return;

            var global = ServiceLocator.Get<Global>();
            var corners = _attachedHand.GetWorldCorners();

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
                    spriteBatch.DrawRectangle(handle.Value, global.Palette_White, 1f);
                }
            }

            // Draw line to top rotation handle
            var topCenter = (corners[0] + corners[1]) / 2f;
            spriteBatch.DrawLine(topCenter, _handleRects[DragHandle.TopRotation].Center.ToVector2(), global.Palette_White, 1f);
        }
    }
}