using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond
{
    public class SpriteManager
    {
        private readonly Core _core;
        private readonly TextureFactory _textureFactory;

        private readonly Dictionary<string, Texture2D> _enemySprites = new(StringComparer.OrdinalIgnoreCase);

        private Texture2D _logoSprite;
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
        private Texture2D _emptySprite;
        private Texture2D _speedMarkSprite;
        private Texture2D _worldMapHoverSelectorSprite;
        private Texture2D _circleTextureSprite;
        private Texture2D _settingsIconSprite;
        private Texture2D _turnIndicatorSprite;
        private Texture2D _handIdleSprite;
        private Texture2D _handHoldSprite;
        private Texture2D _enemySprite;
        private Texture2D _cardBaseSprite;
        private Texture2D _circleParticleSprite;
        private Texture2D _emberParticleSprite;
        private Texture2D _softParticleSprite;
        public Effect CardShaderEffect { get; private set; }
        public Effect FireballParticleShaderEffect { get; private set; }


        public Texture2D LogoSprite => _logoSprite;
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
        public Texture2D EmptySprite => _emptySprite;
        public Texture2D SpeedMarkSprite => _speedMarkSprite;
        public Texture2D WorldMapHoverSelectorSprite => _worldMapHoverSelectorSprite;
        public Texture2D CircleTextureSprite => _circleTextureSprite;
        public Texture2D SettingsIconSprite => _settingsIconSprite;
        public Texture2D TurnIndicatorSprite => _turnIndicatorSprite;
        public Texture2D HandIdleSprite => _handIdleSprite;
        public Texture2D HandHoldSprite => _handHoldSprite;
        public Texture2D EnemySprite => _enemySprite;
        public Texture2D CardBaseSprite => _cardBaseSprite;
        public Texture2D CircleParticleSprite => _circleParticleSprite;
        public Texture2D EmberParticleSprite => _emberParticleSprite;
        public Texture2D SoftParticleSprite => _softParticleSprite;


        public SpriteManager()
        {
            _core = ServiceLocator.Get<Core>();
            _textureFactory = ServiceLocator.Get<TextureFactory>();
        }

        /// <summary>
        /// Loads assets required for the main menu and essential UI elements that are always present.
        /// </summary>
        public void LoadEssentialContent()
        {
            try { _logoSprite = _core.Content.Load<Texture2D>("Sprites/logo"); }
            catch { _logoSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _worldMapHoverSelectorSprite = _core.Content.Load<Texture2D>("Sprites/UI/ui_world_map_selector"); }
            catch { _worldMapHoverSelectorSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _circleTextureSprite = _textureFactory.CreateCircleTexture(); }
            catch { _circleTextureSprite = _textureFactory.CreateColoredTexture(16, 16, Color.Red); }

            try { _settingsIconSprite = _core.Content.Load<Texture2D>("Sprites/UI/ButtonIcons/ui_settings_icon"); }
            catch { _settingsIconSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _turnIndicatorSprite = _core.Content.Load<Texture2D>("Sprites/UI/ui_turn_indicator"); }
            catch { _turnIndicatorSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _circleParticleSprite = _textureFactory.CreateCircleParticleTexture(); }
            catch { _circleParticleSprite = _textureFactory.CreateColoredTexture(4, 4, Color.Red); }

            try { _emberParticleSprite = _core.Content.Load<Texture2D>("Sprites/Particles/ember_particle"); }
            catch { _emberParticleSprite = _textureFactory.CreateColoredTexture(9, 9, Color.Red); }

            try { _softParticleSprite = _textureFactory.CreateSoftCircleParticleTexture(); }
            catch { _softParticleSprite = _textureFactory.CreateColoredTexture(16, 16, Color.Red); }

            // Moved from LoadGameContent because it's used on the main menu
            try { FireballParticleShaderEffect = _core.Content.Load<Effect>("Shaders/FireballParticleShader"); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ERROR] Could not load shader 'Shaders/FireballParticleShader'. Please ensure it's in the Content project. {ex.Message}"); }
        }

        /// <summary>
        /// Loads all assets related to the main game world, combat, and entities.
        /// </summary>
        public void LoadGameContent()
        {
            try { _waterSprite = _core.Content.Load<Texture2D>("Sprites/water"); }
            catch { _waterSprite = _textureFactory.CreateWaterTexture(); }

            try { _flatlandSprite = _core.Content.Load<Texture2D>("Sprites/flatland"); }
            catch { _flatlandSprite = _textureFactory.CreateColoredTexture(8, 8, Color.White); }

            try { _hillSprite = _core.Content.Load<Texture2D>("Sprites/hill"); }
            catch { _hillSprite = _textureFactory.CreateColoredTexture(8, 8, Color.White); }

            try { _mountainSprite = _core.Content.Load<Texture2D>("Sprites/mountain"); }
            catch { _mountainSprite = _textureFactory.CreateColoredTexture(8, 8, Color.White); }

            try { _peakSprite = _core.Content.Load<Texture2D>("Sprites/peak"); }
            catch { _peakSprite = _textureFactory.CreateColoredTexture(8, 8, Color.White); }

            try { _playerSprite = _core.Content.Load<Texture2D>("Sprites/player"); }
            catch { _playerSprite = _textureFactory.CreatePlayerTexture(); }

            try { _pathSprite = _core.Content.Load<Texture2D>("Sprites/path"); }
            catch { _pathSprite = _textureFactory.CreatePathTexture(); }

            try { _runPathSprite = _core.Content.Load<Texture2D>("Sprites/runPathEnd"); }
            catch { _runPathSprite = _textureFactory.CreateRunPathTexture(); }

            try { _pathEndSprite = _core.Content.Load<Texture2D>("Sprites/pathEnd"); }
            catch { _pathEndSprite = _textureFactory.CreatePathEndTexture(); }

            try { _shortRestSprite = _core.Content.Load<Texture2D>("Sprites/shortRest"); }
            catch { _shortRestSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _longRestSprite = _core.Content.Load<Texture2D>("Sprites/longRest"); }
            catch { _longRestSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _emptySprite = _textureFactory.CreateEmptyTexture(); }
            catch { _emptySprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _speedMarkSprite = _core.Content.Load<Texture2D>("Sprites/speedMark"); }
            catch { _speedMarkSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            // Load placeholder combat hand sprites
            try { _handIdleSprite = _core.Content.Load<Texture2D>("Sprites/Combat/hand_idle"); }
            catch { _handIdleSprite = _textureFactory.CreateColoredTexture(128, 256, Color.DarkGray); }

            try { _handHoldSprite = _core.Content.Load<Texture2D>("Sprites/Combat/hand_hold"); }
            catch { _handHoldSprite = _textureFactory.CreateColoredTexture(128, 256, Color.CornflowerBlue); }

            try { _enemySprite = _core.Content.Load<Texture2D>("Sprites/Combat/enemy_placeholder"); }
            catch { _enemySprite = _textureFactory.CreateEnemyPlaceholderTexture(); }

            try { _cardBaseSprite = _core.Content.Load<Texture2D>("Sprites/Cards/card_base"); }
            catch { _cardBaseSprite = _textureFactory.CreateColoredTexture(120, 168, Color.Magenta); }

            try { CardShaderEffect = _core.Content.Load<Effect>("Shaders/CardShader"); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ERROR] Could not load shader 'Shaders/CardShader'. Please ensure it's in the Content project. {ex.Message}"); }

            LoadAllArchetypeSprites();
        }

        /// <summary>
        /// Iterates through all loaded archetypes and pre-loads any sprites specified in their RenderableComponents.
        /// </summary>
        public void LoadAllArchetypeSprites()
        {
            Debug.WriteLine("[SpriteManager] --- Loading Archetype Sprites ---");
            Debug.WriteLine($"[SpriteManager] [DIAGNOSTIC] Content.RootDirectory is: '{_core.Content.RootDirectory}'");
            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            var allArchetypes = archetypeManager.GetAllArchetypeTemplates();

            foreach (var archetype in allArchetypes)
            {
                bool isCombatant = archetype.TemplateComponents.Any(c => c is CombatantComponent);
                bool isPlayer = archetype.TemplateComponents.Any(c => c is PlayerTagComponent);

                // This validation applies to all non-player entities that can participate in combat.
                if (isCombatant && !isPlayer)
                {
                    var renderable = archetype.TemplateComponents.Find(c => c is RenderableComponent) as RenderableComponent;

                    if (renderable == null)
                    {
#if DEBUG
                        throw new Exception($"[SpriteManager] [CRITICAL CONTENT ERROR] Archetype '{archetype.Id}' is a combatant but is missing a RenderableComponent. All non-player combatants must have a RenderableComponent in their JSON file.");
#else
                        Debug.WriteLine($"[SpriteManager] [WARNING] Archetype '{archetype.Id}' is a combatant but is missing a RenderableComponent. It will not be visible.");
                        continue;
#endif
                    }

                    if (string.IsNullOrEmpty(renderable.SpritePath))
                    {
#if DEBUG
                        throw new Exception($"[SpriteManager] [CRITICAL CONTENT ERROR] Archetype '{archetype.Id}' has a RenderableComponent but is missing a 'SpritePath'. Please define a valid sprite path in the archetype's JSON file.");
#else
                        Debug.WriteLine($"[SpriteManager] [WARNING] Archetype '{archetype.Id}' is missing a 'SpritePath'. The game will use a fallback placeholder.");
                        continue;
#endif
                    }

                    Debug.WriteLine($"[SpriteManager] [DIAGNOSTIC] Processing archetype '{archetype.Id}'. Found SpritePath: '{renderable.SpritePath}'.");
                    try
                    {
                        Debug.WriteLine($"[SpriteManager] [DIAGNOSTIC] Calling Content.Load<Texture2D>(\"{renderable.SpritePath}\")");
                        var texture = _core.Content.Load<Texture2D>(renderable.SpritePath);
                        _enemySprites[renderable.SpritePath] = texture;
                        Debug.WriteLine($"[SpriteManager] [DIAGNOSTIC] SUCCESS: Loaded and cached '{renderable.SpritePath}'.");
                    }
                    catch (ContentLoadException ex)
                    {
                        Debug.WriteLine($"[SpriteManager] [DIAGNOSTIC] FAILURE: Content.Load threw an exception for '{renderable.SpritePath}'.");
#if DEBUG
                        throw new Exception($"[SpriteManager] [CRITICAL FAILURE] Failed to load sprite '{renderable.SpritePath}' for archetype '{archetype.Id}'. Ensure the file exists and its 'Build Action' is 'Content' and 'Copy to Output Directory' is 'Copy if newer'.", ex);
#else
                        Debug.WriteLine($"[SpriteManager] [WARNING] Failed to load sprite '{renderable.SpritePath}' for archetype '{archetype.Id}'. The game will use a fallback placeholder. Error: {ex.Message}");
#endif
                    }
                }
            }
            Debug.WriteLine($"[SpriteManager] --- Finished loading archetype sprites. Total loaded: {_enemySprites.Count} ---");
        }


        /// <summary>
        /// Retrieves a pre-loaded enemy sprite from the manager.
        /// </summary>
        /// <param name="spritePath">The content path of the sprite to retrieve.</param>
        /// <returns>The Texture2D if found; otherwise, null.</returns>
        public Texture2D GetEnemySprite(string spritePath)
        {
            if (string.IsNullOrEmpty(spritePath))
            {
                return null;
            }
            Debug.WriteLine($"[SpriteManager] [DIAGNOSTIC] GetEnemySprite requested for path: '{spritePath}'.");
            if (_enemySprites.TryGetValue(spritePath, out var texture))
            {
                Debug.WriteLine($"[SpriteManager] [DIAGNOSTIC] Found texture in cache.");
                return texture;
            }
            else
            {
                Debug.WriteLine($"[SpriteManager] [DIAGNOSTIC] Texture NOT FOUND in cache.");
                return null;
            }
        }
    }
}