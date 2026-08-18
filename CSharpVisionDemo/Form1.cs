using System.Diagnostics;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace CSharpVisionDemo;

public partial class Form1 : Form
{
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private YoloDetector? _yolo;
    private PoseDetector? _pose;
    private HandDetector? _hand;

    public Form1()
    {
        InitializeComponent();
        DoubleBuffered = true;
        bottomPanel.Resize += (_, _) =>
        {
            lblStatus.Width = Math.Max(120, bottomPanel.ClientSize.Width - 240);
        };
    }

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        if (_loopTask is { IsCompleted: false })
            return;

        btnStart.Enabled = false;
        btnStop.Enabled = true;
        lblStatus.Text = "正在启动摄像头并加载模型…";

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loopTask = Task.Run(() => CaptureLoop(token), token);

        try
        {
            await _loopTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowStatus($"运行失败: {ex.Message}");
        }
        finally
        {
            if (!IsDisposed)
            {
                btnStart.Enabled = true;
                btnStop.Enabled = false;
            }
        }
    }

    private async void BtnStop_Click(object? sender, EventArgs e)
    {
        await StopCaptureAsync();
    }

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _cts?.Cancel();
        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        catch (OperationCanceledException)
        {
        }

        _yolo?.Dispose();
        _pose?.Dispose();
        _hand?.Dispose();
    }

    private async Task StopCaptureAsync()
    {
        if (_cts is null)
            return;

        _cts.Cancel();
        btnStop.Enabled = false;
        try
        {
            if (_loopTask is not null)
                await _loopTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CaptureLoop(CancellationToken token)
    {
        EnsureDetectors();

        using var capture = new VideoCapture(0);
        if (!capture.IsOpened)
        {
            ShowStatus("无法打开默认摄像头（设备 0）。");
            return;
        }

        capture.Set(CapProp.FrameWidth, 1280);
        capture.Set(CapProp.FrameHeight, 720);

        using var frame = new Mat();
        var fpsWatch = Stopwatch.StartNew();
        var frames = 0;
        var fps = 0.0;
        var inferWatch = new Stopwatch();

        while (!token.IsCancellationRequested)
        {
            if (!capture.Read(frame) || frame.IsEmpty)
            {
                Thread.Sleep(10);
                continue;
            }

            inferWatch.Restart();
            IReadOnlyList<YoloDetection> detections = Array.Empty<YoloDetection>();
            IReadOnlyList<PoseResult> poses = Array.Empty<PoseResult>();
            IReadOnlyList<HandResult> hands = Array.Empty<HandResult>();

            try
            {
                if (_yolo is { IsReady: true })
                    detections = _yolo.Detect(frame);
                if (_pose is { IsReady: true })
                    poses = _pose.Detect(frame, detections);
                if (_hand is { IsReady: true })
                    hands = _hand.Detect(frame, detections);
            }
            catch (Exception ex)
            {
                ShowStatus($"推理异常: {ex.Message}");
            }

            inferWatch.Stop();
            frames++;
            if (fpsWatch.ElapsedMilliseconds >= 500)
            {
                fps = frames * 1000.0 / fpsWatch.ElapsedMilliseconds;
                frames = 0;
                fpsWatch.Restart();
            }

            var bitmap = frame.ToBitmap();
            FrameRenderer.Draw(bitmap, detections, poses, hands);

            var personCount = poses.Count > 0
                ? poses.Count
                : detections.Count(d => d.ClassId == YoloDetector.PersonClassId);
            var status =
                $"FPS: {fps:0.0}  |  人: {personCount}  |  手: {hands.Count}  |  推理 {inferWatch.ElapsedMilliseconds} ms";
            var warn = DetectorWarning();
            if (warn.Length > 0)
                status += "  |  " + warn;

            PostFrame(bitmap, status);
        }
    }

    private void EnsureDetectors()
    {
        _yolo ??= new YoloDetector();
        _pose ??= new PoseDetector();
        _hand ??= new HandDetector();
        ShowStatus($"{_yolo.Status}; {_pose.Status}; {_hand.Status}");
    }

    private string DetectorWarning()
    {
        var parts = new List<string>();
        if (_yolo is { IsReady: false }) parts.Add("YOLO 未加载");
        if (_pose is { IsReady: false }) parts.Add("Pose 未加载");
        if (_hand is { IsReady: false }) parts.Add("Hands 未加载");
        return string.Join("，", parts);
    }

    private void PostFrame(Bitmap bitmap, string status)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            bitmap.Dispose();
            return;
        }

        try
        {
            BeginInvoke(() =>
            {
                if (IsDisposed)
                {
                    bitmap.Dispose();
                    return;
                }

                var old = pictureBox.Image;
                pictureBox.Image = bitmap;
                old?.Dispose();
                lblStatus.Text = status;
            });
        }
        catch (ObjectDisposedException)
        {
            bitmap.Dispose();
        }
        catch (InvalidOperationException)
        {
            bitmap.Dispose();
        }
    }

    private void ShowStatus(string text)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        try
        {
            BeginInvoke(() =>
            {
                if (!IsDisposed)
                    lblStatus.Text = text;
            });
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
