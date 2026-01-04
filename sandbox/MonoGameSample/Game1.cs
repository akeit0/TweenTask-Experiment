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

namespace MonoGameSample;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager graphics;

    private readonly Random rand = new();
    private readonly HashSet<SimpleSpriteObject> spriteObjects = new();
    private readonly HashSet<SimpleSpriteObject> spriteObjectsToDelete = new();

    private ManualTweenRunner? runner;
    private SpriteBatch spriteBatch;

    private AudioSource soundFx;

    private bool spacePressed;
    private bool jKeyPressed;
    public Texture2D Texture;
    private SpriteFont hudFont;
    private int MoveTweenCount { get; set; }
    private int DeletingCount { get; set; }
    private int TotalCount { get; set; }

    SimpleSpriteObject? seqObject;
    private TweenTask seqTask;
    private Vector2[] pathPoints;
    Spline2D spline;

    public Game1()
    {
        graphics = new(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    bool[] lastKeysPressed = new bool[256];
    bool[] keysPressed = new bool[256];
    Keys[] pressedKeys = new Keys[256];

    public void UpdateKeyStates()
    {
        (keysPressed, lastKeysPressed) = (lastKeysPressed, keysPressed);
        keysPressed.AsSpan().Clear();
        pressedKeys.AsSpan().Clear();
        Keyboard.GetState().GetPressedKeys(pressedKeys);
        foreach (var key in pressedKeys)
        {
            if (key != 0)
            {
                keysPressed[(int)key] = true;
            }
        }
    }

    public bool IsKeyPressedSinceLastFrame(Keys key)
    {
        return keysPressed[(int)key] && !lastKeysPressed[(int)key];
    }

    protected override void LoadContent()
    {
        spriteBatch = new(GraphicsDevice);
        soundFx = new();
        Texture = new(graphics.GraphicsDevice, 1, 1);
        hudFont = Content.Load<SpriteFont>("Fonts/Hud");
        Texture.SetData([Color.White]);
    }

    protected override void BeginRun()
    {
        if (runner == null)
        {
            runner = new(0);
            ITweenRunner.Default = runner;
        }

        var bounds = Window.ClientBounds;
        var center = new Vector2(bounds.Width / 2f, bounds.Height / 2f);
        pathPoints =
        [
            center,
            center + new Vector2(100, 0),
            center + new Vector2(150, 100),
            center + new Vector2(-100, 100),
            center + new Vector2(-50, 00),
            center
        ];
        spline = new Spline2D(pathPoints);
        CreateSeq();
        base.BeginRun();
    }


    void CreateSeq()
    {
        var bounds = Window.ClientBounds;
        var center = new Vector2(bounds.Width / 2f, bounds.Height / 2f);
        seqObject = new SimpleSpriteObject(Texture)
        {
            Position = center,
            Size = 50,
            Color = Color.Yellow
        };
        TotalCount++;


        seqTask = TweenSequence.Create()
            .Append(seqObject.TweenPositionTo(new Vector2(100, 0), 0.5)
                .WithRelative().WithLoop(2,LoopType.Incremental).WithEase(Ease.OutBounce))
            .Append(seqObject.TweenPositionTo(new Vector2(0, 100), 0.5)
                .WithRelative().WithLoop(3,LoopType.Yoyo).WithEase(Ease.InCirc))
            .Append(seqObject.TweenPositionTo(new Vector2(-100, 0), 0.5)
                .WithRelative().WithLoop(3,LoopType.Flip).WithEase(Ease.InCirc))
            .Join(seqObject.TweenRotationTo(0, 0.5))
            .Append(seqObject.TweenPositionTo(center, 0.5))
            .Join(seqObject.TweenRotationTo(-1 * MathF.PI, 1))
            .Append(TweenTask
                .CreatePath(pathPoints, 1)
                .Bind(seqObject, ((o, v) => o.Position = v)))
            .Append(seqObject.TweenRotationTo(MathF.PI, 0.5).WithRelative())
            .Append(TweenTask
                .CreatePath(spline, 1)
                .Bind(seqObject, ((o, v) => o.Position = v)))
            .Join(seqObject.TweenRotationTo(-MathF.PI, 0.5).WithRelative())
            .Insert(0, seqObject.TweenRotationTo(-MathF.PI, 0.5).WithRelative())
            .Schedule(seqObject.CancellationToken);
        seqTask.IsPreserved = true;
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
        return new(r, g, b);
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();
        UpdateKeyStates();
        runner!.Run(gameTime.TotalGameTime.TotalSeconds);

        var bounds = Window.ClientBounds;
        var center = new Vector2(bounds.Width / 2f, bounds.Height / 2f);
        if (Keyboard.GetState().IsKeyDown(Keys.Space))
        {
            if (!spacePressed)
            {
                var newObj = new SimpleSpriteObject(Texture)
                {
                    Position = center + new Vector2(bounds.Width / 2f, bounds.Height / 2f) *
                        new Vector2(rand.NextSingle() - 0.5f, rand.NextSingle() - 0.5f),
                    Size = 10 + 20 * rand.NextSingle(),
                    Color = HsvToRgb(360 * rand.NextDouble(), 1, 1)
                };
                TotalCount++;
                MoveTweenCount++;
                spriteObjects.Add(newObj);

                newObj.TweenPositionTo(200 * new Vector2(rand.NextSingle() - 0.5f, rand.NextSingle() - 0.5f),
                        1 + rand.NextSingle())
                    .WithRelative()
                    .WithEase(Ease.InBounce)
                    .WithCancellationToken(newObj.CancellationToken)
                    .WithOnEnd(this, static (o, result) =>
                    {
                        o.MoveTweenCount--;
                        switch (result.ResultType)
                        {
                            case TweenResultType.Complete:
                            {
                                o.soundFx.PlayWave(440 * MathF.Pow(2, o.rand.NextSingle() - 0.5f), 50, WaveType.Square,
                                    0.3f);
                            }
                                break;
                            case TweenResultType.Cancel:
                            {
                                o.soundFx.PlayWave(0, 100, WaveType.Noise,
                                    0.1f);
                            }
                                break;
                        }
                    }).Schedule();

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

        if (Keyboard.GetState().IsKeyDown(Keys.J))
        {
            if (!jKeyPressed)
            {
                var newObj = new SimpleSpriteObject(Texture)
                {
                    Position = center + new Vector2(bounds.Width / 2f, bounds.Height / 2f) *
                        new Vector2(rand.NextSingle() - 0.5f, rand.NextSingle() - 0.5f),
                    Size = 10 + 20 * rand.NextSingle(),
                    Color = HsvToRgb(360 * rand.NextDouble(), 1, 1)
                };
                TotalCount++;
                MoveTweenCount++;
                spriteObjects.Add(newObj);

                TweenSequence.Create()
                    .Append(newObj.TweenRotationTo(MathF.PI * 2, 1))
                    .Append(newObj.TweenPositionTo(new Vector2(100, 0), 1).WithRelative())
                    .Append(newObj.TweenPositionTo(new Vector2(0, 100), 1).WithRelative())
                    .WithOnEnd(this,
                        static (o, result) =>
                        {
                            o.MoveTweenCount--;
                            switch (result.ResultType)
                            {
                                case TweenResultType.Complete:
                                {
                                    o.soundFx.PlayWave(440 * MathF.Pow(2, o.rand.NextSingle() - 0.5f), 50,
                                        WaveType.Square,
                                        0.3f);
                                }
                                    break;
                                case TweenResultType.Cancel:
                                {
                                    o.soundFx.PlayWave(0, 100, WaveType.Noise,
                                        0.1f);
                                }
                                    break;
                            }
                        }).Schedule(newObj.CancellationToken);

                if (spriteObjects.Count > 10)
                {
                    var firstObj = spriteObjects.Shuffle().FirstOrDefault(x => !spriteObjectsToDelete.Contains(x));

                    if (firstObj != null) Delete(firstObj);
                }
            }

            jKeyPressed = true;
        }
        else
        {
            jKeyPressed = false;
        }

        if (Keyboard.GetState().IsKeyDown(Keys.Right))
        {
            seqTask.SetPlaybackSpeed(1);
        }
        else if (Keyboard.GetState().IsKeyDown(Keys.Left))
        {
            seqTask.SetPlaybackSpeed(-1);
        }


        if (IsKeyPressedSinceLastFrame(Keys.P))
        {
            if (seqObject != null)
            {
                seqTask.IsPreserved = false;
                seqTask.TryCancel();
                seqTask = default;
                seqObject.Dispose();
                seqObject = null;
            }
            else
            {
                CreateSeq();
            }
        }

        base.Update(gameTime);
    }


    private async void Delete(SimpleSpriteObject obj)
    {
        try
        {
            spriteObjectsToDelete.Add(obj);
            DeletingCount++;
            if (rand.NextDouble() < 0.5)
            {
                obj.TweenRotationTo(MathF.PI * 4, 2).WithEase(Ease.InOutCubic).Run();
                await obj.TweenSizeTo(0, 2).WithEase(Ease.Linear)
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
                    }).Schedule();
            }
            else
            {
                obj.TweenRotationTo(-MathF.PI * 4, 1.5).WithEase(Ease.Linear).Schedule().Forget();
                await TweenTask.Create(obj.Size, 0, 2)
                    .Bind(obj, static (o, size) => o.Size = size).WithEase(Ease.Linear)
                    .Schedule();
                DeletingCount--;
            }

            obj.Dispose();
            spriteObjects.Remove(obj);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        spriteBatch.Begin();
        seqObject?.Draw(spriteBatch);
        foreach (var spriteObject in spriteObjects) spriteObject.Draw(spriteBatch);
        if (seqObject != null)
        {
            spriteBatch.DrawString(hudFont,
                $"SeqTime: {seqTask.Time:f1}", new Vector2(0, 0),
                Color.White);
        }

        spriteBatch.DrawString(hudFont,
            $"Moving: {MoveTweenCount:00}, Deleting: {DeletingCount:00}, Active: {spriteObjects.Count:00}",
            new Vector2(0, 50),
            Color.White);

        spriteBatch.End();
        base.Draw(gameTime);
    }
}

public static class TweenExtensions
{
    extension(SimpleSpriteObject obj)
    {
        public TweenBuilder<Vector2, Vector2TweenAdapter> TweenPositionTo(Vector2 position,
            double duration)
        {
            return TweenBuilder
                .CreateToEntry<Vector2, Vector2TweenAdapter>(new(position), duration)
                .Bind(obj, static (obj) => obj.Position,
                    static (obj, v) => obj.Position = v).WithCancellationToken(obj.CancellationToken);
        }

        public TweenBuilder<Vector2, Vector2TweenAdapter> TweenPosition(Vector2 from,
            Vector2 to, double duration)
        {
            return TweenBuilder.CreateEntry<Vector2, Vector2TweenAdapter>(new(from, to), duration)
                .Bind(obj, static (obj, v) => obj.Position = v).WithCancellationToken(obj.CancellationToken);
        }

        public TweenBuilder<float, FloatTweenAdapter> TweenSizeTo(float to, double duration)
        {
            return TweenBuilder
                .CreateToEntry<float, FloatTweenAdapter>(new(to), duration)
                .Bind(obj, static (obj) => obj.Size, static (obj, v) => obj.Size = v)
                .WithCancellationToken(obj.CancellationToken);
        }

        public TweenBuilder<float, FloatTweenAdapter> TweenRotationTo(float to, double duration)
        {
            return TweenBuilder
                .CreateToEntry<float, FloatTweenAdapter>(new(to), duration)
                .Bind(obj, static (obj) => obj.Rotation,
                    static (obj, v) => obj.Rotation = v)
                .WithCancellationToken(obj.CancellationToken);
        }
    }
}

public class SimpleSpriteObject : IDisposable
{
    private readonly CancellationTokenSource cts = new();

    public SimpleSpriteObject(Texture2D texture)
    {
        Texture = texture;
        tokenCache = cts.Token;
    }

    public Texture2D Texture { get; }

    public Vector2 Position
    {
        get
        {
            if (tokenCache.IsCancellationRequested)
            {
                throw new OperationCanceledException(tokenCache);
            }

            return field;
        }
        set
        {
            if (tokenCache.IsCancellationRequested)
            {
                throw new OperationCanceledException(tokenCache);
            }

            field = value;
        }
    }

    public float Size { get; set; } = 1;
    public float Rotation { get; set; } = 0;
    public Color Color { get; set; } = Color.White;
    private CancellationToken tokenCache;

    public CancellationToken CancellationToken => tokenCache;

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

        //Console.WriteLine("Dispose\n" + new StackTrace());
        cts.Dispose();
    }

    public void Draw(SpriteBatch sb)
    {
        if (cts.IsCancellationRequested) return;
        var rot = Rotation;
        rot %= MathF.PI * 2;
        if (rot < 0)
        {
            rot += MathF.PI * 2;
        }

        var baseRot = MathF.PI / 4;
        sb.Draw(Texture,
            Position - Size / MathF.Sqrt(2) * new Vector2(MathF.Cos(Rotation + baseRot), MathF.Sin(Rotation + baseRot)),
            null,
            Color,
            rot, default, Size,
            SpriteEffects.None, 0.00001f);
    }
}

public static class Vector2Tween
{
    extension(TweenTask)
    {
        public static TweenBuilderEntry<Vector2, Vector2TweenAdapter> Create(Vector2 start, Vector2 end,
            double duration)
        {
            return new(new(start, end), duration);
        }

        public static TweenBuilderEntry<Vector2, Vector2PathTweenAdapter> CreatePath(Vector2[] path,
            double duration)
        {
            return new(new(path), duration);
        }

        public static TweenBuilderEntry<Vector2, Vector2PathTweenAdapter> CreatePath(Spline2D spline2D,
            double duration)
        {
            return new(new(spline2D), duration);
        }
    }
}

public record struct Vector2TweenAdapter(Vector2 From, Vector2 To)
    : ITweenFromAdapter<Vector2>, IRelativeAdapter<Vector2>
{
    public Vector2TweenAdapter(Vector2 to) : this(default, to)
    {
    }

    public void ApplyFrom(Vector2 from, bool isRelative)
    {
        From = from;
        if (isRelative)
        {
            To += from;
        }
    }

    public Vector2 Evaluate(double progress)
    {
        return Vector2.Lerp(From, To, (float)progress);
    }
}

public struct Vector2PathTweenAdapter : ITweenAdapter<Vector2>
{
    public Vector2PathTweenAdapter(Spline2D spline2D)
    {
        this.Spline2D = spline2D;
        this.Path = null;
        this.PathType = PathType.CustomSpline;
    }

    public Vector2PathTweenAdapter(Vector2[] path)
    {
        this.Path = path;
        this.Spline2D = null;
        this.PathType = PathType.Linear;
    }

    public Vector2 Evaluate(double progress)
    {
        return PathType switch
        {
            PathType.Linear => Interpolation.Linear(Path!, (float)progress),
            PathType.CustomSpline => Spline2D!.GetPoint(progress),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public Spline2D? Spline2D { get; set; }
    public Vector2[]? Path { get; set; }
    public PathType PathType { get; set; }
}

public enum PathType
{
    Linear,
    CustomSpline
}

public static class Interpolation
{
    public static Vector2 Linear(ReadOnlySpan<Vector2> points, float t)
    {
        if (points.Length < 2) throw new ArgumentException("1次には2点以上必要です");
        int segCount = points.Length - 1;
        float scaledT = t * segCount;
        int seg = Math.Clamp((int)scaledT, 0, segCount - 1);
        float localT = scaledT - seg;
        var p0 = points[seg];
        var p1 = points[seg + 1];
        float u = 1 - localT;
        return u * p0 + localT * p1;
    }
}