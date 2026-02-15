using Microsoft.Xna.Framework;
using ProjectVagabond;
using System;

namespace ProjectVagabond.Battle
{
    public class BattleCameraController
    {
        private Vector2 _currentPosition;
        private float _currentZoom;

        private Vector2 _targetPosition;
        private float _targetZoom;

        // Kick offsets (Impact physics)
        private Vector2 _kickOffset;
        private float _kickZoom;

        private const float LERP_SPEED = 10.0f;
        private const float FOCUS_INTENSITY = 0.2f;
        private const float KICK_DECAY = 15.0f;

        public Vector2 Position => _currentPosition + _kickOffset;
        public float Zoom => _currentZoom + _kickZoom;

        public BattleCameraController()
        {
            _currentPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);
            _targetPosition = _currentPosition;
            _currentZoom = 1.0f;
            _targetZoom = 1.0f;
        }

        public void SetTarget(Vector2 focusPoint, float zoomLevel)
        {
            Vector2 screenCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);
            _targetPosition = Vector2.Lerp(screenCenter, focusPoint, FOCUS_INTENSITY);
            _targetZoom = zoomLevel;
        }

        public void Kick(Vector2 direction, float intensity)
        {
            _kickOffset = direction * intensity * 4.0f;
            _kickZoom = 0.05f * (intensity / 10f);
        }

        public void SnapToTarget()
        {
            _currentPosition = _targetPosition;
            _currentZoom = _targetZoom;
            _kickOffset = Vector2.Zero;
            _kickZoom = 0f;
        }

        public void Update(float dt)
        {
            _currentPosition = Vector2.Lerp(_currentPosition, _targetPosition, dt * LERP_SPEED);
            _currentZoom = MathHelper.Lerp(_currentZoom, _targetZoom, dt * LERP_SPEED);

            float decay = 1.0f - MathF.Exp(-KICK_DECAY * dt);
            _kickOffset = Vector2.Lerp(_kickOffset, Vector2.Zero, decay);
            _kickZoom = MathHelper.Lerp(_kickZoom, 0f, decay);
        }

        public Matrix GetTransform()
        {
            var pos = Position;
            var zoom = Zoom;

            var translationToTarget = Matrix.CreateTranslation(-pos.X, -pos.Y, 0);
            var scale = Matrix.CreateScale(zoom);
            var translationToScreenCenter = Matrix.CreateTranslation(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f, 0);

            return translationToTarget * scale * translationToScreenCenter;
        }
    }
}
