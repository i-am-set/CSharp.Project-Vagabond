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

        public bool IsHorizontalLayout { get; set; } = false;

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

            int delta = 0;

            if (IsHorizontalLayout)
            {
                if (direction == NavigationDirection.Left) delta = -1;
                if (direction == NavigationDirection.Right) delta = 1;
            }
            else
            {
                if (direction == NavigationDirection.Up) delta = -1;
                if (direction == NavigationDirection.Down) delta = 1;
            }

            if (delta == 0) return;

            if (_currentIndex == -1)
            {
                int wakeUpIndex = -1;
                if (delta > 0)
                {
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (_items[i].IsEnabled) { wakeUpIndex = i; break; }
                    }
                }
                else
                {
                    for (int i = _items.Count - 1; i >= 0; i--)
                    {
                        if (_items[i].IsEnabled) { wakeUpIndex = i; break; }
                    }
                }

                if (wakeUpIndex != -1) Select(wakeUpIndex);
                return;
            }

            int start = _currentIndex;
            int next = start;
            int count = _items.Count;

            for (int i = 0; i < count; i++)
            {
                next += delta;

                if (_wrapNavigation)
                {
                    if (next >= count) next = 0;
                    if (next < 0) next = count - 1;
                }
                else
                {
                    if (next >= count) return;
                    if (next < 0) return;
                }

                if (_items[next].IsEnabled)
                {
                    Select(next);
                    return;
                }
            }
        }

        public void Update(InputManager inputManager, MouseState? mouseState = null, bool deselectIfNoHover = false)
        {
            if (inputManager.CurrentInputDevice != InputDeviceType.Mouse || !inputManager.MouseMovedThisFrame)
                return;

            MouseState currentMouseState = mouseState ?? inputManager.GetEffectiveMouseState();
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