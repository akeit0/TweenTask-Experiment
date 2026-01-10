using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TweenTasks;
using TweenTasks.Internal;
using MotionTasks;
using SpringTasks;
using Vector2 = System.Numerics.Vector2;

namespace MonoGameSample;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager graphics;

    private readonly Random rand = new();
    private readonly HashSet<SimpleSpriteObject> spriteObjects = new();
    private readonly HashSet<SimpleSpriteObject> spriteObjectsToDelete = new();

    private readonly ManualFrameDeltaTimeProvider provider = new(0);
    private SpriteBatch spriteBatch;

    private AudioSource soundFx;

    private bool spacePressed;
    private bool jKeyPressed;
    public Texture2D BoxTexture;
    public Texture2D Texture;
    private SpriteFont hudFont;
    private int MoveTweenCount { get; set; }
    private int DeletingCount { get; set; }
    private int TotalCount { get; set; }
    SimpleSpriteObject? springObject;
    SimpleSpriteObject? seqObject;
    SimpleSpriteObject? hoverObject;
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
        BoxTexture = new(graphics.GraphicsDevice, 1, 1);
        BoxTexture.SetData([Color.White]);
        hudFont = Content.Load<SpriteFont>("Fonts/Hud");
        Texture = Content.Load<Texture2D>("Textures/circle");
    }

    protected override void BeginRun()
    {
        MotionSystem.DefaultFrameDeltaTimeProvider = provider;


        var bounds = Window.ClientBounds;
        var center = new Vector2(bounds.Width / 2f, bounds.Height / 2f);

        springObject = new SimpleSpriteObject(Texture)
        {
            Position = center,
            Size = 50,
            Color = Color.White
        };

        Follow(springObject).Forget();

        hoverObject = new SimpleSpriteObject(Texture)
        {
            Position = center + new Vector2(-200, -100),
            Size = 50,
            Color = Color.MediumPurple
        };
        Hover(hoverObject).Forget();
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
        // CreateSeq();
        base.BeginRun();
    }


    void CreateSeq()
    {
        return;
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
                .WithRelative().WithLoop(2, LoopType.Incremental).WithEase(Ease.OutBounce))
            .Append(seqObject.TweenPositionTo(new Vector2(0, 100), 0.5)
                .WithRelative().WithLoop(3, LoopType.Yoyo).WithEase(Ease.InCirc))
            .Append(seqObject.TweenPositionTo(new Vector2(-100, 0), 0.5)
                .WithRelative().WithLoop(3, LoopType.Flip).WithEase(Ease.InCirc))
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
        provider.IncrementFrameCount();
        provider.Run(gameTime.ElapsedGameTime.TotalSeconds);
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
                        0.5 + 0.5 * rand.NextSingle())
                    .WithRelative()
                    .WithEase(Ease.InBounce)
                    .WithCancellationToken(newObj.CancellationToken)
                    .WithOnEvent(this, static (o, result) =>
                    {
                        if (result.IsEnd) o.MoveTweenCount--;
                        switch (result.EventType)
                        {
                            case TweenEventType.Complete:
                            {
                                o.soundFx.PlayWave(440 * MathF.Pow(2, o.rand.NextSingle() - 0.5f), 50, WaveType.Square,
                                    0.3f);
                            }
                                break;
                            case TweenEventType.Cancel:
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
                    .Append(newObj.TweenRotationTo(MathF.PI, 0.2))
                    .Append(newObj.TweenPositionTo(new Vector2(0, 100), 0.3).WithRelative()
                        .WithLoop(6, LoopType.Flip).WithDelay(0.1, DelayType.EveryLoop)
                        .WithEase(Ease.InExpo)
                        .WithOnEvent(this,
                            static (o, result) =>
                            {
                                if (result.EventType == TweenEventType.LoopComplete)
                                {
                                    Console.WriteLine(result.CompletedLoops);
                                    if (result.CompletedLoops % 2 == 1)
                                        o.soundFx.PlayWave(440 * MathF.Pow(2, (result.CompletedLoops - 1) / 12f), 400,
                                            WaveType.Sin,
                                            0.3f);
                                }
                            }))
                    .Append(newObj.TweenPositionTo(new Vector2(100, 0), 0.5).WithRelative())
                    .WithOnEvent(this,
                        static (o, result) =>
                        {
                            if (result.IsEnd) o.MoveTweenCount--;
                            if (result.EventType == TweenEventType.Cancel)
                            {
                                o.soundFx.PlayWave(0, 100, WaveType.Noise,
                                    0.1f);
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

    private async UniTaskVoid Follow(SimpleSpriteObject obj)
    {
        try
        {
            var followState = new StrongBox<(SimpleSpriteObject Obj, float TargetPos, bool Held)>();
            followState.Value.Obj = obj;
            obj.Position = new Vector2(300, 300);

            while (obj.CancellationToken.IsCancellationRequested == false)
            {
                await MotionTask.WaitWhile(obj, o =>
                {
                    var mouseState = Mouse.GetState();
                    if (mouseState.LeftButton == ButtonState.Released) return true;
                    return Vector2.Distance(o.Position, mouseState.PositionVector2) > 30f;
                });
                var from = obj.Position.X;
                var to = obj.Position.X > 400 ? 300 : 500;
                followState.Value.TargetPos = obj.Position.X > 400 ? 300 : 500;
                await SpringTask.Create(from, to,
                        new SpringConfig(frequency: 20, dampingRatio: 1f)
                        {
                            PositionEpsilon = 1,
                            VelocityEpsilon = 10
                        })
                    .Bind(obj, static (o, v) => o.Position = o.Position with { X = v })
                    .WithModifier(
                        followState, static (box, ref adapter) =>
                        {
                            var mouseState = Mouse.GetState();
                            ref var state = ref box.Value;
                            ref var held = ref state.Held;
                            if (mouseState.LeftButton == ButtonState.Pressed)
                            {
                                var mouseVec = mouseState.PositionVector2;
                                if (!held && Vector2.Distance(state.Obj.Position with { X = adapter.Current },
                                        mouseVec) > 30f)
                                {
                                    return;
                                }

                                adapter.Config.Frequency = 80;
                                adapter.Config.PositionEpsilon = 0;
                                adapter.To = Math.Clamp(mouseVec.X, 300, 500);
                                held = true;
                            }
                            else if (held)
                            {
                                var targetPos = state.TargetPos;
                                adapter.Config.Frequency = 20;
                                adapter.Config.PositionEpsilon = 1;
                                adapter.To =
                                    Math.Abs(adapter.Current - targetPos) < Math.Abs(adapter.Current - adapter.From)
                                        ? state.TargetPos
                                        : adapter.From;

                                held = false;
                            }
                        })
                    .WithCancellationToken(obj.CancellationToken)
                    .Schedule();

                if (Math.Abs(obj.Position.X - to) < Math.Abs(obj.Position.X - from))
                {
                    obj.Color = obj.Position.X < 400 ? Color.White : Color.LimeGreen;
                    await SpringTask.Create(to, to, velocity: 500, config:
                            new(frequency: 70, dampingRatio: 0.3f)
                            {
                                PositionEpsilon = 1,
                                VelocityEpsilon = 10
                            })
                        .Bind(obj, static (o, v) => o.Position = o.Position with { X = v })
                        .WithCancellationToken(obj.CancellationToken)
                        .Schedule();
                }
            }
        }
        catch (Exception e)
        {
            throw; // TODO 例外の処理
        }
    }

    private async UniTaskVoid Hover(SimpleSpriteObject obj)
    {
        try
        {
            const float baseSize = 50;
            const float hoverSize = 70;
            const float pressedSize = 40;

            static float GetTargetSize(SimpleSpriteObject obj)
            {
                var mouseState = Mouse.GetState();
                obj.Color = Color.MediumPurple;
                if (Vector2.Distance(obj.Position, mouseState.PositionVector2) >= obj.Size / 2)
                {
                    return baseSize;
                }

                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    obj.Color *= 0.8f;
                    return pressedSize;
                }
                else
                {
                    obj.Color *= 1.2f;
                    return hoverSize;
                }
            }

            while (!obj.CancellationToken.IsCancellationRequested)
            {
                await MotionTask.WaitWhile(obj, obj.Size switch
                {
                    < baseSize => o =>
                        GetTargetSize(o) < baseSize,
                    < hoverSize => o =>
                        GetTargetSize(o) < hoverSize,
                    _ => o =>
                        GetTargetSize(o) >= hoverSize,
                });
                await SpringTask.Create(obj.Size, 50, config: new(frequency: 20, dampingRatio: 0.65f)
                    {
                        PositionEpsilon = 1,
                        VelocityEpsilon = 10
                    })
                    .Bind(obj, static (o, v) => o.Size = v)
                    .WithModifier(
                        obj, static (obj, ref adapter) => adapter.To = GetTargetSize(obj))
                    .WithCancellationToken(obj.CancellationToken)
                    .Schedule();
            }
        }
        catch (Exception e)
        {
            throw; // TODO 例外の処理
        }
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
                    .WithOnEvent(this, (game, result) =>
                    {
                        if (result.EventType == TweenEventType.Complete)
                        {
                            game.DeletingCount--;
                        }
                        else if (result.EventType == TweenEventType.Cancel)
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
        GraphicsDevice.Clear(Color.White * 0.1f);
        spriteBatch.Begin();

        seqObject?.Draw(spriteBatch);
        foreach (var spriteObject in spriteObjects) spriteObject.Draw(spriteBatch);
        if (seqObject != null)
        {
            spriteBatch.DrawString(hudFont,
                $"SeqTime: {seqTask.Time:f1}", new Vector2(0, 0),
                Color.White);
        }

        spriteBatch.Draw(BoxTexture, new Vector2(400, 0), null,
            Color.White,
            0, default, new Vector2(1, 500),
            SpriteEffects.None, 0.00001f);

        // spriteBatch.DrawString(hudFont,
        //     $"Moving: {MoveTweenCount:00}, Deleting: {DeletingCount:00}, Active: {spriteObjects.Count:00}",
        //     new Vector2(0, 50),
        //     Color.White);
        hoverObject?.Draw(spriteBatch);
        springObject?.Draw(spriteBatch);
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
                .Bind(obj, static obj => obj.Position,
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
                .Bind(obj, static obj => obj.Size, static (obj, v) => obj.Size = v)
                .WithCancellationToken(obj.CancellationToken);
        }

        public TweenBuilder<float, FloatTweenAdapter> TweenRotationTo(float to, double duration)
        {
            return TweenBuilder
                .CreateToEntry<float, FloatTweenAdapter>(new(to), duration)
                .Bind(obj, static obj => obj.Rotation,
                    static (obj, v) => obj.Rotation = v)
                .WithCancellationToken(obj.CancellationToken);
        }
    }
}

static class XnaVector2Extensions
{
    extension(MouseState state)
    {
        public Vector2 PositionVector2
        {
            get
            {
                var pos = state.Position;
                return new Vector2(pos.X, pos.Y);
            }
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
            rot, default, Size / Texture.Width,
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