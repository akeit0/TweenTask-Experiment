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

        private readonly Random Rand = new();
        private readonly HashSet<SimpleSpriteObject> spriteObjects = new();
        private readonly HashSet<SimpleSpriteObject> spriteObjectsToDelete = new();

        private ManualTweenRunner? _runner;
        private SpriteBatch _spriteBatch;

        private AudioSource soundFX;

        private bool spacePressed;
        public Texture2D Texture;
        private SpriteFont hudFont;
        private int MoveTweenCount { get; set; }
        private int DeletingCount { get; set; }
        private int TotalCount { get; set; }

        public Game1()
        {
            _graphics = new(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }


        protected override void LoadContent()
        {
            _spriteBatch = new(GraphicsDevice);
            soundFX = new();
            Texture = new(_graphics.GraphicsDevice, 1, 1);
            hudFont = Content.Load<SpriteFont>("Fonts/Hud");
            Texture.SetData([Color.White]);
        }

        static Color HsvToRgb(double h, double s, double v)
        {
            h = h % 360; // Ensure hue is within 0-360
            if (h < 0) h += 360;

            double c = v * s; // Chroma
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double rPrime = 0, gPrime = 0, bPrime = 0;

            if (h < 60)
            {
                rPrime = c;
                gPrime = x;
            }
            else if (h < 120)
            {
                rPrime = x;
                gPrime = c;
            }
            else if (h < 180)
            {
                gPrime = c;
                bPrime = x;
            }
            else if (h < 240)
            {
                gPrime = x;
                bPrime = c;
            }
            else if (h < 300)
            {
                rPrime = x;
                bPrime = c;
            }
            else
            {
                rPrime = c;
                bPrime = x;
            }

            var r = (int)((rPrime + m) * 255);
            var g = (int)((gPrime + m) * 255);
            var b = (int)((bPrime + m) * 255);
            return new Color(r, g, b);
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

            var bounds = Window.ClientBounds;
            var center = new Vector2(bounds.Width / 2f, bounds.Height / 2f);
            if (Keyboard.GetState().IsKeyDown(Keys.Space))
            {
                if (!spacePressed)
                {
                    var newObj = new SimpleSpriteObject(Texture)
                    {
                        Position = center + new Vector2(bounds.Width / 2f, bounds.Height / 2f) *
                            new Vector2(Rand.NextSingle() - 0.5f, Rand.NextSingle() - 0.5f),
                        Size = 10 + 20 * Rand.NextSingle(),
                        Color = HsvToRgb(360 * Rand.NextDouble(), 1, 1)
                    };
                    TotalCount++;
                    MoveTweenCount++;
                    spriteObjects.Add(newObj);

                    TweenTask.Create(newObj.Position,
                            newObj.Position + 200 * new Vector2(Rand.NextSingle() - 0.5f, Rand.NextSingle() - 0.5f),
                            1 + Rand.NextSingle()).WithCancellationToken(newObj.CancellationToken)
                        .WithEase(Ease.InBounce)
                        .WithOnEnd(newObj, (o, result) =>
                        {
                            MoveTweenCount--;
                            switch (result.ResultType)
                            {
                                case TweenResultType.Complete:
                                {
                                    soundFX.PlayWave(440 * MathF.Pow(2, Rand.NextSingle() - 0.5f), 50, WaveType.Square,
                                        0.3f);
                                }
                                    break;
                                case TweenResultType.Cancel:
                                {
                                    soundFX.PlayWave(0, 100, WaveType.Noise,
                                        0.1f);
                                }
                                    break;
                            }
                        }).Bind(newObj, (o, position) => o.Position = position);

                    if (spriteObjects.Count > 10)
                    {
                        var firstObj = spriteObjects.Shuffle().FirstOrDefault(x => !spriteObjectsToDelete.Contains(x));

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
            spriteObjectsToDelete.Add(obj);
            DeletingCount++;
            if (Rand.NextDouble() < 0.5)
            {
                await TweenTask.Create(obj.Size, 0, 0.5).WithEase(Ease.OutCirc)
                    .WithOnEnd(this, (game, result) =>
                    {
                        if (result.ResultType == TweenResultType.Complete)
                        {
                            game.DeletingCount--;
                        }
                        else
                        {
                            Console.WriteLine("Failed to delete");
                        }
                    }).Bind(obj, (o, size) => o.Size = size);
            }
            else
            {
                await TweenTask.Create(obj.Size, 0, 0.5).WithEase(Ease.OutElastic)
                    .Bind(obj, (o, size) => o.Size = size);
                DeletingCount--;
            }

            obj.Dispose();
            spriteObjects.Remove(obj);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            _spriteBatch.Begin();
            foreach (var spriteObject in spriteObjects) spriteObject.Draw(_spriteBatch);
            _spriteBatch.DrawString(hudFont,
                $"Moving: {MoveTweenCount:00}, Deleting: {DeletingCount:00}, Active: {spriteObjects.Count:00}", default,
                Color.White);

            _spriteBatch.End();
            base.Draw(gameTime);
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

        public void Draw(SpriteBatch sb)
        {
            if (cts.IsCancellationRequested) return;

            sb.Draw(Texture, Position - new Vector2(Size / 2), null,
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