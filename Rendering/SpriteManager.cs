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
        private Texture2D _runPathSprite;
        private Texture2D _pathEndSprite;
        private Texture2D _shortRestSprite;
        private Texture2D _longRestSprite;
        private Texture2D _warningMarkSprite;
        private Texture2D _emptySprite;
        private Texture2D _speedMarkSprite;

        public Texture2D WaterSprite => _waterSprite;
        public Texture2D FlatlandSprite => _flatlandSprite;
        public Texture2D HillSprite => _hillSprite;
        public Texture2D MountainSprite => _mountainSprite;
        public Texture2D PeakSprite => _peakSprite;
        public Texture2D PlayerSprite => _playerSprite;
        public Texture2D PathSprite => _pathSprite;
        public Texture2D RunPathSprite => _runPathSprite;
        public Texture2D PathEndSprite => _pathEndSprite;
        public Texture2D ShortRestSprite => _shortRestSprite;
        public Texture2D LongRestSprite => _longRestSprite;
        public Texture2D WarningMarkSprite => _warningMarkSprite;
        public Texture2D EmptySprite => _emptySprite;
        public Texture2D SpeedMarkSprite => _speedMarkSprite;

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
                _flatlandSprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.White);
            }

            try
            {
                _hillSprite = Core.Instance.Content.Load<Texture2D>("Sprites/hill");
            }
            catch
            {
                _hillSprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.White);
            }

            try
            {
                _mountainSprite = Core.Instance.Content.Load<Texture2D>("Sprites/mountain");
            }
            catch
            {
                _mountainSprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.White);
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
                _runPathSprite = Core.Instance.Content.Load<Texture2D>("Sprites/runPathEnd");
            }
            catch (Exception)
            {

                _runPathSprite = Core.CurrentTextureFactory.CreateRunPathTexture();
            }

            try
            {
                _pathEndSprite = Core.Instance.Content.Load<Texture2D>("Sprites/pathEnd");
            }
            catch
            {
                _pathEndSprite = Core.CurrentTextureFactory.CreatePathEndTexture();
            }

            try
            {
                _shortRestSprite = Core.Instance.Content.Load<Texture2D>("Sprites/shortRest");
            }
            catch (Exception)
            {

                _shortRestSprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.Red);
            }

            try
            {
                _longRestSprite = Core.Instance.Content.Load<Texture2D>("Sprites/longRest");
            }
            catch (Exception)
            {

                _longRestSprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.Red);
            }

            try
            {
                _warningMarkSprite = Core.Instance.Content.Load<Texture2D>("Sprites/warningMark");
            }
            catch (Exception)
            {

                _warningMarkSprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.Red);
            }

            try
            {
                _emptySprite = Core.CurrentTextureFactory.CreateEmptyTexture();
            }
            catch (Exception)
            {

                _emptySprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.Red);
            }

            try
            {
                _speedMarkSprite = Core.Instance.Content.Load<Texture2D>("Sprites/speedMark");
            }
            catch (Exception)
            {
                _speedMarkSprite = Core.CurrentTextureFactory.CreateColoredTexture(8, 8, Color.Red);
            }
        }
    }
}
