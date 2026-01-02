using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TweenTasks;

namespace MonoGameSample
{
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager _graphics;

        private ManualTweenRunner? _runner;
        private SpriteBatch _spriteBatch;

        private readonly Random Rand = new();

        private bool spacePressed;

        private readonly HashSet<SimpleSpriteObject> spriteObjects = new();
        public Texture2D Texture;

        public Game1()
        {
            _graphics = new(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new(GraphicsDevice);

            Texture = new(_graphics.GraphicsDevice, 1, 1);
            Texture.SetData(new[] { Color.White });
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (_runner == null)
            {
                _runner = new(gameTime.TotalGameTime.TotalSeconds);
                ITweenRunner.Default = _runner;
            }
            else
            {
                _runner.Run(gameTime.TotalGameTime.TotalSeconds);
            }


            if (Keyboard.GetState().IsKeyDown(Keys.Space))
            {
                if (!spacePressed)
                {
                    var newObj = new SimpleSpriteObject(Texture)
                    {
                        Position = new(500 * Rand.NextSingle(), 500 * Rand.NextSingle()),
                        Size = 10 + 20 * Rand.NextSingle()
                    };

                    spriteObjects.Add(newObj);

                    TweenTask.Create(newObj.Position,
                            newObj.Position + new Vector2(300 * Rand.NextSingle() - 150, 300 * Rand.NextSingle() - 150),
                            1 + Rand.NextSingle()).WithCancellationToken(newObj.CancellationToken)
                        .WithEase(Ease.InBounce).Bind(newObj, (o, position) => o.Position = position)
                        .WithOnComplete(newObj, (o, result) =>
                        {
                            switch (result)
                            {
                                case TweenResultType.Complete:
                                {
                                    o.Position -= new Vector2(o.Size, o.Size) / 2;
                                    o.Size *= 2;
                                }
                                    break;
                                case TweenResultType.Cancel:
                                {
                                    Console.WriteLine("Cancelled");
                                }
                                    break;
                            }
                        });

                    if (spriteObjects.Count > 10)
                    {
                        var firstObj = spriteObjects.Shuffle().FirstOrDefault(x => !x.IsMarkedDisposed);

                        if (firstObj != null) Delete(firstObj);
                    }
                }

                spacePressed = true;
            }
            else
            {
                spacePressed = false;
            }

            base.Update(gameTime);
        }

        private async ValueTask Delete(SimpleSpriteObject obj)
        {
            obj.MarkForDelete();
            await TweenTask.Create(obj.Size, 0, 0.5).WithEase(Ease.OutCirc).Bind(obj, (o, size) => o.Size = size);
            obj.Dispose();
            spriteObjects.Remove(obj);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            _spriteBatch.Begin();
            foreach (var spriteObject in spriteObjects) spriteObject.Draw(_spriteBatch);

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        private void DrawRectangle(SpriteBatch sb, Rectangle rec, Color color)
        {
            var pos = new Vector2(rec.X, rec.Y);
            sb.Draw(Texture, pos, rec,
                color * 1.0f,
                0, Vector2.Zero, 1.0f,
                SpriteEffects.None, 0.00001f);
        }

        private void DrawRectangle(SpriteBatch sb, Vector2 position, float scale, Color color)
        {
            sb.Draw(Texture, position, null,
                color * 1.0f,
                0, Vector2.Zero, scale,
                SpriteEffects.None, 0.00001f);
        }
    }

    public class SimpleSpriteObject(Texture2D texture) : IDisposable
    {
        private readonly CancellationTokenSource cts = new();
        public Texture2D Texture { get; } = texture;

        public Vector2 Position
        {
            get
            {
                if (cts.IsCancellationRequested) Console.WriteLine("This Object is Disposed");

                return field;
            }
            set
            {
                if (cts.IsCancellationRequested) Console.WriteLine("This Object is Disposed");

                field = value;
            }
        }

        public float Size { get; set; } = 1;
        public Color Color { get; set; } = Color.White;
        public bool IsMarkedDisposed { get; private set; }

        public CancellationToken CancellationToken => cts.Token;

        public void Dispose()
        {
            try
            {
                cts.Cancel();
            }
            catch (Exception e)
            {
                Console.WriteLine(e + "\n" + new StackTrace());
            }

            cts.Dispose();
        }

        public void MarkForDelete()
        {
            IsMarkedDisposed = true;
        }

        public void Draw(SpriteBatch sb)
        {
            if (cts.IsCancellationRequested) return;

            sb.Draw(Texture, Position, null,
                Color,
                0, Vector2.Zero, Size,
                SpriteEffects.None, 0.00001f);
        }
    }

    public static class Vector2Tween
    {
        extension(TweenTask)
        {
            public static TweenBuilder<Vector2, Vector2TweenAdapter> Create(Vector2 start, Vector2 end, double duration)
            {
                return TweenBuilder<Vector2, Vector2TweenAdapter>.Create(new(start, end), duration);
            }
        }
    }

    public readonly record struct Vector2TweenAdapter(Vector2 From, Vector2 To) : ITweenAdapter<Vector2>
    {
        public Vector2 Evaluate(double progress)
        {
            return Vector2.Lerp(From, To, (float)progress);
        }

        public void Dispose()
        {
        }
    }
}