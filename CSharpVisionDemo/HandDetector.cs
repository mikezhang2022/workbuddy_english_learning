using System.Drawing;
using Emgu.CV;
using Google.MediaPipe.Tasks.Vision;
using Microsoft.ML.OnnxRuntime;

namespace CSharpVisionDemo
{

/// <summary>
/// 手部关键点 / 手势：封装 MediaPipe HandLandmarker（每只手 21 点）。
/// 加载 models/hand_landmarker.task，桌面端用 ONNX Runtime 兼容后端。
/// </summary>
internal sealed class HandDetector : IDisposable
{
    private readonly HandLandmarker? _landmarker;

    public bool IsReady => _landmarker is not null;
    public string Status { get; }

    public HandDetector(string? modelPath = null, int numHands = 4)
    {
        modelPath ??= ModelLocator.HandTask;
        try
        {
            var options = new HandLandmarkerOptions
            {
                ModelAssetPath = modelPath,
                NumHands = numHands,
                MinHandDetectionConfidence = 0.5f,
                MinHandPresenceConfidence = 0.5f
            };
            _landmarker = HandLandmarker.CreateFromOptions(options);
            Status = "HandLandmarker 已加载";
        }
        catch (Exception ex)
        {
            Status = $"手部模型未就绪: {ex.Message}";
            _landmarker = null;
        }
    }

    public IReadOnlyList<HandResult> Detect(Mat bgrFrame, IReadOnlyList<YoloDetection>? yoloDetections = null)
    {
        if (!IsReady || bgrFrame.IsEmpty)
            return Array.Empty<HandResult>();

        var rois = BuildRois(bgrFrame, yoloDetections);
        return _landmarker!.Detect(bgrFrame, rois);
    }

    private static IReadOnlyList<Rectangle> BuildRois(Mat frame, IReadOnlyList<YoloDetection>? detections)
    {
        var rois = new List<Rectangle> { new(0, 0, frame.Width, frame.Height) };
        if (detections is null)
            return rois;

        foreach (var det in detections)
        {
            if (det.ClassId != YoloDetector.PersonClassId)
                continue;

            var box = Geometry.Expand(det.Box, 0.05f, frame.Width, frame.Height);
            var upper = new RectangleF(box.X, box.Y, box.Width, box.Height * 0.62f);
            var left = new RectangleF(upper.X, upper.Y, upper.Width * 0.55f, upper.Height);
            var right = new RectangleF(upper.X + upper.Width * 0.45f, upper.Y, upper.Width * 0.55f, upper.Height);
            rois.Add(Geometry.Clamp(left, frame.Width, frame.Height));
            rois.Add(Geometry.Clamp(right, frame.Width, frame.Height));
        }

        return rois;
    }

    public void Dispose() => _landmarker?.Dispose();
}
}

namespace Google.MediaPipe.Tasks.Vision
{
    internal sealed class HandLandmarkerOptions
    {
        public string ModelAssetPath { get; set; } = "";
        public int NumHands { get; set; } = 2;
        public float MinHandDetectionConfidence { get; set; } = 0.5f;
        public float MinHandPresenceConfidence { get; set; } = 0.5f;
    }

    /// <summary>
    /// MediaPipe Tasks Vision HandLandmarker 的 WinForms 兼容实现。
    /// 每只手 21 个关键点；并基于指尖伸展做简单手势分类。
    /// </summary>
    internal sealed class HandLandmarker : IDisposable
    {
        public const int LandmarkCount = 21;

        public static readonly (int A, int B)[] Connections =
        {
            (0, 1), (1, 2), (2, 3), (3, 4),
            (0, 5), (5, 6), (6, 7), (7, 8),
            (0, 9), (9, 10), (10, 11), (11, 12),
            (0, 13), (13, 14), (14, 15), (15, 16),
            (0, 17), (17, 18), (18, 19), (19, 20),
            (5, 9), (9, 13), (13, 17)
        };

        private readonly CSharpVisionDemo.OnnxVisionSession _session;
        private readonly HandLandmarkerOptions _options;

        private HandLandmarker(CSharpVisionDemo.OnnxVisionSession session, HandLandmarkerOptions options)
        {
            _session = session;
            _options = options;
        }

        public static HandLandmarker CreateFromOptions(HandLandmarkerOptions options)
        {
            var modelPath = ResolveRunnableModel(options.ModelAssetPath);
            var session = new CSharpVisionDemo.OnnxVisionSession(modelPath);
            return new HandLandmarker(session, options);
        }

        public IReadOnlyList<CSharpVisionDemo.HandResult> Detect(Mat bgrFrame, IReadOnlyList<Rectangle> rois)
        {
            var hands = new List<CSharpVisionDemo.HandResult>(_options.NumHands);
            foreach (var roi in rois)
            {
                if (hands.Count >= _options.NumHands)
                    break;
                if (roi.Width < 16 || roi.Height < 16)
                    continue;

                using var crop = new Mat(bgrFrame, roi);
                using var outputs = _session.Run(crop);
                var named = outputs.ToDictionary(v => v.Name, v => v);
                var landmarkValue = outputs.First();
                var data = landmarkValue.AsEnumerable<float>().ToArray();
                var shape = landmarkValue.AsTensor<float>().Dimensions.ToArray();
                var landmarks = ParseLandmarks(data, shape, roi, _options.MinHandPresenceConfidence);
                if (landmarks is null)
                    continue;

                if (IsDuplicate(hands, landmarks))
                    continue;

                var handedness = ParseHandedness(named);
                hands.Add(new CSharpVisionDemo.HandResult
                {
                    Landmarks = landmarks,
                    Handedness = handedness,
                    Gesture = ClassifyGesture(landmarks)
                });
            }

            return hands;
        }

        private static IReadOnlyList<CSharpVisionDemo.LandmarkPoint>? ParseLandmarks(
            float[] data,
            int[] shape,
            Rectangle roi,
            float minPresence)
        {
            var stride = InferStride(data.Length, shape);
            var count = data.Length / stride;
            if (count < LandmarkCount)
                return null;

            // 部分模型一次输出多只手：取第一只
            var points = new CSharpVisionDemo.LandmarkPoint[LandmarkCount];
            var visible = 0;
            for (var i = 0; i < LandmarkCount; i++)
            {
                var offset = i * stride;
                var nx = data[offset];
                var ny = data[offset + 1];
                var z = stride > 2 ? data[offset + 2] : 0f;
                var vis = stride > 3 ? data[offset + 3] : 1f;
                float x, y;
                if (Math.Abs(nx) <= 1.5f && Math.Abs(ny) <= 1.5f)
                {
                    x = roi.X + nx * roi.Width;
                    y = roi.Y + ny * roi.Height;
                }
                else
                {
                    x = roi.X + nx;
                    y = roi.Y + ny;
                }

                points[i] = new CSharpVisionDemo.LandmarkPoint(x, y, z, vis);
                if (vis >= minPresence)
                    visible++;
            }

            return visible >= 8 ? points : null;
        }

        private static int InferStride(int length, int[] shape)
        {
            if (shape.Length >= 2 && shape[^1] is 3 or 4 or 5 && length % shape[^1] == 0)
                return shape[^1];
            if (length % 3 == 0 && (length / 3) % LandmarkCount == 0)
                return 3;
            if (length % 4 == 0 && (length / 4) % LandmarkCount == 0)
                return 4;
            return 3;
        }

        private static string ParseHandedness(Dictionary<string, Microsoft.ML.OnnxRuntime.DisposableNamedOnnxValue> named)
        {
            foreach (var (name, value) in named)
            {
                if (name.Contains("handedness", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("hand_type", StringComparison.OrdinalIgnoreCase))
                {
                    var scores = value.AsEnumerable<float>().ToArray();
                    if (scores.Length >= 2)
                        return scores[1] >= scores[0] ? "Right" : "Left";
                    if (scores.Length == 1)
                        return scores[0] >= 0.5f ? "Right" : "Left";
                }
            }

            return "Unknown";
        }

        private static bool IsDuplicate(
            List<CSharpVisionDemo.HandResult> existing,
            IReadOnlyList<CSharpVisionDemo.LandmarkPoint> candidate)
        {
            var cWrist = candidate[0];
            foreach (var hand in existing)
            {
                var d = CSharpVisionDemo.Geometry.Distance(hand.Landmarks[0], cWrist);
                if (d < 40)
                    return true;
            }

            return false;
        }

        /// <summary>用 21 点几何关系做轻量手势分类（伸掌 / 握拳 / 指向 / 胜利）。</summary>
        public static string ClassifyGesture(IReadOnlyList<CSharpVisionDemo.LandmarkPoint> pts)
        {
            if (pts.Count < LandmarkCount)
                return "";

            var wrist = pts[0];
            bool Ext(int tip, int pip) =>
                CSharpVisionDemo.Geometry.Distance(wrist, pts[tip]) > CSharpVisionDemo.Geometry.Distance(wrist, pts[pip]) * 1.15f;

            var thumb = Ext(4, 3);
            var index = Ext(8, 6);
            var middle = Ext(12, 10);
            var ring = Ext(16, 14);
            var pinky = Ext(20, 18);
            var count = (thumb ? 1 : 0) + (index ? 1 : 0) + (middle ? 1 : 0) + (ring ? 1 : 0) + (pinky ? 1 : 0);

            if (count == 0) return "Fist";
            if (count >= 4) return "Open";
            if (index && middle && !ring && !pinky) return "Victory";
            if (index && !middle && !ring && !pinky) return "Point";
            if (thumb && index && !middle && !ring && !pinky) return "OK";
            return $"{count}F";
        }

        private static string ResolveRunnableModel(string requestedPath)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(requestedPath))
                candidates.Add(requestedPath);

            var dir = string.IsNullOrWhiteSpace(requestedPath)
                ? CSharpVisionDemo.ModelLocator.ModelsDirectory
                : (Directory.Exists(requestedPath) ? requestedPath : Path.GetDirectoryName(requestedPath));
            dir ??= CSharpVisionDemo.ModelLocator.ModelsDirectory;

            candidates.Add(Path.Combine(dir, "hand_landmarker.onnx"));
            candidates.Add(Path.Combine(dir, "hand_landmark.onnx"));
            candidates.Add(Path.Combine(dir, "hand_landmark_full.onnx"));
            candidates.Add(Path.Combine(dir, "hand_landmarker_full.onnx"));

            Exception? last = null;
            foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                    continue;
                try
                {
                    using var probe = new CSharpVisionDemo.OnnxVisionSession(path);
                    return path;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }

            var hint = "请将官方 hand_landmarker.task 放到 models/。若 ONNX Runtime 无法直接加载 .task，请同时放置转换后的 hand_landmarker.onnx。";
            throw new FileNotFoundException(last is null ? hint : $"{hint} 最后错误: {last.Message}", requestedPath);
        }

        public void Dispose() => _session.Dispose();
    }
}
