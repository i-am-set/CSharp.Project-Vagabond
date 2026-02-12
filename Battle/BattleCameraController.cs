using Microsoft.Xna.Framework;
using ProjectVagabond;

namespace ProjectVagabond.Battle
{
    public class BattleCameraController
    {
        private Vector2 _currentPosition;
        private float _currentZoom;

        private Vector2 _targetPosition;
        private float _targetZoom;

        private const float LERP_SPEED = 10.0f;
        private const float FOCUS_INTENSITY = 0.2f;

        public Vector2 Position => _currentPosition;
        public float Zoom => _currentZoom;

        public BattleCameraController()
        {
            // Initialize centered on the screen with default zoom
            _currentPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);
            _targetPosition = _currentPosition;
            _currentZoom = 1.0f;
            _targetZoom = 1.0f;
        }

        public void SetTarget(Vector2 focusPoint, float zoomLevel)
        {
            // Instead of centering directly on the target, we move slightly towards it
            // relative to the screen center. This creates a subtle focus effect.
            Vector2 screenCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);
            _targetPosition = Vector2.Lerp(screenCenter, focusPoint, FOCUS_INTENSITY);
            _targetZoom = zoomLevel;
        }

        public void SnapToTarget()
        {
            _currentPosition = _targetPosition;
            _currentZoom = _targetZoom;
        }

        public void Update(float dt)
        {
            _currentPosition = Vector2.Lerp(_currentPosition, _targetPosition, dt * LERP_SPEED);
            _currentZoom = MathHelper.Lerp(_currentZoom, _targetZoom, dt * LERP_SPEED);
        }

        public Matrix GetTransform()
        {
            // 1. Translate the world so the camera position is at (0,0)
            var translationToTarget = Matrix.CreateTranslation(-_currentPosition.X, -_currentPosition.Y, 0);

            // 2. Scale the world (zoom) around the new origin
            var scale = Matrix.CreateScale(_currentZoom);

            // 3. Translate the origin to the center of the screen
            var translationToScreenCenter = Matrix.CreateTranslation(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f, 0);

            // Combine: Move to Target -> Zoom -> Move to Screen Center
            return translationToTarget * scale * translationToScreenCenter;
        }
    }
}