using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
namespace ProjectVagabond
{
    public class SpriteManager
    {
        private Texture2D _waterSprite;
        private Texture2D _flatlandSprite;
        private Texture2D _hillSprite;
        private Texture2D _mountainSprite;
        private Texture2D _peakSprite;
        private Texture2D _playerSprite;
        private Texture2D _pathSprite;
        private Texture2D _pathEndSprite;

        public Texture2D WaterSprite => _waterSprite;
        public Texture2D FlatlandSprite => _flatlandSprite;
        public Texture2D HillSprite => _hillSprite;
        public Texture2D MountainSprite => _mountainSprite;
        public Texture2D PeakSprite => _peakSprite;
        public Texture2D PlayerSprite => _playerSprite;
        public Texture2D PathSprite => _pathSprite;
        public Texture2D PathEndSprite => _pathEndSprite;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void LoadSpriteContent()
        {
            try
            {
                _waterSprite = Core.Instance.Content.Load<Texture2D>("Sprites/water");
            }
            catch
            {
                _waterSprite = Core.CurrentTextureFactory.CreateWaterTexture();
            }

            try
            {
                _flatlandSprite = Core.Instance.Content.Load<Texture2D>("Sprites/flatland");
            }
            catch
            {
                _flatlandSprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.Green);
            }

            try
            {
                _hillSprite = Core.Instance.Content.Load<Texture2D>("Sprites/hill");
            }
            catch
            {
                _hillSprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.DarkGray);
            }

            try
            {
                _mountainSprite = Core.Instance.Content.Load<Texture2D>("Sprites/mountain");
            }
            catch
            {
                _mountainSprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.Gray);
            }

            try
            {
                _peakSprite = Core.Instance.Content.Load<Texture2D>("Sprites/peak");
            }
            catch
            {
                _peakSprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.White);
            }

            try
            {
                _playerSprite = Core.Instance.Content.Load<Texture2D>("Sprites/player");
            }
            catch
            {
                _playerSprite = Core.CurrentTextureFactory.CreatePlayerTexture();
            }

            try
            {
                _pathSprite = Core.Instance.Content.Load<Texture2D>("Sprites/path");
            }
            catch
            {
                _pathSprite = Core.CurrentTextureFactory.CreatePathTexture();
            }

            try
            {
                _pathEndSprite = Core.Instance.Content.Load<Texture2D>("Sprites/pathEnd");
            }
            catch
            {
                _pathEndSprite = Core.CurrentTextureFactory.CreatePathEndTexture();
            }
        }
    }
}
