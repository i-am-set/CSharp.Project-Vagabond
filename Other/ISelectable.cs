using Microsoft.Xna.Framework;

namespace ProjectVagabond.UI
{
    public interface ISelectable
    {
        bool IsSelected { get; set; }
        bool IsEnabled { get; set; }
        Rectangle Bounds { get; }

        void OnSelect();
        void OnDeselect();
        void OnSubmit();
        bool HandleInput(InputManager input);
    }
}