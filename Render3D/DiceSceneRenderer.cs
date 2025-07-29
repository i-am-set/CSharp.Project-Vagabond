using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using BepuVector3 = System.Numerics.Vector3;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// Handles all 3D rendering for the dice scene, including camera, models, and the render target.
    /// </summary>
    public class DiceSceneRenderer
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Global _global;

        // Rendering Resources
        private RenderTarget2D _renderTarget;
        private Model _d6Model;
        private Model _d4Model;

        // Shared Debug Resources
        private BasicEffect _debugEffect;
        private VertexPositionColor[] _debugAxisVertices;

        // Camera
        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }

        public RenderTarget2D RenderTarget => _renderTarget;

        public DiceSceneRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
        }

        public void Initialize(ContentManager content)
        {
            _renderTarget = new RenderTarget2D(
                _graphicsDevice,
                Global.VIRTUAL_WIDTH,
                Global.VIRTUAL_HEIGHT,
                false,
                _graphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);

            // Load models and apply textures
            _d6Model = content.Load<Model>("Models/die");
            var d6Texture = content.Load<Texture2D>("Textures/die_texture");
            ApplyTextureToModel(_d6Model, d6Texture);

            _d4Model = content.Load<Model>("Models/die_d4");
            var d4Texture = content.Load<Texture2D>("Textures/die_d4_texture");
            ApplyTextureToModel(_d4Model, d4Texture);

            // Create shared debug resources
            _debugEffect = new BasicEffect(_graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
                TextureEnabled = false
            };
            float debugAxisSize = _global.DiceDebugAxisLineSize;
            _debugAxisVertices = new[]
            {
                new VertexPositionColor(new XnaVector3(-debugAxisSize, 0, 0), Color.Red),
                new VertexPositionColor(new XnaVector3(debugAxisSize, 0, 0), Color.Red),
                new VertexPositionColor(new XnaVector3(0, -debugAxisSize, 0), Color.Green),
                new VertexPositionColor(new XnaVector3(0, debugAxisSize, 0), Color.Green),
                new VertexPositionColor(new XnaVector3(0, 0, -debugAxisSize), Color.Blue),
                new VertexPositionColor(new XnaVector3(0, 0, debugAxisSize), Color.Blue)
            };
        }

        private void ApplyTextureToModel(Model model, Texture2D texture)
        {
            foreach (var mesh in model.Meshes)
            {
                foreach (var part in mesh.MeshParts)
                {
                    if (part.Effect is BasicEffect effect)
                    {
                        effect.Texture = texture;
                    }
                }
            }
        }

        public Model GetModelForDieType(DieType dieType)
        {
            return dieType == DieType.D4 ? _d4Model : _d6Model;
        }

        public List<BepuVector3> GetVerticesForModel(DieType dieType)
        {
            var model = dieType == DieType.D4 ? _d4Model : _d6Model;
            var uniqueVertices = new HashSet<XnaVector3>();
            foreach (var mesh in model.Meshes)
            {
                foreach (var part in mesh.MeshParts)
                {
                    var vertices = new VertexPositionNormalTexture[part.NumVertices];
                    part.VertexBuffer.GetData(part.VertexOffset * part.VertexBuffer.VertexDeclaration.VertexStride, vertices, 0, part.NumVertices, part.VertexBuffer.VertexDeclaration.VertexStride);
                    foreach (var vertex in vertices)
                    {
                        uniqueVertices.Add(vertex.Position);
                    }
                }
            }
            return uniqueVertices.Select(v => new BepuVector3(v.X, v.Y, v.Z)).ToList();
        }

        public (float viewWidth, float viewHeight) UpdateCamera(int totalDice)
        {
            float requiredZoom = totalDice <= 8 ? 20f : (totalDice <= 20 ? 30f : 40f);
            float aspectRatio = (float)_renderTarget.Width / _renderTarget.Height;
            float viewHeight = requiredZoom;
            float viewWidth = viewHeight * aspectRatio;

            float physicsWorldWidth = 40f * aspectRatio;
            float physicsWorldHeight = 40f;

            var cameraPosition = new XnaVector3(physicsWorldWidth / 2f, _global.DiceCameraHeight, physicsWorldHeight / 2f);
            var cameraTarget = new XnaVector3(physicsWorldWidth / 2f, 0, physicsWorldHeight / 2f);
            View = Matrix.CreateLookAt(cameraPosition, cameraTarget, XnaVector3.Forward);
            Projection = Matrix.CreateOrthographic(viewWidth, viewHeight, 1f, 200f);

            return (viewWidth, viewHeight);
        }

        public void BeginDraw()
        {
            _graphicsDevice.SetRenderTarget(_renderTarget);
            _graphicsDevice.Clear(Color.Transparent);
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.BlendState = BlendState.Opaque;
        }

        public void DrawDiceScene(IEnumerable<RenderableDie> diceToDraw, Dictionary<RenderableDie, List<BepuVector3>> colliderVertices, bool showDebugColliders)
        {
            foreach (var die in diceToDraw)
            {
                die.Draw(View, Projection);
            }

            if (showDebugColliders)
            {
                var originalDepthState = _graphicsDevice.DepthStencilState;
                _graphicsDevice.DepthStencilState = DepthStencilState.None;

                foreach (var die in diceToDraw)
                {
                    if (colliderVertices.TryGetValue(die, out var vertices))
                    {
                        die.DrawDebug(View, Projection, _debugEffect, _debugAxisVertices, vertices);
                    }
                }

                _graphicsDevice.DepthStencilState = originalDepthState;
            }
        }

        public void EndDraw()
        {
            _graphicsDevice.SetRenderTarget(null);
        }
    }
}