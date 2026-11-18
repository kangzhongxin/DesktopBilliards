using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Text;

namespace DesktopBilliards;

internal sealed class GameForm : Form
{
    private const float FixedStep = 1f / 120f;
    private const int HotkeyId = 0x5B1;
    private const int RackBallCount = 15;

    private static readonly Color Mint = Color.FromArgb(255, 140, 232, 206);
    private static readonly Color Ice = Color.FromArgb(255, 166, 202, 255);
    private static readonly Color Gold = Color.FromArgb(255, 255, 198, 105);
    private static readonly Color Coral = Color.FromArgb(255, 255, 112, 92);
    private static readonly Color White = Color.FromArgb(255, 240, 247, 255);

    private static readonly Color[] BallAccents =
    {
        Color.FromArgb(255, 245, 197, 66),
        Color.FromArgb(255, 78, 134, 232),
        Color.FromArgb(255, 228, 87, 87),
        Color.FromArgb(255, 155, 107, 223),
        Color.FromArgb(255, 242, 153, 74),
        Color.FromArgb(255, 79, 191, 123),
        Color.FromArgb(255, 184, 92, 92),
        Color.FromArgb(255, 88, 108, 140)
    };

    // 1-15 号球的实际摆位：顶点 1 号，8 号在中心。
    private static readonly int[][] RackNumbers =
    {
        new[] { 1 },
        new[] { 2, 9 },
        new[] { 3, 8, 10 },
        new[] { 4, 11, 5, 12 },
        new[] { 6, 13, 7, 14, 15 }
    };

    private static readonly Color[] NumberColors =
    {
        White,                         // 0：白球占位
        Color.FromArgb(255, 245, 197, 66),   // 1 黄
        Color.FromArgb(255, 72, 128, 228),   // 2 蓝
        Color.FromArgb(255, 224, 78, 78),    // 3 红
        Color.FromArgb(255, 150, 98, 220),   // 4 紫
        Color.FromArgb(255, 240, 145, 62),   // 5 橙
        Color.FromArgb(255, 62, 176, 112),   // 6 绿
        Color.FromArgb(255, 158, 68, 68),    // 7 棕红
        Color.FromArgb(255, 28, 34, 46),     // 8 黑
        Color.FromArgb(255, 238, 204, 96),   // 9 黄
        Color.FromArgb(255, 80, 144, 235),   // 10 蓝
        Color.FromArgb(255, 232, 92, 92),    // 11 红
        Color.FromArgb(255, 164, 116, 232),  // 12 紫
        Color.FromArgb(255, 246, 164, 76),   // 13 橙
        Color.FromArgb(255, 88, 190, 128),   // 14 绿
        Color.FromArgb(255, 184, 82, 82)     // 15 棕红
    };

    private static Color NumberColor(int number) =>
        NumberColors[Math.Clamp(number, 1, 15)];

    private enum Phase
    {
        Aiming,
        Rolling,
        Won
    }

    private enum DemoStage
    {
        Rack,
        BreakAim,
        WaitSettle,
        MoveCue,
        PotAim,
        PotWait,
        Done
    }

    private sealed class Ball
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public float Mass = 1f;
        public bool IsCue;
        public bool Potted;
        public int Number;
        public Bitmap Sprite = null!;
        public Color Accent;
        public readonly Queue<Vector2> Trail = new();
        public float TrailTimer;

        public float InvMass => 1f / Mass;
    }

    private sealed class Pocket
    {
        public Vector2 Center;
        public float Radius;
        public int Index;
        public float Flash;
    }

    private sealed class IconStart
    {
        public Bitmap Sprite = null!;
        public Vector2 Position;
        public Color Accent;
        public int Index;
    }

    private sealed class Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Gravity;
        public Color Color;
    }

    private sealed class Shockwave
    {
        public Vector2 Center;
        public float Age;
        public float Duration;
        public float MaxRadius;
        public float Width;
        public Color Color;
    }

    private sealed class PotAnimation
    {
        public Bitmap Sprite = null!;
        public Vector2 Start;
        public Pocket Pocket = null!;
        public float Age;
        public float Duration;
        public float Angle;
        public Color Accent;
    }

    private sealed class ComboPopup
    {
        public Pocket Pocket = null!;
        public int Count;
        public float Age;
        public float Duration;
    }

    private readonly DesktopSnapshot _snapshot;
    private readonly List<Ball> _balls = new();
    private readonly List<IconStart> _iconStarts = new();
    private readonly List<Pocket> _pockets = new();
    private readonly List<Particle> _particles = new();
    private readonly List<Shockwave> _waves = new();
    private readonly List<PotAnimation> _pots = new();
    private readonly List<ComboPopup> _combos = new();
    private readonly List<Bitmap> _owned = new();
    private readonly Random _rng = new(20261117);
    private readonly Stopwatch _frameClock = Stopwatch.StartNew();
    private readonly Stopwatch _gameClock = new();
    private readonly System.Windows.Forms.Timer _timer;

    private readonly Font _hudFont = new("Microsoft YaHei UI", 22f, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _smallFont = new("Microsoft YaHei UI", 16f, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly Font _tinyFont = new("Microsoft YaHei UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly Font _titleFont = new("Microsoft YaHei UI", 58f, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _comboFont = new("Segoe UI", 72f, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _captionFont = new("Microsoft YaHei UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);

    private Bitmap _shadow = null!;
    private Bitmap _cueSprite = null!;
    private Bitmap _pocketSprite = null!;
    private Bitmap _pocketGlow = null!;
    private Bitmap _goldGlow = null!;
    private Bitmap _cueGlow = null!;
    private Bitmap _vignette = null!;
    private Bitmap _board = null!;
    private readonly Bitmap?[] _numberSprites = new Bitmap?[16];
    private Font _numberFont = null!;

    private RectangleF _table;
    private Vector2 _headSpot;
    private Vector2 _footSpot;
    private Vector2 _rackLongDir = Vector2.UnitX;
    private Vector2 _rackPerp = Vector2.UnitY;
    private float _ballDiameter;
    private float _cueRadius;
    private float _pocketRadius;
    private Ball _cue = null!;

    private Phase _phase = Phase.Aiming;
    private bool _charging;
    private double _chargeStart;
    private float _power;
    private Vector2 _aimPoint;
    private Vector2 _aimDir = -Vector2.UnitY;

    private int _combo;
    private int _shotPots;
    private int _potted;
    private int _shots;
    private int _total;
    private Pocket? _lastPocket;
    private bool _cuePotted;
    private bool _won;
    private bool _paused;
    private bool _assist = true;
    private bool _hotkeyRegistered;

    private double _lastFrameSeconds;
    private double _accumulator;
    private float _time;
    private float _flash;
    private Color _flashColor = Color.White;
    private string _toast = "";
    private float _toastTime;
    private bool _paintLogged;
    private bool _paintErrorLogged;
    private readonly bool _demo;
    private readonly bool _demoFill;

    private DemoStage _demoStage = DemoStage.Rack;
    private float _demoTime;
    private float _demoStageTime;
    private int _demoPotsDone;
    private bool _demoRackSaved;
    private bool _demoFinalSaved;
    private bool _demoMovingCue;
    private Ball? _demoTarget;
    private Pocket? _demoPocket;
    private Vector2 _demoCueFrom;
    private Vector2 _demoCueTo;
    private float _demoMoveElapsed;

    private bool _demoRecording;
    private string _demoFrameDir = "";
    private int _demoFrameIndex;
    private float _demoRecordAccumulator;
    private float _demoRecordStopTime = float.MaxValue;

    private const float CueStrikeDuration = 0.18f;
    private float _cueStrikeTimer;
    private Vector2 _cueStrikeOrigin;
    private Vector2 _cueStrikeDir;
    private float _cueStrikePower;

    public GameForm(DesktopSnapshot snapshot, bool demo = false, bool demoFill = false)
    {
        Log.Write("GameForm: ctor start");
        _snapshot = snapshot;
        _demo = demo;
        _demoFill = demoFill;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Opaque,
            true);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = snapshot.WorkArea;
        TopMost = true;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        BackColor = Color.Black;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        Text = "Desktop Billiards";

        BuildSprites();
        BuildLayout();
        BuildBoard();
        ResetGame();

        _timer = new System.Windows.Forms.Timer { Interval = 15 };
        _timer.Tick += TimerOnTick;

        NativeMethods.TimeBeginPeriod(1);
        _lastFrameSeconds = _frameClock.Elapsed.TotalSeconds;

        Log.Write($"GameForm: ctor end bounds={Bounds} client={ClientSize} dpi={DeviceDpi}");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Log.Write($"GameForm: OnShown bounds={Bounds} visible={Visible} topmost={TopMost} dpi={DeviceDpi}");

        Activate();
        BringToFront();

        // 从终端/其他窗口启动时，Windows 可能不允许直接抢前台；
        // 强制把窗口放进 topmost 带，确保游戏盖在整个桌面上。
        NativeMethods.SetWindowPos(
            Handle,
            (IntPtr)(-1),
            0,
            0,
            0,
            0,
            0x0002 | 0x0001 | 0x0040); // NOMOVE | NOSIZE | SHOWWINDOW

        NativeMethods.SetForegroundWindow(Handle);
        LogWindowState("OnShown");
        _lastFrameSeconds = _frameClock.Elapsed.TotalSeconds;

        var stateTimer = new System.Windows.Forms.Timer { Interval = 600 };
        stateTimer.Tick += (_, _) =>
        {
            stateTimer.Stop();
            stateTimer.Dispose();
            LogWindowState("600ms");
        };
        stateTimer.Start();

        _hotkeyRegistered = NativeMethods.RegisterHotKey(
            Handle,
            HotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
            0x58); // X

        _timer.Start();
        Invalidate();

        if (_demo)
            StartDemoCapture();
    }

    private void StartDemoCapture()
    {
        _demoStage = DemoStage.Rack;
        _demoTime = 0f;
        _demoStageTime = 0f;
        _demoPotsDone = 0;
        _demoRackSaved = false;
        _demoFinalSaved = false;
        _demoMovingCue = false;
        Log.Write("Demo: start");
    }

    private void UpdateDemo(double now, float dt)
    {
        _demoTime += dt;

        if (_demoRecording)
        {
            _demoRecordAccumulator += dt;

            if (_demoRecordAccumulator >= 0.05f)
            {
                _demoRecordAccumulator = 0f;
                WriteDemoFrame();

                if (_demoTime >= _demoRecordStopTime)
                {
                    _demoRecording = false;
                    Log.Write($"Demo: record done frames={_demoFrameIndex} t={_demoTime:0.00}");
                }
            }
        }

        switch (_demoStage)
        {
            case DemoStage.Rack:
                if (!_demoRackSaved && _demoTime >= 1.2f)
                {
                    _demoRackSaved = true;
                    SaveDebugScreenshot("DesktopBilliards-rack.png");
                }

                if (_demoTime >= 1.4f)
                {
                    _demoStage = DemoStage.BreakAim;
                    _demoStageTime = _demoTime;
                    _aimDir = Vector2.Normalize(_footSpot - _cue.Position);
                    _charging = true;
                    _power = 0f;
                    _chargeStart = now;
                    Log.Write($"Demo: break aim t={_demoTime:0.00}");
                }
                break;

            case DemoStage.BreakAim:
                if (_demoTime - _demoStageTime >= 0.70f && _phase == Phase.Aiming)
                {
                    _power = 1f;
                    Log.Write($"Demo: break shoot t={_demoTime:0.00} power={_power:0.00}");
                    Shoot();
                    _demoStage = DemoStage.WaitSettle;
                    _demoStageTime = _demoTime;
                }
                break;

            case DemoStage.WaitSettle:
                if (_phase == Phase.Aiming && _demoTime - _demoStageTime >= 0.5f)
                {
                    // 开球可能碰巧进袋，重置连击，保证演示从 ×2 开始。
                    _combo = 0;
                    _shotPots = 0;
                    _combos.Clear();
                    _lastPocket = null;
                    Log.Write($"Demo: settled t={_demoTime:0.00} potted={_potted}");
                    StartDemoRecording();
                    StartDemoPot(now);
                }
                break;

            case DemoStage.MoveCue:
                _demoMoveElapsed += dt;
                {
                    float t = Math.Clamp(_demoMoveElapsed / 0.22f, 0f, 1f);
                    float eased = t * t * (3f - 2f * t);
                    _cue.Position = Vector2.Lerp(_demoCueFrom, _demoCueTo, eased);
                    _cue.Velocity = Vector2.Zero;

                    if (t >= 1f)
                    {
                        _demoMovingCue = false;
                        _aimDir = Vector2.Normalize(_demoTarget!.Position - _cue.Position);
                        _charging = true;
                        _power = 0f;
                        _chargeStart = now;
                        _demoStage = DemoStage.PotAim;
                        _demoStageTime = _demoTime;
                        Log.Write($"Demo: aim pot {_demoPotsDone + 1} t={_demoTime:0.00}");
                    }
                }
                break;

            case DemoStage.PotAim:
                if (_demoTarget == null || _demoTarget.Potted)
                {
                    StartDemoPot(now);
                    break;
                }

                if (_demoTime - _demoStageTime >= 0.55f)
                    DemoStrike(now);
                break;

            case DemoStage.PotWait:
                if (_demoTime - _demoStageTime >= 0.95f)
                {
                    if (_demoPotsDone >= 3 || _won)
                        FinishDemo();
                    else
                        StartDemoPot(now);
                }
                break;
        }
    }

    private void StartDemoPot(double now)
    {
        if (_won || _demoPotsDone >= 3)
        {
            FinishDemo();
            return;
        }

        if (!TryPickDemoPot(out var target, out var pocket, out var cuePosition))
        {
            FinishDemo();
            return;
        }

        _demoTarget = target;
        _demoPocket = pocket;
        _demoCueFrom = _cue.Position;
        _demoCueTo = cuePosition;
        _demoMoveElapsed = 0f;
        _demoMovingCue = true;

        _phase = Phase.Aiming;
        _charging = false;
        _power = 0f;
        _cue.Velocity = Vector2.Zero;
        _cue.Potted = false;
        _cue.Trail.Clear();

        _demoStage = DemoStage.MoveCue;
        _demoStageTime = _demoTime;
        Log.Write($"Demo: move cue for pot {_demoPotsDone + 1}");
    }

    private bool TryPickDemoPot(out Ball target, out Pocket pocket, out Vector2 cuePosition)
    {
        target = null!;
        pocket = null!;
        cuePosition = default;

        float bestDistance = float.MaxValue;

        foreach (var candidatePocket in _pockets)
        {
            foreach (var candidateBall in _balls)
            {
                if (candidateBall.IsCue || candidateBall.Potted)
                    continue;

                float distance = Vector2.Distance(candidateBall.Position, candidatePocket.Center);
                if (distance < _cueRadius * 2f + 8f || distance > _table.Width * 0.55f)
                    continue;

                var toPocket = Vector2.Normalize(candidatePocket.Center - candidateBall.Position);

                for (float extra = 8f; extra <= 65f; extra += 13f)
                {
                    var cuePoint = candidateBall.Position
                        - toPocket * (_cueRadius * 2f + extra);

                    if (!IsDemoCueSpotFree(cuePoint, candidateBall, toPocket))
                        continue;

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        target = candidateBall;
                        pocket = candidatePocket;
                        cuePosition = cuePoint;
                    }
                    break;
                }
            }
        }

        if (target != null)
            return true;

        // 兜底：球桌很拥挤时，优先保证演示能继续。
        float bestFallback = float.MaxValue;
        Ball? fallbackBall = null;
        Pocket? fallbackPocket = null;

        foreach (var candidateBall in _balls)
        {
            if (candidateBall.IsCue || candidateBall.Potted)
                continue;

            foreach (var candidatePocket in _pockets)
            {
                float distance = Vector2.Distance(candidateBall.Position, candidatePocket.Center);
                if (distance < bestFallback)
                {
                    bestFallback = distance;
                    fallbackBall = candidateBall;
                    fallbackPocket = candidatePocket;
                }
            }
        }

        if (fallbackBall == null || fallbackPocket == null)
            return false;

        var fallbackDir = Vector2.Normalize(fallbackPocket.Center - fallbackBall.Position);
        target = fallbackBall;
        pocket = fallbackPocket;
        cuePosition = ClampToTable(
            fallbackBall.Position - fallbackDir * (_cueRadius * 2f + 20f),
            _cueRadius);
        return true;
    }

    private bool IsDemoCueSpotFree(Vector2 point, Ball target, Vector2 shotDirection)
    {
        if (!_table.Contains(point.X, point.Y))
            return false;

        if (point.X < _table.Left + _cueRadius + 4f ||
            point.X > _table.Right - _cueRadius - 4f ||
            point.Y < _table.Top + _cueRadius + 4f ||
            point.Y > _table.Bottom - _cueRadius - 4f)
            return false;

        // 球杆要从白球后方伸出去；后方空间太短会看起来像一根短棍。
        if (AvailableRayLength(point, -shotDirection, 8f) < _ballDiameter * 2.2f)
            return false;

        foreach (var pocket in _pockets)
        {
            if (Vector2.Distance(point, pocket.Center) < pocket.Radius + _cueRadius * 0.9f)
                return false;
        }

        foreach (var ball in _balls)
        {
            if (ball == target || ball.IsCue || ball.Potted)
                continue;

            if (Vector2.Distance(point, ball.Position) < _cueRadius + ball.Radius + 6f)
                return false;
        }

        return true;
    }

    private void DemoStrike(double now)
    {
        if (_demoTarget == null || _demoPocket == null)
        {
            StartDemoPot(now);
            return;
        }

        _charging = false;
        _cueStrikeOrigin = _cue.Position;
        _cueStrikeDir = _aimDir;
        _cueStrikePower = MathF.Max(0.35f, _power);
        _cueStrikeTimer = CueStrikeDuration;

        _demoPotsDone++;
        Log.Write($"Demo: pot {_demoPotsDone} t={_demoTime:0.00} number={_demoTarget.Number}");
        Pot(_demoTarget, _demoPocket);

        _power = 0f;
        _demoStage = DemoStage.PotWait;
        _demoStageTime = _demoTime;
    }

    private void FinishDemo()
    {
        if (_demoStage == DemoStage.Done)
            return;

        _demoStage = DemoStage.Done;
        _demoMovingCue = false;
        _charging = false;
        _power = 0f;
        _demoRecordStopTime = _demoTime + 1.0f;
        Log.Write($"Demo: done t={_demoTime:0.00} pots={_demoPotsDone}");

        if (!_demoFinalSaved)
        {
            _demoFinalSaved = true;
            SaveDebugScreenshot("DesktopBilliards-game.png");
        }
    }

    private void StartDemoRecording()
    {
        try
        {
            _demoRecording = true;
            _demoFrameDir = Path.Combine(Path.GetTempPath(), "DesktopBilliards-frames");
            Directory.CreateDirectory(_demoFrameDir);

            foreach (var file in Directory.EnumerateFiles(_demoFrameDir))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // 忽略清理失败的文件。
                }
            }

            _demoFrameIndex = 0;
            _demoRecordAccumulator = 0.05f; // 立即写第一帧
            _demoRecordStopTime = float.MaxValue;
            Log.Write("Demo: record start");
        }
        catch (Exception ex)
        {
            _demoRecording = false;
            Log.Write("Demo: record start failed " + ex.Message);
        }
    }

    private void WriteDemoFrame()
    {
        try
        {
            using var full = new Bitmap(
                ClientSize.Width,
                ClientSize.Height,
                PixelFormat.Format32bppPArgb);

            using (var g = Graphics.FromImage(full))
                RenderScene(g);

            const int width = 900;
            int height = Math.Max(1, (int)Math.Round(full.Height * (width / (double)full.Width)));

            using var small = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(full, 0, 0, width, height);
            }

            string name = $"frame_{_demoFrameIndex:0000}.png";
            small.Save(Path.Combine(_demoFrameDir, name), ImageFormat.Png);
            File.AppendAllText(
                Path.Combine(_demoFrameDir, "frames.txt"),
                $"{name}|{_demoTime:0.000}{Environment.NewLine}");

            _demoFrameIndex++;
        }
        catch (Exception ex)
        {
            Log.Write("Demo frame failed: " + ex.Message);
        }
    }

    private void SaveDebugScreenshot(string fileName)
    {
        try
        {
            using var bitmap = new Bitmap(
                ClientSize.Width,
                ClientSize.Height,
                PixelFormat.Format32bppPArgb);

            using (var g = Graphics.FromImage(bitmap))
                RenderScene(g);

            var path = Path.Combine(Path.GetTempPath(), fileName);
            bitmap.Save(path, ImageFormat.Png);
            Log.Write("Debug screenshot saved: " + path);
        }
        catch (Exception ex)
        {
            Log.Write("Debug screenshot failed: " + ex.Message);
        }
    }

    private void LogWindowState(string context)
    {
        var exStyle = NativeMethods.GetWindowLongPtr(Handle, -20).ToInt64();
        var foreground = NativeMethods.GetForegroundWindow();
        var fgClass = new StringBuilder(256);
        NativeMethods.GetClassName(foreground, fgClass, fgClass.Capacity);

        Log.Write(
            $"GameForm {context}: handle=0x{Handle.ToInt64():X} visible={Visible} " +
            $"topmostStyle={(exStyle & 0x8) != 0} ex=0x{exStyle:X} " +
            $"fg=0x{foreground.ToInt64():X} fgClass={fgClass}");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();

        if (_hotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _hotkeyRegistered = false;
        }

        NativeMethods.TimeEndPeriod(1);

        foreach (var bitmap in _owned)
            bitmap.Dispose();

        _hudFont.Dispose();
        _smallFont.Dispose();
        _tinyFont.Dispose();
        _titleFont.Dispose();
        _comboFont.Dispose();
        _captionFont.Dispose();
        _numberFont.Dispose();

        base.OnFormClosed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
        {
            Close();
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // 全部在 OnPaint 中绘制。
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;

        if (!_paintLogged)
        {
            _paintLogged = true;
            Log.Write($"GameForm: first paint client={ClientSize} clip={g.ClipBounds}");
        }

        try
        {
            RenderScene(g);
        }
        catch (Exception ex)
        {
            if (!_paintErrorLogged)
            {
                _paintErrorLogged = true;
                Log.Write("PAINT ERROR: " + ex);
            }
        }
    }

    /// <summary>
    /// 游戏画面的唯一绘制入口。屏幕绘制和离屏录制都走这里，
    /// 这样录制动图时不会把其他窗口截进去。
    /// </summary>
    private void RenderScene(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;

        // 壁纸、暗角、球台边框、袋口底座都是一次性渲染好的静态图层，
        // 每帧只需要一次整屏 blit，避免 GDI+ 反复做全屏半透明绘制。
        g.DrawImageUnscaled(_board, 0, 0);
        DrawPocketEffects(g);
        DrawShadows(g);
        DrawTrails(g);
        DrawBalls(g);
        DrawPotAnimations(g);

        if (_won)
        {
            using var dim = new SolidBrush(Color.FromArgb(76, 3, 6, 12));
            g.FillRectangle(dim, ClientRectangle);
        }

        DrawParticles(g);
        DrawShockwaves(g);

        bool demoAimVisible = !_demo
            || _demoStage == DemoStage.BreakAim
            || _demoStage == DemoStage.PotAim;

        bool aiming = _phase == Phase.Aiming
            && !_won
            && !_paused
            && demoAimVisible
            && !_demoMovingCue;

        bool striking = _cueStrikeTimer > 0f && !_won && !_paused;

        if (aiming && !striking)
            DrawAimAssist(g);

        if (aiming || striking)
            DrawCueStick(g);

        if (aiming && !striking)
            DrawPowerRing(g);

        DrawComboPopups(g);
        DrawHud(g);
        DrawToast(g);
        DrawFlash(g);

        if (_won)
            DrawWinCard(g);

        if (_paused)
            DrawPauseCard(g);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_paused)
            return;

        UpdateAim(e.Location);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Log.Write($"MouseDown {e.Button} phase={_phase} paused={_paused} charging={_charging}");

        if (_demo)
            return;

        if (_paused)
        {
            ResumeFromPause();
            return;
        }

        if (e.Button == MouseButtons.Left && _phase == Phase.Aiming && !_charging)
        {
            _charging = true;
            _chargeStart = _frameClock.Elapsed.TotalSeconds;
            _power = 0;
            Capture = true;
        }
        else if (e.Button == MouseButtons.Right && _phase == Phase.Aiming)
        {
            MoveCueBall(e.Location);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        Log.Write($"MouseUp {e.Button} charging={_charging} phase={_phase} power={_power:0.00}");

        if (_demo)
            return;

        if (e.Button != MouseButtons.Left || !_charging)
            return;

        _charging = false;
        Capture = false;

        if (_phase == Phase.Aiming && !_paused)
            Shoot();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.KeyCode)
        {
            case Keys.Escape:
                Close();
                break;

            case Keys.R:
                ResetGame();
                break;

            case Keys.F1:
                _assist = !_assist;
                ShowToast(_assist ? "辅助线：开" : "辅助线：关");
                break;
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Log.Write("GameForm deactivated");

        // 从终端/后台启动时 Windows 可能短暂抢走焦点，不能因此暂停整局；
        // 只取消正在蓄力的这一杆，避免鼠标松开时误发射。
        _charging = false;
        _power = 0;
        Capture = false;
    }

    private void ResumeFromPause()
    {
        _paused = false;
        _lastFrameSeconds = _frameClock.Elapsed.TotalSeconds;
        Activate();
        Invalidate();
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        if (_paused)
            return;

        double now = _frameClock.Elapsed.TotalSeconds;
        float dt = (float)(now - _lastFrameSeconds);
        _lastFrameSeconds = now;

        if (dt <= 0f || dt > 0.08f)
            dt = 1f / 60f;

        _time += dt;
        _cueStrikeTimer = MathF.Max(0f, _cueStrikeTimer - dt);

        if (_charging && _phase == Phase.Aiming)
            _power = Math.Clamp((float)((now - _chargeStart) / 1.15), 0f, 1f);

        if (_phase != Phase.Won)
        {
            _accumulator += dt;
            int guard = 0;

            while (_accumulator >= FixedStep && guard++ < 8)
            {
                StepPhysics(FixedStep);
                _accumulator -= FixedStep;
            }

            if (_phase == Phase.Rolling && AllStopped())
                OnShotSettled();
        }

        if (_demo)
            UpdateDemo(now, dt);

        UpdateEffects(dt);
        Invalidate();
    }

    private void BuildSprites()
    {
        float maxIcon = 46f;
        foreach (var icon in _snapshot.Icons)
            maxIcon = Math.Max(maxIcon, Math.Min(icon.ScreenRect.Width, icon.ScreenRect.Height));

        _ballDiameter = Math.Clamp(maxIcon + 12f, 46f, 96f);
        _cueRadius = _ballDiameter / 2f;
        _pocketRadius = _ballDiameter * 0.82f;

        _shadow = Keep(SpriteFactory.CreateShadow((int)(_ballDiameter * 2.4f)));
        _cueSprite = Keep(SpriteFactory.CreateCueBall((int)_ballDiameter));
        _pocketSprite = Keep(SpriteFactory.CreatePocket((int)(_pocketRadius * 2f), Mint));
        _pocketGlow = Keep(SpriteFactory.CreateGlow((int)(_ballDiameter * 6f), Mint));
        _goldGlow = Keep(SpriteFactory.CreateGlow((int)(_ballDiameter * 5f), Gold));
        _cueGlow = Keep(SpriteFactory.CreateGlow((int)(_ballDiameter * 3.2f), Ice));
        _vignette = Keep(SpriteFactory.CreateVignette(
            Math.Max(1, ClientSize.Width),
            Math.Max(1, ClientSize.Height)));

        for (int number = 1; number <= 15; number++)
            _numberSprites[number] = Keep(SpriteFactory.CreateSolidBall(
                (int)_ballDiameter,
                NumberColor(number)));

        _numberFont = new Font(
            "Segoe UI",
            Math.Clamp(_ballDiameter * 0.30f, 12f, 24f),
            FontStyle.Bold,
            GraphicsUnit.Pixel);

        for (int i = 0; i < _snapshot.Icons.Count; i++)
        {
            var icon = _snapshot.Icons[i];
            Color accent = BallAccents[i % BallAccents.Length];
            Bitmap sprite;

            try
            {
                sprite = SpriteFactory.CreateBall(icon.Image, (int)_ballDiameter, accent);
            }
            catch
            {
                sprite = SpriteFactory.CreatePlainBall((int)_ballDiameter, accent);
            }

            Keep(sprite);

            var center = new Vector2(
                icon.ScreenRect.Left + icon.ScreenRect.Width / 2f - _snapshot.WorkArea.Left,
                icon.ScreenRect.Top + icon.ScreenRect.Height / 2f - _snapshot.WorkArea.Top);

            _iconStarts.Add(new IconStart
            {
                Sprite = sprite,
                Position = center,
                Accent = accent,
                Index = i
            });
        }
    }

    private void BuildLayout()
    {
        float width = Math.Max(320, ClientSize.Width);
        float height = Math.Max(240, ClientSize.Height);
        float margin = Math.Max(14f, _ballDiameter * 0.24f);

        _table = new RectangleF(margin, margin, width - margin * 2f, height - margin * 2f);

        bool horizontal = _table.Width >= _table.Height;
        _rackLongDir = horizontal ? Vector2.UnitX : Vector2.UnitY;
        _rackPerp = horizontal ? Vector2.UnitY : -Vector2.UnitX;

        var center = new Vector2(
            _table.Left + _table.Width / 2f,
            _table.Top + _table.Height / 2f);

        float longLength = horizontal ? _table.Width : _table.Height;
        _headSpot = center - _rackLongDir * (longLength * 0.25f);
        _footSpot = center + _rackLongDir * (longLength * 0.25f);

        // 确保 15 球三角架不会顶到库边。
        float spacing = _cueRadius * 2f + 1.5f;
        float rowSpacing = spacing * MathF.Sqrt(3f) / 2f;
        float rackExtent = rowSpacing * 4f + _cueRadius;
        float farRail = horizontal ? _table.Right : _table.Bottom;
        float maxFoot = farRail - margin - rackExtent;

        if (horizontal)
            _footSpot.X = MathF.Min(_footSpot.X, maxFoot);
        else
            _footSpot.Y = MathF.Min(_footSpot.Y, maxFoot);

        _pockets.Clear();
        AddPocket(_table.Left, _table.Top);
        AddPocket(_table.Left + _table.Width / 2f, _table.Top);
        AddPocket(_table.Right, _table.Top);
        AddPocket(_table.Left, _table.Bottom);
        AddPocket(_table.Left + _table.Width / 2f, _table.Bottom);
        AddPocket(_table.Right, _table.Bottom);
    }

    private void AddPocket(float x, float y)
    {
        _pockets.Add(new Pocket
        {
            Center = new Vector2(x, y),
            Radius = _pocketRadius,
            Index = _pockets.Count
        });
    }

    private void ResetGame()
    {
        _balls.Clear();
        _particles.Clear();
        _waves.Clear();
        _pots.Clear();
        _combos.Clear();

        _combo = 0;
        _shotPots = 0;
        _potted = 0;
        _shots = 0;
        _total = RackBallCount;
        _lastPocket = null;
        _cuePotted = false;
        _won = false;
        _paused = false;
        _phase = Phase.Aiming;
        _charging = false;
        _power = 0;
        Capture = false;
        _flash = 0f;
        _toast = "";
        _toastTime = 0f;
        _accumulator = 0;
        _gameClock.Reset();

        BuildRack();

        // 把初始位置重叠的球轻轻推开，避免开局就互相弹飞。
        for (int iteration = 0; iteration < 16; iteration++)
        {
            bool moved = false;

            for (int i = 0; i < _balls.Count; i++)
            {
                for (int j = i + 1; j < _balls.Count; j++)
                {
                    var a = _balls[i];
                    var b = _balls[j];
                    var delta = b.Position - a.Position;
                    float distSq = delta.LengthSquared();
                    float sum = a.Radius + b.Radius;

                    if (distSq >= sum * sum || distSq < 0.0001f)
                        continue;

                    float dist = MathF.Sqrt(distSq);
                    var n = delta / dist;
                    float push = (sum - dist) * 0.5f + 0.1f;
                    a.Position -= n * push;
                    b.Position += n * push;
                    moved = true;
                }
            }

            foreach (var ball in _balls)
            {
                ball.Position = ClampToTable(ball.Position, ball.Radius);
                ball.Position = PushOutOfPockets(ball.Position, ball.Radius);
            }

            if (!moved)
                break;
        }

        var cueStart = FindCueSpot();
        _cue = new Ball
        {
            Position = cueStart,
            Velocity = Vector2.Zero,
            Radius = _cueRadius,
            Mass = 1.15f,
            IsCue = true,
            Sprite = _cueSprite,
            Accent = Mint,
            Number = 0
        };
        _balls.Add(_cue);

        var mouse = PointToClient(Cursor.Position);
        _aimPoint = new Vector2(mouse.X, mouse.Y);
        var toMouse = _aimPoint - _cue.Position;
        _aimDir = toMouse.LengthSquared() > 64f ? Vector2.Normalize(toMouse) : -Vector2.UnitY;

        _lastFrameSeconds = _frameClock.Elapsed.TotalSeconds;
    }

    private void BuildRack()
    {
        var pool = new List<IconStart>(_iconStarts);
        Shuffle(pool);

        int iconLimit = _demoFill
            ? Math.Min(5, pool.Count)
            : Math.Min(RackBallCount, pool.Count);

        int iconIndex = 0;
        float spacing = _cueRadius * 2f + 1.5f;
        float rowSpacing = spacing * MathF.Sqrt(3f) / 2f;

        for (int row = 0; row < 5; row++)
        {
            for (int column = 0; column <= row; column++)
            {
                int number = RackNumbers[row][column];

                var position = _footSpot
                    + _rackLongDir * (row * rowSpacing)
                    + _rackPerp * ((column - row / 2f) * spacing);

                position = ClampToTable(position, _cueRadius);
                position = PushOutOfPockets(position, _cueRadius);

                Bitmap sprite;
                Color accent;

                if (iconIndex < iconLimit)
                {
                    var icon = pool[iconIndex++];
                    sprite = icon.Sprite;
                    accent = icon.Accent;
                }
                else
                {
                    sprite = _numberSprites[number]!;
                    accent = NumberColor(number);
                }

                _balls.Add(new Ball
                {
                    Position = position,
                    Velocity = Vector2.Zero,
                    Radius = _cueRadius,
                    Mass = 1f,
                    Sprite = sprite,
                    Accent = accent,
                    Number = number
                });
            }
        }
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private Vector2 FindCueSpot()
    {
        if (IsCueSpotFree(_headSpot))
            return _headSpot;

        for (int ring = 1; ring <= 14; ring++)
        {
            int steps = 16 + ring * 4;

            for (int i = 0; i < steps; i++)
            {
                float angle = i / (float)steps * MathF.Tau;
                var point = _headSpot + new Vector2(MathF.Cos(angle), MathF.Sin(angle))
                    * (ring * _cueRadius * 1.35f);

                point = ClampToTable(point, _cueRadius);

                if (IsCueSpotFree(point))
                    return point;
            }
        }

        return _headSpot;
    }

    private bool IsCueSpotFree(Vector2 point)
    {
        foreach (var ball in _balls)
        {
            if (ball.IsCue || ball.Potted)
                continue;

            if (Vector2.Distance(point, ball.Position) < _cueRadius + ball.Radius + 8f)
                return false;
        }

        foreach (var pocket in _pockets)
        {
            if (Vector2.Distance(point, pocket.Center) < pocket.Radius + _cueRadius * 0.8f)
                return false;
        }

        return _table.Contains(point.X, point.Y);
    }

    private void MoveCueBall(Point location)
    {
        if (_cue == null || _phase != Phase.Aiming)
            return;

        var target = new Vector2(location.X, location.Y);
        target = ClampToTable(target, _cueRadius);

        foreach (var pocket in _pockets)
        {
            if (Vector2.Distance(target, pocket.Center) < pocket.Radius + _cueRadius)
                return;
        }

        foreach (var ball in _balls)
        {
            if (ball == _cue)
                continue;

            if (Vector2.Distance(target, ball.Position) < _cueRadius + ball.Radius + 6f)
                return;
        }

        _cue.Position = target;
        _cue.Velocity = Vector2.Zero;
        _cue.Trail.Clear();
        UpdateAim(location);
        ShowToast("白球移动");
    }

    private void UpdateAim(Point location)
    {
        if (_cue == null)
            return;

        _aimPoint = new Vector2(location.X, location.Y);
        var delta = _aimPoint - _cue.Position;

        if (delta.LengthSquared() > 64f)
            _aimDir = Vector2.Normalize(delta);
    }

    private void Shoot()
    {
        if (_cue == null || _cue.Potted || _phase != Phase.Aiming)
            return;

        float power = Math.Max(0.06f, _power);
        float speed = 420f + 2150f * MathF.Pow(power, 1.15f);

        Log.Write($"Shoot power={power:0.00} speed={speed:0.0} dir=({_aimDir.X:0.00},{_aimDir.Y:0.00})");

        _cueStrikeOrigin = _cue.Position;
        _cueStrikeDir = _aimDir;
        _cueStrikePower = power;
        _cueStrikeTimer = CueStrikeDuration;

        _cue.Velocity = _aimDir * speed;
        _phase = Phase.Rolling;
        _shotPots = 0;
        _shots++;
        _charging = false;
        _power = 0;

        if (!_gameClock.IsRunning)
            _gameClock.Start();

        SpawnBurst(_cue.Position + _aimDir * _cue.Radius, 10, Mint, 40f, 180f, 0f);
        AddWave(_cue.Position, _cue.Radius * 2.2f, 0.28f, 2f, Color.FromArgb(120, Mint));
        _flash = Math.Min(0.18f, _flash + 0.08f);
        _flashColor = Mint;
    }

    private void StepPhysics(float dt)
    {
        foreach (var ball in _balls)
        {
            if (ball.Potted)
                continue;

            ball.Position += ball.Velocity * dt;

            float speed = ball.Velocity.Length();
            if (speed > 0.001f)
            {
                float deceleration = (ball.IsCue ? 330f : 300f) * dt;
                float newSpeed = MathF.Max(0f, speed - deceleration);
                ball.Velocity = newSpeed <= 0.01f ? Vector2.Zero : ball.Velocity * (newSpeed / speed);
            }

            if (speed > 35f)
            {
                ball.TrailTimer += dt;
                if (ball.TrailTimer >= 0.028f)
                {
                    ball.TrailTimer = 0f;
                    ball.Trail.Enqueue(ball.Position);
                    while (ball.Trail.Count > 5)
                        ball.Trail.Dequeue();
                }
            }
            else if (speed < 8f)
            {
                ball.Trail.Clear();
            }
        }

        for (int iteration = 0; iteration < 3; iteration++)
        {
            for (int i = 0; i < _balls.Count; i++)
            {
                for (int j = i + 1; j < _balls.Count; j++)
                {
                    var a = _balls[i];
                    var b = _balls[j];

                    if (a.Potted || b.Potted)
                        continue;

                    ResolveBallCollision(a, b, iteration == 0);
                }
            }
        }

        foreach (var ball in _balls)
        {
            if (ball.Potted)
                continue;

            bool potted = false;

            foreach (var pocket in _pockets)
            {
                // 袋口捕获半径必须大于球贴住库边时到袋心的距离（角袋约 R*sqrt(2)），
                // 否则球会被库边挡住，永远进不了角袋。
                float capture = MathF.Max(pocket.Radius * 0.95f, ball.Radius * 1.55f);
                if (Vector2.DistanceSquared(ball.Position, pocket.Center) <= capture * capture)
                {
                    Pot(ball, pocket);
                    potted = true;
                    break;
                }
            }

            if (potted)
                continue;

            ResolveCushions(ball);
            ball.Position = ClampToTable(ball.Position, ball.Radius);
        }
    }

    private void ResolveBallCollision(Ball a, Ball b, bool spawnEffects)
    {
        var delta = b.Position - a.Position;
        float distSq = delta.LengthSquared();
        float sum = a.Radius + b.Radius;

        if (distSq >= sum * sum || distSq < 0.0001f)
            return;

        float dist = MathF.Sqrt(distSq);
        var normal = delta / dist;
        float invSum = a.InvMass + b.InvMass;

        if (invSum <= 0f)
            return;

        float penetration = sum - dist;
        a.Position -= normal * (penetration * a.InvMass / invSum);
        b.Position += normal * (penetration * b.InvMass / invSum);

        float relativeNormal = Vector2.Dot(b.Velocity - a.Velocity, normal);
        if (relativeNormal > 0f)
            return;

        float impulse = -(1f + 0.96f) * relativeNormal / invSum;
        a.Velocity -= normal * (impulse * a.InvMass);
        b.Velocity += normal * (impulse * b.InvMass);

        if (spawnEffects && -relativeNormal > 115f)
        {
            var contact = a.Position + normal * a.Radius;
            SpawnImpact(contact, normal, -relativeNormal, b.Accent, a.Accent);
        }
    }

    private void ResolveCushions(Ball ball)
    {
        float left = _table.Left + ball.Radius;
        float right = _table.Right - ball.Radius;
        float top = _table.Top + ball.Radius;
        float bottom = _table.Bottom - ball.Radius;

        if (ball.Position.X < left)
        {
            ball.Position.X = left;
            if (ball.Velocity.X < 0f)
            {
                float impact = -ball.Velocity.X;
                ball.Velocity.X = impact * 0.82f;
                ball.Velocity.Y *= 0.94f;
                if (impact > 150f)
                    SpawnCushion(new Vector2(_table.Left, ball.Position.Y), new Vector2(1f, 0f), impact, ball.Accent);
            }
        }
        else if (ball.Position.X > right)
        {
            ball.Position.X = right;
            if (ball.Velocity.X > 0f)
            {
                float impact = ball.Velocity.X;
                ball.Velocity.X = -impact * 0.82f;
                ball.Velocity.Y *= 0.94f;
                if (impact > 150f)
                    SpawnCushion(new Vector2(_table.Right, ball.Position.Y), new Vector2(-1f, 0f), impact, ball.Accent);
            }
        }

        if (ball.Position.Y < top)
        {
            ball.Position.Y = top;
            if (ball.Velocity.Y < 0f)
            {
                float impact = -ball.Velocity.Y;
                ball.Velocity.Y = impact * 0.82f;
                ball.Velocity.X *= 0.94f;
                if (impact > 150f)
                    SpawnCushion(new Vector2(ball.Position.X, _table.Top), new Vector2(0f, 1f), impact, ball.Accent);
            }
        }
        else if (ball.Position.Y > bottom)
        {
            ball.Position.Y = bottom;
            if (ball.Velocity.Y > 0f)
            {
                float impact = ball.Velocity.Y;
                ball.Velocity.Y = -impact * 0.82f;
                ball.Velocity.X *= 0.94f;
                if (impact > 150f)
                    SpawnCushion(new Vector2(ball.Position.X, _table.Bottom), new Vector2(0f, -1f), impact, ball.Accent);
            }
        }
    }

    private void Pot(Ball ball, Pocket pocket)
    {
        if (ball.Potted)
            return;

        ball.Potted = true;
        ball.Velocity = Vector2.Zero;
        ball.Trail.Clear();
        pocket.Flash = 1f;

        _pots.Add(new PotAnimation
        {
            Sprite = ball.Sprite,
            Start = ball.Position,
            Pocket = pocket,
            Age = 0f,
            Duration = 0.3f,
            Angle = _rng.NextSingle() * 360f,
            Accent = ball.Accent
        });

        SpawnBurst(pocket.Center, ball.IsCue ? 18 : 28, ball.Accent, 90f, 420f, 80f);
        AddWave(pocket.Center, pocket.Radius * 2.4f, 0.48f, 4f, Color.FromArgb(190, ball.Accent));

        if (ball.IsCue)
        {
            _cuePotted = true;
            _combo = 0;
            _flash = Math.Min(0.3f, _flash + 0.12f);
            _flashColor = Coral;
            ShowToast("白球落袋");
            return;
        }

        _potted++;
        _shotPots++;
        _combo++;
        _lastPocket = pocket;

        if (_combo >= 2)
        {
            _combos.Clear();
            _combos.Add(new ComboPopup
            {
                Pocket = pocket,
                Count = _combo,
                Age = 0f,
                Duration = 1.45f
            });

            AddWave(pocket.Center, pocket.Radius * 3.1f, 0.55f, 6f, Color.FromArgb(230, Gold));
            SpawnBurst(pocket.Center, 22, Gold, 120f, 520f, 120f);
            _flashColor = Gold;
            _flash = Math.Min(0.42f, _flash + 0.16f);
        }
        else
        {
            _flashColor = White;
            _flash = Math.Min(0.25f, _flash + 0.08f);
        }

        if (_potted >= _total)
            Win();
    }

    private void Win()
    {
        if (_won)
            return;

        _won = true;
        _phase = Phase.Won;
        _charging = false;
        _power = 0;
        _gameClock.Stop();

        SpawnConfetti(180);

        if (_lastPocket != null)
        {
            AddWave(_lastPocket.Center, _lastPocket.Radius * 5f, 0.9f, 8f, Color.FromArgb(255, Gold));
            SpawnBurst(_lastPocket.Center, 80, Gold, 200f, 700f, 160f);
        }

        _flash = 0.5f;
        _flashColor = Gold;
    }

    private void OnShotSettled()
    {
        Log.Write($"Shot settled pots={_shotPots} combo={_combo} potted={_potted}");

        if (_shotPots == 0)
            _combo = 0;

        if (_cuePotted)
        {
            _cuePotted = false;
            RespawnCue();
        }

        _phase = Phase.Aiming;
        _charging = false;
        _power = 0;
        _accumulator = 0;
        _lastFrameSeconds = _frameClock.Elapsed.TotalSeconds;
    }

    private void RespawnCue()
    {
        if (_cue == null)
            return;

        _cue.Position = FindCueSpot();
        _cue.Velocity = Vector2.Zero;
        _cue.Potted = false;
        _cue.Trail.Clear();
        AddWave(_cue.Position, _cue.Radius * 3f, 0.4f, 3f, Color.FromArgb(160, Mint));
        ShowToast("白球复位");
    }

    private bool AllStopped()
    {
        foreach (var ball in _balls)
        {
            if (ball.Potted)
                continue;

            if (ball.Velocity.LengthSquared() > 9f)
                return false;
        }

        return true;
    }

    private Vector2 ClampToTable(Vector2 point, float radius)
    {
        float minX = _table.Left + radius;
        float maxX = _table.Right - radius;
        float minY = _table.Top + radius;
        float maxY = _table.Bottom - radius;

        if (maxX < minX)
        {
            minX = maxX = _table.Left + _table.Width / 2f;
        }

        if (maxY < minY)
        {
            minY = maxY = _table.Top + _table.Height / 2f;
        }

        return new Vector2(
            Math.Clamp(point.X, minX, maxX),
            Math.Clamp(point.Y, minY, maxY));
    }

    private Vector2 PushOutOfPockets(Vector2 point, float radius)
    {
        foreach (var pocket in _pockets)
        {
            float minDistance = pocket.Radius + radius * 0.2f;
            var delta = point - pocket.Center;
            float distance = delta.Length();

            if (distance >= minDistance)
                continue;

            if (distance < 0.001f)
                delta = new Vector2(0f, -1f);
            else
                delta /= distance;

            point = pocket.Center + delta * minDistance;
            point = ClampToTable(point, radius);
        }

        return point;
    }

    private void UpdateEffects(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;

            if (p.Life <= 0f)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * dt;
            p.Velocity += new Vector2(0f, p.Gravity * dt);
            p.Velocity *= 0.985f;
        }

        for (int i = _waves.Count - 1; i >= 0; i--)
        {
            var wave = _waves[i];
            wave.Age += dt;
            if (wave.Age >= wave.Duration)
                _waves.RemoveAt(i);
        }

        for (int i = _pots.Count - 1; i >= 0; i--)
        {
            var pot = _pots[i];
            pot.Age += dt;
            if (pot.Age >= pot.Duration)
                _pots.RemoveAt(i);
        }

        for (int i = _combos.Count - 1; i >= 0; i--)
        {
            var combo = _combos[i];
            combo.Age += dt;
            if (combo.Age >= combo.Duration)
                _combos.RemoveAt(i);
        }

        foreach (var pocket in _pockets)
            pocket.Flash = MathF.Max(0f, pocket.Flash - dt * 2.8f);

        _flash = MathF.Max(0f, _flash - dt * 2.6f);
        _toastTime = MathF.Max(0f, _toastTime - dt);
    }

    private void SpawnBurst(Vector2 center, int count, Color color, float minSpeed, float maxSpeed, float gravity)
    {
        if (_particles.Count > 520)
            return;

        for (int i = 0; i < count && _particles.Count < 560; i++)
        {
            float angle = _rng.NextSingle() * MathF.Tau;
            float speed = minSpeed + _rng.NextSingle() * (maxSpeed - minSpeed);
            float life = 0.35f + _rng.NextSingle() * 0.55f;

            _particles.Add(new Particle
            {
                Position = center,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
                Life = life,
                MaxLife = life,
                Size = 1.4f + _rng.NextSingle() * 3.2f,
                Gravity = gravity,
                Color = color
            });
        }
    }

    private void SpawnImpact(Vector2 contact, Vector2 normal, float speed, Color colorA, Color colorB)
    {
        if (_particles.Count > 480)
            return;

        int count = Math.Clamp((int)(speed / 90f), 3, 9);

        for (int i = 0; i < count; i++)
        {
            var tangent = new Vector2(-normal.Y, normal.X);
            var direction = Vector2.Normalize(normal + tangent * ((_rng.NextSingle() - 0.5f) * 1.6f));
            float magnitude = 40f + _rng.NextSingle() * MathF.Min(260f, speed * 0.7f);
            float life = 0.18f + _rng.NextSingle() * 0.3f;

            _particles.Add(new Particle
            {
                Position = contact,
                Velocity = direction * magnitude,
                Life = life,
                MaxLife = life,
                Size = 1.2f + _rng.NextSingle() * 2.2f,
                Gravity = 0f,
                Color = _rng.Next(2) == 0 ? colorA : colorB
            });
        }

        if (speed > 260f)
            AddWave(contact, 18f, 0.24f, 2f, Color.FromArgb(110, White));
    }

    private void SpawnCushion(Vector2 contact, Vector2 normal, float speed, Color color)
    {
        if (_particles.Count > 500)
            return;

        int count = Math.Clamp((int)(speed / 130f), 2, 6);

        for (int i = 0; i < count; i++)
        {
            var tangent = new Vector2(-normal.Y, normal.X);
            var direction = Vector2.Normalize(normal + tangent * ((_rng.NextSingle() - 0.5f) * 2f));
            float magnitude = 30f + _rng.NextSingle() * 120f;
            float life = 0.15f + _rng.NextSingle() * 0.25f;

            _particles.Add(new Particle
            {
                Position = contact,
                Velocity = direction * magnitude,
                Life = life,
                MaxLife = life,
                Size = 1f + _rng.NextSingle() * 2f,
                Gravity = 0f,
                Color = Color.FromArgb(220, color)
            });
        }
    }

    private void AddWave(Vector2 center, float maxRadius, float duration, float width, Color color)
    {
        if (_waves.Count > 80)
            return;

        _waves.Add(new Shockwave
        {
            Center = center,
            Age = 0f,
            Duration = duration,
            MaxRadius = maxRadius,
            Width = width,
            Color = color
        });
    }

    private void SpawnConfetti(int count)
    {
        for (int i = 0; i < count && _particles.Count < 700; i++)
        {
            float life = 2.4f + _rng.NextSingle() * 2.2f;
            var color = BallAccents[_rng.Next(BallAccents.Length)];

            _particles.Add(new Particle
            {
                Position = new Vector2(
                    _rng.NextSingle() * Math.Max(1, ClientSize.Width),
                    -20f - _rng.NextSingle() * 160f),
                Velocity = new Vector2(
                    (_rng.NextSingle() - 0.5f) * 160f,
                    70f + _rng.NextSingle() * 150f),
                Life = life,
                MaxLife = life,
                Size = 2f + _rng.NextSingle() * 3.5f,
                Gravity = 170f,
                Color = color
            });
        }
    }

    private void ShowToast(string text)
    {
        _toast = text;
        _toastTime = 1.25f;
    }

    // ---------- Drawing ----------

    private void DrawBackground(Graphics g)
    {
        var background = _snapshot.Background;

        if (background.Width == ClientSize.Width && background.Height == ClientSize.Height)
            g.DrawImageUnscaled(background, 0, 0);
        else
            g.DrawImage(background, ClientRectangle);

        using (var tint = new SolidBrush(Color.FromArgb(16, 0, 0, 0)))
            g.FillRectangle(tint, ClientRectangle);

        if (_vignette.Width == ClientSize.Width && _vignette.Height == ClientSize.Height)
            g.DrawImageUnscaled(_vignette, 0, 0);
        else
            g.DrawImage(_vignette, ClientRectangle);
    }

    private void BuildBoard()
    {
        _board = Keep(new Bitmap(
            Math.Max(1, ClientSize.Width),
            Math.Max(1, ClientSize.Height),
            PixelFormat.Format32bppPArgb));

        using var g = Graphics.FromImage(_board);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;

        DrawBackground(g);
        DrawTable(g);

        foreach (var pocket in _pockets)
        {
            g.DrawImage(
                _pocketSprite,
                pocket.Center.X - _pocketSprite.Width / 2f,
                pocket.Center.Y - _pocketSprite.Height / 2f);
        }

        Log.Write("GameForm: static board rendered");
    }

    private void DrawTable(Graphics g)
    {
        var tableRect = new RectangleF(
            _table.X - _ballDiameter * 0.16f,
            _table.Y - _ballDiameter * 0.16f,
            _table.Width + _ballDiameter * 0.32f,
            _table.Height + _ballDiameter * 0.32f);

        using var path = RoundedRect(tableRect, 34f);

        using (var felt = new SolidBrush(Color.FromArgb(16, 4, 10, 21)))
            g.FillPath(felt, path);

        using (var glow = new Pen(Color.FromArgb(26, Mint), 16f))
            g.DrawPath(glow, path);

        using (var line = new Pen(Color.FromArgb(72, Mint), 1.4f))
            g.DrawPath(line, path);

        // 发球线（head string）与三个点位，使用真实台球桌的方位。
        using (var headString = new Pen(Color.FromArgb(20, White), 1f))
        {
            headString.DashStyle = DashStyle.Dash;
            headString.DashPattern = new[] { 3f, 9f };

            if (_table.Width >= _table.Height)
            {
                g.DrawLine(
                    headString,
                    _headSpot.X,
                    _table.Top + 8,
                    _headSpot.X,
                    _table.Bottom - 8);
            }
            else
            {
                g.DrawLine(
                    headString,
                    _table.Left + 8,
                    _headSpot.Y,
                    _table.Right - 8,
                    _headSpot.Y);
            }
        }

        using (var spot = new SolidBrush(Color.FromArgb(40, White)))
        {
            g.FillEllipse(spot, _headSpot.X - 3f, _headSpot.Y - 3f, 6f, 6f);
            g.FillEllipse(spot, _footSpot.X - 3f, _footSpot.Y - 3f, 6f, 6f);
            g.FillEllipse(
                spot,
                _table.Left + _table.Width / 2f - 3f,
                _table.Top + _table.Height / 2f - 3f,
                6f,
                6f);
        }
    }

    private void DrawPocketEffects(Graphics g)
    {
        foreach (var pocket in _pockets)
        {
            float nearest = float.MaxValue;

            foreach (var ball in _balls)
            {
                if (ball.Potted)
                    continue;

                nearest = MathF.Min(nearest, Vector2.Distance(ball.Position, pocket.Center));
            }

            float near = Math.Clamp(1f - nearest / (pocket.Radius * 3.4f), 0f, 1f);

            if (near > 0.01f)
            {
                float size = _pocketGlow.Width * (0.55f + near * 0.18f);
                DrawImageWithAlpha(
                    g,
                    _pocketGlow,
                    new RectangleF(
                        pocket.Center.X - size / 2f,
                        pocket.Center.Y - size / 2f,
                        size,
                        size),
                    0.22f + near * 0.4f);
            }

            if (pocket.Flash > 0.01f)
            {
                float t = 1f - pocket.Flash;
                float radius = pocket.Radius * (1f + t * 2.2f);
                int alpha = (int)(pocket.Flash * 210f);
                using var pen = new Pen(Color.FromArgb(alpha, Gold), 2.5f);
                g.DrawEllipse(
                    pen,
                    pocket.Center.X - radius,
                    pocket.Center.Y - radius,
                    radius * 2f,
                    radius * 2f);
            }

            if (_lastPocket == pocket && _combo >= 2)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin(_time * 6f);
                int alpha = (int)(80 + pulse * 90f);
                using var pen = new Pen(Color.FromArgb(alpha, Gold), 1.4f);
                g.DrawEllipse(
                    pen,
                    pocket.Center.X - pocket.Radius - 7,
                    pocket.Center.Y - pocket.Radius - 7,
                    (pocket.Radius + 7) * 2,
                    (pocket.Radius + 7) * 2);
            }
        }
    }

    private void DrawShadows(Graphics g)
    {
        foreach (var ball in _balls)
        {
            if (ball.Potted)
                continue;

            // 静止的图标球不需要每帧画柔和阴影，只有白球和运动中的球需要。
            if (!ball.IsCue && ball.Velocity.LengthSquared() < 400f)
                continue;

            float size = ball.Radius * 2.4f;
            g.DrawImage(
                _shadow,
                ball.Position.X - size / 2f + ball.Radius * 0.16f,
                ball.Position.Y - size / 2f + ball.Radius * 0.26f,
                size,
                size);
        }
    }

    private void DrawTrails(Graphics g)
    {
        foreach (var ball in _balls)
        {
            if (ball.Potted || ball.Trail.Count == 0)
                continue;

            int index = 0;
            foreach (var position in ball.Trail)
            {
                float t = (index + 1f) / (ball.Trail.Count + 1f);
                float radius = ball.Radius * (0.28f + t * 0.48f);
                int alpha = (int)(t * 42f);

                using var brush = new SolidBrush(Color.FromArgb(alpha, ball.Accent));
                g.FillEllipse(
                    brush,
                    position.X - radius,
                    position.Y - radius,
                    radius * 2f,
                    radius * 2f);

                index++;
            }
        }
    }

    private void DrawBalls(Graphics g)
    {
        foreach (var ball in _balls)
        {
            if (ball.Potted)
                continue;

            if (ball.IsCue)
            {
                float glow = _cueGlow.Width * 0.9f;
                DrawImageWithAlpha(
                    g,
                    _cueGlow,
                    new RectangleF(
                        ball.Position.X - glow / 2f,
                        ball.Position.Y - glow / 2f,
                        glow,
                        glow),
                    0.5f);
            }

            // 球贴图和球直径是 1:1 的，用 DrawImageUnscaled 可以明显降低 GDI+ 开销。
            g.DrawImageUnscaled(
                ball.Sprite,
                (int)MathF.Round(ball.Position.X - ball.Radius),
                (int)MathF.Round(ball.Position.Y - ball.Radius));

            if (!ball.IsCue)
                DrawNumberBadge(g, ball);
        }
    }

    private void DrawNumberBadge(Graphics g, Ball ball)
    {
        float radius = ball.Radius * 0.36f;
        float cx = ball.Position.X;
        float cy = ball.Position.Y - ball.Radius * 0.55f;

        using (var fill = new SolidBrush(Color.FromArgb(244, 250, 252, 255)))
            g.FillEllipse(fill, cx - radius, cy - radius, radius * 2f, radius * 2f);

        using (var border = new Pen(
                   Color.FromArgb(205, 10, 16, 28),
                   Math.Max(1f, ball.Radius * 0.035f)))
            g.DrawEllipse(border, cx - radius, cy - radius, radius * 2f, radius * 2f);

        string text = ball.Number.ToString();
        var size = g.MeasureString(text, _numberFont);

        using var textBrush = new SolidBrush(Color.FromArgb(250, 12, 18, 30));
        g.DrawString(
            text,
            _numberFont,
            textBrush,
            cx - size.Width / 2f,
            cy - size.Height / 2f);
    }

    private void DrawPotAnimations(Graphics g)
    {
        foreach (var pot in _pots)
        {
            float t = Math.Clamp(pot.Age / pot.Duration, 0f, 1f);
            float eased = t * t;
            float scale = 1f - eased * 0.9f;
            float alpha = 1f - eased * 0.95f;
            float size = _ballDiameter * MathF.Max(0.08f, scale);
            var position = Vector2.Lerp(pot.Start, pot.Pocket.Center, eased);

            var state = g.Save();
            g.TranslateTransform(position.X, position.Y);
            g.RotateTransform(pot.Angle + t * 420f);

            using (var attributes = AlphaAttributes(alpha))
            {
                g.DrawImage(
                    pot.Sprite,
                    new Rectangle(
                        (int)MathF.Round(-size / 2f),
                        (int)MathF.Round(-size / 2f),
                        Math.Max(1, (int)MathF.Round(size)),
                        Math.Max(1, (int)MathF.Round(size))),
                    0f,
                    0f,
                    pot.Sprite.Width,
                    pot.Sprite.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }

            g.Restore(state);
        }
    }

    private void DrawParticles(Graphics g)
    {
        foreach (var particle in _particles)
        {
            float t = Math.Clamp(particle.Life / particle.MaxLife, 0f, 1f);
            int alpha = (int)(t * 220f);
            var color = Color.FromArgb(alpha, particle.Color);
            float size = particle.Size * (0.5f + t * 0.5f);

            if (particle.Gravity > 0f)
            {
                using var brush = new SolidBrush(color);
                g.FillRectangle(
                    brush,
                    particle.Position.X - size * 0.5f,
                    particle.Position.Y - size * 1.1f,
                    size,
                    size * 2.2f);
            }
            else
            {
                using var pen = new Pen(color, MathF.Max(1f, size * 0.7f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                var tail = particle.Position - particle.Velocity * 0.022f;
                g.DrawLine(pen, tail.X, tail.Y, particle.Position.X, particle.Position.Y);
            }
        }
    }

    private void DrawShockwaves(Graphics g)
    {
        foreach (var wave in _waves)
        {
            float t = Math.Clamp(wave.Age / wave.Duration, 0f, 1f);
            float eased = 1f - MathF.Pow(1f - t, 3f);
            float radius = wave.MaxRadius * eased;
            int alpha = (int)((1f - t) * 190f);

            using (var pen = new Pen(Color.FromArgb(alpha, wave.Color), wave.Width))
                g.DrawEllipse(
                    pen,
                    wave.Center.X - radius,
                    wave.Center.Y - radius,
                    radius * 2f,
                    radius * 2f);

            if (wave.Width > 3f)
            {
                using var second = new Pen(Color.FromArgb(alpha / 3, wave.Color), 1f);
                g.DrawEllipse(
                    second,
                    wave.Center.X - radius * 1.12f,
                    wave.Center.Y - radius * 1.12f,
                    radius * 2.24f,
                    radius * 2.24f);
            }
        }
    }

    private void DrawAimAssist(Graphics g)
    {
        if (_cue == null || _cue.Potted)
            return;

        var origin = _cue.Position;
        var direction = _aimDir;
        var hit = FindFirstHit(origin, direction);

        if (!_assist)
            return;

        var start = origin + direction * (_cue.Radius + 3f);
        var end = origin + direction * hit.Distance;

        using (var glow = new Pen(Color.FromArgb(30, Mint), 7f))
            g.DrawLine(glow, start.X, start.Y, end.X, end.Y);

        using (var line = new Pen(Color.FromArgb(205, Mint), 1.5f))
        {
            line.DashStyle = DashStyle.Custom;
            line.DashPattern = new[] { 5f, 7f };
            line.DashOffset = -(_time * 22f) % 12f;
            g.DrawLine(line, start.X, start.Y, end.X, end.Y);
        }

        if (hit.Ball != null)
        {
            float radius = _cue.Radius;

            using (var ghost = new Pen(Color.FromArgb(160, White), 1.2f))
                g.DrawEllipse(
                    ghost,
                    end.X - radius,
                    end.Y - radius,
                    radius * 2f,
                    radius * 2f);

            using (var ghostFill = new SolidBrush(Color.FromArgb(18, Mint)))
                g.FillEllipse(
                    ghostFill,
                    end.X - radius,
                    end.Y - radius,
                    radius * 2f,
                    radius * 2f);

            var objectDirection = Vector2.Normalize(hit.Ball.Position - end);
            var objectStart = hit.Ball.Position + objectDirection * (hit.Ball.Radius + 3f);
            var objectEnd = hit.Ball.Position + objectDirection * (hit.Ball.Radius + 95f);

            using (var objectLine = new Pen(Color.FromArgb(180, Gold), 2f))
                g.DrawLine(objectLine, objectStart.X, objectStart.Y, objectEnd.X, objectEnd.Y);

            var tangent = new Vector2(-objectDirection.Y, objectDirection.X);
            var cueEnd = end + tangent * 55f;

            using (var cueLine = new Pen(Color.FromArgb(90, Ice), 1.2f))
                g.DrawLine(cueLine, end.X, end.Y, cueEnd.X, cueEnd.Y);
        }
        else
        {
            using var dot = new SolidBrush(Color.FromArgb(150, Mint));
            g.FillEllipse(dot, end.X - 3f, end.Y - 3f, 6f, 6f);
        }
    }

    private AimHit FindFirstHit(Vector2 origin, Vector2 direction)
    {
        float best = WallDistance(origin, direction);
        Ball? hitBall = null;

        foreach (var ball in _balls)
        {
            if (ball.Potted || ball.IsCue)
                continue;

            var offset = origin - ball.Position;
            float radius = _cue.Radius + ball.Radius;
            float b = Vector2.Dot(offset, direction);
            float c = Vector2.Dot(offset, offset) - radius * radius;
            float discriminant = b * b - c;

            if (discriminant < 0f)
                continue;

            float t = -b - MathF.Sqrt(discriminant);
            if (t <= 0.01f || t >= best)
                continue;

            best = t;
            hitBall = ball;
        }

        return new AimHit(best, hitBall);
    }

    private float WallDistance(Vector2 origin, Vector2 direction)
    {
        float best = 5000f;
        float left = _table.Left + _cue.Radius;
        float right = _table.Right - _cue.Radius;
        float top = _table.Top + _cue.Radius;
        float bottom = _table.Bottom - _cue.Radius;

        if (direction.X < -0.0001f)
            best = MathF.Min(best, (left - origin.X) / direction.X);
        else if (direction.X > 0.0001f)
            best = MathF.Min(best, (right - origin.X) / direction.X);

        if (direction.Y < -0.0001f)
            best = MathF.Min(best, (top - origin.Y) / direction.Y);
        else if (direction.Y > 0.0001f)
            best = MathF.Min(best, (bottom - origin.Y) / direction.Y);

        return MathF.Max(2f, best);
    }

    private void DrawCueStick(Graphics g)
    {
        if (_cue == null || _cue.Potted)
            return;

        bool striking = _cueStrikeTimer > 0f;
        var direction = striking ? _cueStrikeDir : _aimDir;
        var origin = striking ? _cueStrikeOrigin : _cue.Position;

        if (direction.LengthSquared() < 0.0001f)
            return;

        var perpendicular = new Vector2(-direction.Y, direction.X);
        float pullback = _charging ? 14f + _power * 95f : 14f;

        if (striking)
        {
            float t = 1f - Math.Clamp(_cueStrikeTimer / CueStrikeDuration, 0f, 1f);
            float startPull = 14f + _cueStrikePower * 95f;
            float strikeT = MathF.Min(1f, t * 2.4f);
            pullback = startPull + (-_cue.Radius * 0.55f - startPull) * strikeT;
        }

        float tipDistance = _cue.Radius + 7f + pullback;

        var tip = origin - direction * tipDistance;
        var back = -direction;
        float length = MathF.Min(
            _ballDiameter * 3.8f,
            AvailableRayLength(tip, back, 10f));

        if (length < 30f)
            return;

        var butt = tip + back * length;

        float tipHalfWidth = MathF.Max(2.4f, _ballDiameter * 0.045f);
        float buttHalfWidth = MathF.Max(7f, _ballDiameter * 0.15f);

        var points = new[]
        {
            new PointF(tip.X + perpendicular.X * tipHalfWidth, tip.Y + perpendicular.Y * tipHalfWidth),
            new PointF(butt.X + perpendicular.X * buttHalfWidth, butt.Y + perpendicular.Y * buttHalfWidth),
            new PointF(butt.X - perpendicular.X * buttHalfWidth, butt.Y - perpendicular.Y * buttHalfWidth),
            new PointF(tip.X - perpendicular.X * tipHalfWidth, tip.Y - perpendicular.Y * tipHalfWidth)
        };

        using (var wood = new LinearGradientBrush(
                   new PointF(tip.X, tip.Y),
                   new PointF(butt.X, butt.Y),
                   Color.FromArgb(225, 205, 168, 116),
                   Color.FromArgb(235, 64, 40, 30)))
            g.FillPolygon(wood, points);

        using (var outline = new Pen(Color.FromArgb(120, 0, 0, 0), 1f))
            g.DrawPolygon(outline, points);

        using (var highlight = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
            g.DrawLine(highlight, tip.X, tip.Y, butt.X, butt.Y);

        using (var ferrule = new Pen(Color.FromArgb(245, 235, 248, 255), 2.2f))
        {
            var a = tip + direction * 4f;
            var b = tip + direction * 10f;
            g.DrawLine(ferrule, a.X, a.Y, b.X, b.Y);
        }

        using (var chalk = new SolidBrush(Color.FromArgb(230, 96, 178, 220)))
            g.FillEllipse(
                chalk,
                tip.X - tipHalfWidth * 1.4f,
                tip.Y - tipHalfWidth * 1.4f,
                tipHalfWidth * 2.8f,
                tipHalfWidth * 2.8f);
    }

    private float AvailableRayLength(Vector2 origin, Vector2 direction, float margin)
    {
        float best = float.MaxValue;

        if (direction.X > 0.0001f)
            best = MathF.Min(best, (ClientSize.Width - margin - origin.X) / direction.X);
        else if (direction.X < -0.0001f)
            best = MathF.Min(best, (margin - origin.X) / direction.X);

        if (direction.Y > 0.0001f)
            best = MathF.Min(best, (ClientSize.Height - margin - origin.Y) / direction.Y);
        else if (direction.Y < -0.0001f)
            best = MathF.Min(best, (margin - origin.Y) / direction.Y);

        return MathF.Max(0f, best);
    }

    private void DrawPowerRing(Graphics g)
    {
        if (!_charging || _cue == null || _cue.Potted)
            return;

        float radius = _cue.Radius + 12f;
        var rect = new RectangleF(
            _cue.Position.X - radius,
            _cue.Position.Y - radius,
            radius * 2f,
            radius * 2f);

        using (var background = new Pen(Color.FromArgb(45, White), 5f))
            g.DrawEllipse(background, rect);

        var color = _power < 0.55f
            ? Lerp(Mint, Gold, _power / 0.55f)
            : Lerp(Gold, Coral, (_power - 0.55f) / 0.45f);

        float sweep = Math.Clamp(_power * 360f, 1f, 360f);

        using (var glow = new Pen(Color.FromArgb(50, color), 12f))
            g.DrawArc(glow, rect, -90f, sweep);

        using (var pen = new Pen(Color.FromArgb(245, color), 5f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawArc(pen, rect, -90f, sweep);
        }
    }

    private void DrawComboPopups(Graphics g)
    {
        foreach (var combo in _combos)
        {
            float t = Math.Clamp(combo.Age / combo.Duration, 0f, 1f);
            float alpha = t < 0.64f ? 1f : 1f - (t - 0.64f) / 0.36f;
            float rise = 46f * (1f - MathF.Pow(1f - t, 3f));

            float x = Math.Clamp(combo.Pocket.Center.X, 92f, ClientSize.Width - 92f);
            float y = combo.Pocket.Center.Y - _pocketRadius - 44f - rise;

            if (y < 118f)
                y = combo.Pocket.Center.Y + _pocketRadius + 58f + rise;

            float pop = MathF.Min(1f, t * 6f);
            float scale = 0.64f + 0.51f * MathF.Sin(pop * MathF.PI * 0.5f);

            if (t > 0.18f)
                scale = 1.15f - 0.15f * MathF.Min(1f, (t - 0.18f) * 3.5f);

            float glowSize = _goldGlow.Width * 0.75f;
            DrawImageWithAlpha(
                g,
                _goldGlow,
                new RectangleF(
                    combo.Pocket.Center.X - glowSize / 2f,
                    combo.Pocket.Center.Y - glowSize / 2f,
                    glowSize,
                    glowSize),
                alpha * 0.55f);

            var state = g.Save();
            g.TranslateTransform(x, y);
            g.ScaleTransform(scale, scale);

            string number = "×" + combo.Count;
            var numberSize = g.MeasureString(number, _comboFont);
            var captionSize = g.MeasureString("连击", _captionFont);

            float pillWidth = MathF.Max(numberSize.Width, captionSize.Width) + 42f;
            float pillHeight = numberSize.Height + captionSize.Height + 18f;
            var pill = new RectangleF(
                -pillWidth / 2f,
                -numberSize.Height / 2f - captionSize.Height - 8f,
                pillWidth,
                pillHeight);

            DrawGlassPanel(g, pill, 18f, (int)(175 * alpha));

            using (var shadow = new SolidBrush(Color.FromArgb((int)(160 * alpha), 0, 0, 0)))
                g.DrawString(number, _comboFont, shadow, -numberSize.Width / 2f + 4f, -numberSize.Height / 2f + 5f);

            using (var fill = new SolidBrush(Color.FromArgb((int)(255 * alpha), 255, 237, 191)))
                g.DrawString(number, _comboFont, fill, -numberSize.Width / 2f, -numberSize.Height / 2f);

            using (var caption = new SolidBrush(Color.FromArgb((int)(230 * alpha), Mint)))
            {
                g.DrawString(
                    "连击",
                    _captionFont,
                    caption,
                    -captionSize.Width / 2f,
                    -numberSize.Height / 2f - captionSize.Height - 2f);
            }

            g.Restore(state);
        }
    }

    private void DrawHud(Graphics g)
    {
        var leftPanel = new RectangleF(22f, 18f, 392f, 66f);
        DrawGlassPanel(g, leftPanel, 18f, 240);

        using (var accent = new SolidBrush(Color.FromArgb(230, Mint)))
            g.FillRectangle(accent, leftPanel.Left + 16f, leftPanel.Top + 16f, 3f, leftPanel.Height - 32f);

        var elapsed = _gameClock.Elapsed;
        string time = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";

        using (var caption = new SolidBrush(Color.FromArgb(190, Ice)))
        {
            g.DrawString("时间", _tinyFont, caption, leftPanel.Left + 32f, leftPanel.Top + 11f);
            g.DrawString("进球", _tinyFont, caption, leftPanel.Left + 176f, leftPanel.Top + 11f);
            g.DrawString("连击", _tinyFont, caption, leftPanel.Left + 292f, leftPanel.Top + 11f);
        }

        using (var value = new SolidBrush(Color.FromArgb(240, White)))
        {
            g.DrawString(time, _hudFont, value, leftPanel.Left + 30f, leftPanel.Top + 28f);
            g.DrawString($"{_potted} / {_total}", _hudFont, value, leftPanel.Left + 174f, leftPanel.Top + 28f);
        }

        using (var comboBrush = new SolidBrush(_combo >= 2 ? Gold : Color.FromArgb(170, White)))
            g.DrawString(_combo >= 2 ? $"×{_combo}" : "—", _hudFont, comboBrush, leftPanel.Left + 292f, leftPanel.Top + 28f);

        var rightPanel = new RectangleF(ClientSize.Width - 354f, 18f, 332f, 46f);
        DrawGlassPanel(g, rightPanel, 14f, 225);

        using (var hint = new SolidBrush(Color.FromArgb(195, White)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            g.DrawString("Esc 退出    R 重开    F1 辅助线", _smallFont, hint, rightPanel, format);

        var bottomPanel = new RectangleF(
            ClientSize.Width / 2f - 290f,
            ClientSize.Height - 52f,
            580f,
            34f);
        DrawGlassPanel(g, bottomPanel, 12f, 215);

        using (var hint = new SolidBrush(Color.FromArgb(185, White)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            g.DrawString("按住左键蓄力 · 松开击球 · 右键移动白球", _smallFont, hint, bottomPanel, format);
    }

    private void DrawToast(Graphics g)
    {
        if (_toastTime <= 0f || string.IsNullOrEmpty(_toast))
            return;

        float alpha = Math.Clamp(_toastTime / 0.3f, 0f, 1f);
        var textSize = g.MeasureString(_toast, _smallFont);
        float width = Math.Max(120f, textSize.Width + 38f);
        float height = textSize.Height + 16f;
        var rect = new RectangleF(
            ClientSize.Width / 2f - width / 2f,
            ClientSize.Height - 110f,
            width,
            height);

        DrawGlassPanel(g, rect, 12f, (int)(195 * alpha));

        using (var brush = new SolidBrush(Color.FromArgb((int)(235 * alpha), White)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            g.DrawString(_toast, _smallFont, brush, rect, format);
    }

    private void DrawFlash(Graphics g)
    {
        if (_flash <= 0.001f)
            return;

        int alpha = (int)(_flash * 150f);
        using var brush = new SolidBrush(Color.FromArgb(alpha, _flashColor));
        g.FillRectangle(brush, ClientRectangle);
    }

    private void DrawWinCard(Graphics g)
    {
        float width = 510f;
        float height = 300f;
        var rect = new RectangleF(
            ClientSize.Width / 2f - width / 2f,
            ClientSize.Height / 2f - height / 2f,
            width,
            height);

        float glowSize = _goldGlow.Width * 1.2f;
        DrawImageWithAlpha(
            g,
            _goldGlow,
            new RectangleF(
                rect.Left + rect.Width / 2f - glowSize / 2f,
                rect.Top + rect.Height / 2f - glowSize / 2f,
                glowSize,
                glowSize),
            0.55f);

        DrawGlassPanel(g, rect, 26f, 225);

        using (var gold = new SolidBrush(Color.FromArgb(235, Gold)))
            g.FillRectangle(gold, rect.Left + 36f, rect.Top + 30f, rect.Width - 72f, 3f);

        using (var title = new SolidBrush(Color.FromArgb(250, White)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center })
            g.DrawString("清 台", _titleFont, title, new RectangleF(rect.Left, rect.Top + 48f, rect.Width, 70f), format);

        using (var sub = new SolidBrush(Color.FromArgb(220, Mint)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center })
            g.DrawString("ALL POTTED", _captionFont, sub, new RectangleF(rect.Left, rect.Top + 116f, rect.Width, 24f), format);

        var elapsed = _gameClock.Elapsed;
        string time = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";

        using (var stat = new SolidBrush(Color.FromArgb(235, White)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center })
            g.DrawString(
                $"用时 {time}    出杆 {_shots}    进球 {_potted} / {_total}",
                _smallFont,
                stat,
                new RectangleF(rect.Left, rect.Top + 158f, rect.Width, 30f),
                format);

        using (var divider = new Pen(Color.FromArgb(50, White), 1f))
            g.DrawLine(
                divider,
                rect.Left + 70f,
                rect.Top + 204f,
                rect.Right - 70f,
                rect.Top + 204f);

        using (var hint = new SolidBrush(Color.FromArgb(210, White)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center })
            g.DrawString(
                "R 再来一局        Esc 退出",
                _hudFont,
                hint,
                new RectangleF(rect.Left, rect.Top + 224f, rect.Width, 40f),
                format);
    }

    private void DrawPauseCard(Graphics g)
    {
        using (var dim = new SolidBrush(Color.FromArgb(150, 3, 6, 12)))
            g.FillRectangle(dim, ClientRectangle);

        float width = 430f;
        float height = 190f;
        var rect = new RectangleF(
            ClientSize.Width / 2f - width / 2f,
            ClientSize.Height / 2f - height / 2f,
            width,
            height);

        DrawGlassPanel(g, rect, 24f, 225);

        using (var title = new SolidBrush(Color.FromArgb(245, White)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center })
            g.DrawString("已 暂 停", _titleFont, title, new RectangleF(rect.Left, rect.Top + 38f, rect.Width, 70f), format);

        using (var hint = new SolidBrush(Color.FromArgb(205, Mint)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center })
            g.DrawString("点击窗口继续 · Esc 退出", _smallFont, hint, new RectangleF(rect.Left, rect.Top + 116f, rect.Width, 34f), format);
    }

    private void DrawGlassPanel(Graphics g, RectangleF rect, float radius, int alpha)
    {
        using var path = RoundedRect(rect, radius);

        using (var fill = new LinearGradientBrush(
                   new PointF(rect.Left, rect.Top),
                   new PointF(rect.Left, rect.Bottom),
                   Color.FromArgb(alpha, 18, 30, 50),
                   Color.FromArgb(alpha, 6, 11, 21)))
            g.FillPath(fill, path);

        using (var border = new Pen(Color.FromArgb(70, Mint), 1f))
            g.DrawPath(border, path);

        using (var highlight = new Pen(Color.FromArgb(36, White), 1f))
            g.DrawLine(highlight, rect.Left + radius, rect.Top + 0.8f, rect.Right - radius, rect.Top + 0.8f);
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        float r = MathF.Min(radius, MathF.Min(rect.Width, rect.Height) / 2f);
        float d = r * 2f;
        var path = new GraphicsPath();

        path.AddArc(rect.Left, rect.Top, d, d, 180f, 90f);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270f, 90f);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();

        return path;
    }

    private static void DrawImageWithAlpha(Graphics g, Image image, RectangleF destination, float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        if (alpha <= 0.001f)
            return;

        using var attributes = AlphaAttributes(alpha);
        g.DrawImage(
            image,
            Rectangle.Round(destination),
            0f,
            0f,
            image.Width,
            image.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private static ImageAttributes AlphaAttributes(float alpha)
    {
        var matrix = new ColorMatrix
        {
            Matrix00 = 1f,
            Matrix11 = 1f,
            Matrix22 = 1f,
            Matrix33 = Math.Clamp(alpha, 0f, 1f),
            Matrix44 = 1f
        };

        var attributes = new ImageAttributes();
        attributes.SetColorMatrix(matrix);
        return attributes;
    }

    private static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    private Bitmap Keep(Bitmap bitmap)
    {
        _owned.Add(bitmap);
        return bitmap;
    }

    private readonly record struct AimHit(float Distance, Ball? Ball);
}
