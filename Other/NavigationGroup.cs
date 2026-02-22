using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using ProjectVagabond;

namespace ProjectVagabond.UI
{
    public enum NavigationDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public class NavigationGroup
    {
        private readonly List<ISelectable> _items = new List<ISelectable>();
        private int _currentIndex = -1;
        private bool _wrapNavigation = true;

        public Rectangle? ClipRectangle { get; set; }

        public event Action<ISelectable> OnSelectionChanged;

        public ISelectable CurrentSelection => (_currentIndex >= 0 && _currentIndex < _items.Count) ? _items[_currentIndex] : null;

        public NavigationGroup(bool wrapNavigation = true)
        {
            _wrapNavigation = wrapNavigation;
        }

        public void Add(ISelectable item)
        {
            _items.Add(item);
        }

        public void Clear()
        {
            DeselectAll();
            _items.Clear();
        }

        public void DeselectAll()
        {
            if (_currentIndex >= 0 && _currentIndex < _items.Count)
            {
                _items[_currentIndex].IsSelected = false;
                _items[_currentIndex].OnDeselect();
            }
            _currentIndex = -1;
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            if (!_items[index].IsEnabled) return;

            if (_currentIndex >= 0 && _currentIndex < _items.Count)
            {
                if (_currentIndex == index) return;

                _items[_currentIndex].IsSelected = false;
                _items[_currentIndex].OnDeselect();
            }

            _currentIndex = index;
            _items[_currentIndex].IsSelected = true;
            _items[_currentIndex].OnSelect();

            OnSelectionChanged?.Invoke(_items[_currentIndex]);
        }

        public void SelectFirst()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].IsEnabled)
                {
                    Select(i);
                    return;
                }
            }
        }

        public void Navigate(NavigationDirection direction)
        {
            if (_items.Count == 0) return;

            // Fallback: If nothing is selected, select the first enabled item
            if (_currentIndex == -1 || CurrentSelection == null)
            {
                SelectFirst();
                return;
            }

            var current = CurrentSelection;

            // Check for explicit overrides
            ISelectable? explicitNeighbor = null;
            switch (direction)
            {
                case NavigationDirection.Up:
                    explicitNeighbor = current.NeighborUp;
                    break;
                case NavigationDirection.Down:
                    explicitNeighbor = current.NeighborDown;
                    break;
                case NavigationDirection.Left:
                    explicitNeighbor = current.NeighborLeft;
                    break;
                case NavigationDirection.Right:
                    explicitNeighbor = current.NeighborRight;
                    break;
            }

            if (explicitNeighbor != null && explicitNeighbor.IsEnabled)
            {
                int index = _items.IndexOf(explicitNeighbor);
                if (index != -1)
                {
                    Select(index);
                    return;
                }
            }

            var currentCenter = new Vector2(current.Bounds.Center.X, current.Bounds.Center.Y);

            // Define direction vector
            Vector2 dirVector = Vector2.Zero;
            switch (direction)
            {
                case NavigationDirection.Up:
                    dirVector = new Vector2(0, -1);
                    break;
                case NavigationDirection.Down:
                    dirVector = new Vector2(0, 1);
                    break;
                case NavigationDirection.Left:
                    dirVector = new Vector2(-1, 0);
                    break;
                case NavigationDirection.Right:
                    dirVector = new Vector2(1, 0);
                    break;
            }

            ISelectable bestCandidate = null;
            float bestDistSq = float.MaxValue;
            const float coneThreshold = 0.707f; // Approx 45 degrees

            foreach (var item in _items)
            {
                if (item == current || !item.IsEnabled) continue;

                var itemCenter = new Vector2(item.Bounds.Center.X, item.Bounds.Center.Y);
                Vector2 toItem = itemCenter - currentCenter;
                float distSq = toItem.LengthSquared();

                if (distSq < 0.001f) continue;

                Vector2 toItemNorm = Vector2.Normalize(toItem);
                float dot = Vector2.Dot(dirVector, toItemNorm);

                if (dot >= coneThreshold)
                {
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestCandidate = item;
                    }
                }
            }

            if (bestCandidate != null)
            {
                Select(_items.IndexOf(bestCandidate));
            }
            else if (_wrapNavigation)
            {
                // Wrap logic: Find the item furthest in the opposite direction
                bestCandidate = null;
                float bestVal = 0;
                bool first = true;

                foreach (var item in _items)
                {
                    if (!item.IsEnabled) continue;
                    var c = new Vector2(item.Bounds.Center.X, item.Bounds.Center.Y);

                    float val = 0;
                    bool better = false;

                    switch (direction)
                    {
                        case NavigationDirection.Up:
                            // Wrap Up -> Go to bottom (Max Y)
                            val = c.Y;
                            better = first || val > bestVal;
                            break;
                        case NavigationDirection.Down:
                            // Wrap Down -> Go to top (Min Y)
                            val = c.Y;
                            better = first || val < bestVal;
                            break;
                        case NavigationDirection.Left:
                            // Wrap Left -> Go to right (Max X)
                            val = c.X;
                            better = first || val > bestVal;
                            break;
                        case NavigationDirection.Right:
                            // Wrap Right -> Go to left (Min X)
                            val = c.X;
                            better = first || val < bestVal;
                            break;
                    }

                    if (better)
                    {
                        bestVal = val;
                        bestCandidate = item;
                        first = false;
                    }
                    else if (Math.Abs(val - bestVal) < 1.0f)
                    {
                        // Tie-breaker: closest in the other axis to current
                        float dist = Vector2.Distance(c, currentCenter);
                        float currentBestDist = Vector2.Distance(new Vector2(bestCandidate.Bounds.Center.X, bestCandidate.Bounds.Center.Y), currentCenter);
                        if (dist < currentBestDist)
                        {
                            bestCandidate = item;
                        }
                    }
                }

                if (bestCandidate != null && bestCandidate != current)
                {
                    Select(_items.IndexOf(bestCandidate));
                }
            }
        }

        public void Update(InputManager inputManager, MouseState? mouseState = null, bool deselectIfNoHover = false)
        {
            // Strictly ignore mouse logic if the input manager says we are using Keyboard/Gamepad
            if (inputManager.CurrentInputDevice != InputDeviceType.Mouse)
                return;

            // Even if device is Mouse, ignore if no movement occurred this frame (optimization + jitter prevention)
            if (!inputManager.MouseMovedThisFrame)
                return;

            MouseState currentMouseState = mouseState ?? inputManager.GetEffectiveMouseState();

            if (ClipRectangle.HasValue && !ClipRectangle.Value.Contains(currentMouseState.Position))
            {
                if (deselectIfNoHover)
                {
                    DeselectAll();
                }
                return;
            }

            bool foundHover = false;

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].IsEnabled && _items[i].Bounds.Contains(currentMouseState.Position))
                {
                    Select(i);
                    foundHover = true;
                    break;
                }
            }

            if (deselectIfNoHover && !foundHover)
            {
                DeselectAll();
            }
        }

        public void UpdateInput(InputManager input)
        {
            if (input.CurrentInputDevice == InputDeviceType.Mouse) return;

            if (HandleInput(input)) return;

            if (input.NavigateUp) Navigate(NavigationDirection.Up);
            if (input.NavigateDown) Navigate(NavigationDirection.Down);
            if (input.NavigateLeft) Navigate(NavigationDirection.Left);
            if (input.NavigateRight) Navigate(NavigationDirection.Right);

            if (input.Confirm) SubmitCurrent();
        }

        public void SubmitCurrent()
        {
            CurrentSelection?.OnSubmit();
        }

        public bool HandleInput(InputManager input)
        {
            if (CurrentSelection != null && CurrentSelection.IsEnabled)
            {
                return CurrentSelection.HandleInput(input);
            }
            return false;
        }
    }
}