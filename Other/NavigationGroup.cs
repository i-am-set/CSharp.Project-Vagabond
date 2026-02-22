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
            var currentCenter = new Vector2(current.Bounds.Center.X, current.Bounds.Center.Y);

            ISelectable bestCandidate = null;
            float bestDistance = float.MaxValue;

            foreach (var item in _items)
            {
                if (item == current || !item.IsEnabled) continue;

                var itemCenter = new Vector2(item.Bounds.Center.X, item.Bounds.Center.Y);
                bool isCandidate = false;

                // Filter candidates based on direction relative to current center
                switch (direction)
                {
                    case NavigationDirection.Up:
                        isCandidate = itemCenter.Y < currentCenter.Y;
                        break;
                    case NavigationDirection.Down:
                        isCandidate = itemCenter.Y > currentCenter.Y;
                        break;
                    case NavigationDirection.Left:
                        isCandidate = itemCenter.X < currentCenter.X;
                        break;
                    case NavigationDirection.Right:
                        isCandidate = itemCenter.X > currentCenter.X;
                        break;
                }

                if (isCandidate)
                {
                    float distance = Vector2.Distance(currentCenter, itemCenter);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
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
            if (inputManager.CurrentInputDevice != InputDeviceType.Mouse || !inputManager.MouseMovedThisFrame)
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