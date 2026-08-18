using System.Drawing;
using System.Drawing.Drawing2D;
using Google.MediaPipe.Tasks.Vision;

namespace CSharpVisionDemo;

/// <summary>
/// 将 YOLO 框、Pose 33 点骨骼、Hands 21 点叠加绘制到同一 Bitmap。
/// </summary>
internal static class FrameRenderer
{
    private static readonly Color[] HandColors =
    {
        Color.FromArgb(255, 255, 165, 0),
        Color.FromArgb(255, 0, 200, 255),
        Color.FromArgb(255, 255, 80, 180),
        Color.FromArgb(255, 180, 255, 80)
    };

    public static void Draw(
        Bitmap bitmap,
        IReadOnlyList<YoloDetection> detections,
        IReadOnlyList<PoseResult> poses,
        IReadOnlyList<HandResult> hands)
    {
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        DrawYolo(g, detections);
        DrawPoses(g, poses);
        DrawHands(g, hands);
    }

    private static void DrawYolo(Graphics g, IReadOnlyList<YoloDetection> detections)
    {
        using var pen = new Pen(Color.FromArgb(255, 50, 220, 90), 2f);
        using var fill = new SolidBrush(Color.FromArgb(180, 50, 220, 90));
        using var textBrush = new SolidBrush(Color.Black);
        using var font = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel);

        foreach (var det in detections)
        {
            var rect = Rectangle.Round(det.Box);
            g.DrawRectangle(pen, rect);
            var label = $"{det.Label} {det.Score:0.00}";
            var size = g.MeasureString(label, font);
            var bg = new RectangleF(rect.X, Math.Max(0, rect.Y - size.Height - 2), size.Width + 4, size.Height + 2);
            g.FillRectangle(fill, bg);
            g.DrawString(label, font, textBrush, bg.X + 2, bg.Y + 1);
        }
    }

    private static void DrawPoses(Graphics g, IReadOnlyList<PoseResult> poses)
    {
        using var bonePen = new Pen(Color.FromArgb(255, 0, 210, 170), 2.4f);
        using var jointBrush = new SolidBrush(Color.FromArgb(255, 255, 230, 80));

        foreach (var pose in poses)
        {
            var pts = pose.Landmarks;
            foreach (var (a, b) in PoseLandmarker.Connections)
            {
                if (a >= pts.Count || b >= pts.Count)
                    continue;
                if (!pts[a].IsVisible() || !pts[b].IsVisible())
                    continue;
                g.DrawLine(bonePen, pts[a].X, pts[a].Y, pts[b].X, pts[b].Y);
            }

            foreach (var p in pts)
            {
                if (!p.IsVisible())
                    continue;
                g.FillEllipse(jointBrush, p.X - 3, p.Y - 3, 6, 6);
            }
        }
    }

    private static void DrawHands(Graphics g, IReadOnlyList<HandResult> hands)
    {
        using var font = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);

        for (var h = 0; h < hands.Count; h++)
        {
            var color = HandColors[h % HandColors.Length];
            using var bonePen = new Pen(color, 2.2f);
            using var jointBrush = new SolidBrush(color);
            var pts = hands[h].Landmarks;

            foreach (var (a, b) in HandLandmarker.Connections)
            {
                if (a >= pts.Count || b >= pts.Count)
                    continue;
                g.DrawLine(bonePen, pts[a].X, pts[a].Y, pts[b].X, pts[b].Y);
            }

            foreach (var p in pts)
                g.FillEllipse(jointBrush, p.X - 2.6f, p.Y - 2.6f, 5.2f, 5.2f);

            var wrist = pts[0];
            var tag = string.IsNullOrEmpty(hands[h].Gesture)
                ? hands[h].Handedness
                : $"{hands[h].Handedness} {hands[h].Gesture}";
            using var bg = new SolidBrush(Color.FromArgb(180, color));
            var size = g.MeasureString(tag, font);
            g.FillRectangle(bg, wrist.X, wrist.Y - size.Height - 4, size.Width + 4, size.Height + 2);
            g.DrawString(tag, font, textBrush, wrist.X + 2, wrist.Y - size.Height - 3);
        }
    }
}
