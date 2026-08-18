using System.Drawing;
using Emgu.CV;
using Google.MediaPipe.Tasks.Vision;
using Microsoft.ML.OnnxRuntime;

namespace CSharpVisionDemo
{

/// <summary>
/// 人体姿态估计：封装 MediaPipe PoseLandmarker（33 关键点）。
/// 官方桌面端没有 Google.MediaPipe.Tasks.Vision NuGet，本类加载 models/pose_landmarker.task，
/// 并在 WinForms 上用 ONNX Runtime 运行兼容后端（.task 本身或同目录 ONNX）。
/// </summary>
internal sealed class PoseDetector : IDisposable
{
    private readonly PoseLandmarker? _landmarker;

    public bool IsReady => _landmarker is not null;
    public string Status { get; }

    public PoseDetector(string? modelPath = null, int numPoses = 4)
    {
        modelPath ??= ModelLocator.PoseTask;
        try
        {
            var options = new PoseLandmarkerOptions
            {
                ModelAssetPath = modelPath,
                NumPoses = numPoses,
                MinPoseDetectionConfidence = 0.5f,
                MinPosePresenceConfidence = 0.5f
            };
            _landmarker = PoseLandmarker.CreateFromOptions(options);
            Status = "PoseLandmarker 已加载";
        }
        catch (Exception ex)
        {
            Status = $"姿态模型未就绪: {ex.Message}";
            _landmarker = null;
        }
    }

    public IReadOnlyList<PoseResult> Detect(Mat bgrFrame, IReadOnlyList<YoloDetection>? yoloDetections = null)
    {
        if (!IsReady || bgrFrame.IsEmpty)
            return Array.Empty<PoseResult>();

        var rois = BuildRois(bgrFrame, yoloDetections);
        return _landmarker!.Detect(bgrFrame, rois);
    }

    private static IReadOnlyList<Rectangle> BuildRois(Mat frame, IReadOnlyList<YoloDetection>? detections)
    {
        var rois = new List<Rectangle>();
        if (detections is not null)
        {
            foreach (var det in detections)
            {
                if (det.ClassId != YoloDetector.PersonClassId)
                    continue;
                var expanded = Geometry.Expand(det.Box, 0.18f, frame.Width, frame.Height);
                rois.Add(Geometry.Clamp(expanded, frame.Width, frame.Height));
            }
        }

        if (rois.Count == 0)
            rois.Add(new Rectangle(0, 0, frame.Width, frame.Height));

        return rois;
    }

    public void Dispose() => _landmarker?.Dispose();
}
}

namespace Google.MediaPipe.Tasks.Vision
{
    internal sealed class PoseLandmarkerOptions
    {
        public string ModelAssetPath { get; set; } = "";
        public int NumPoses { get; set; } = 1;
        public float MinPoseDetectionConfidence { get; set; } = 0.5f;
        public float MinPosePresenceConfidence { get; set; } = 0.5f;
    }

    /// <summary>
    /// MediaPipe Tasks Vision PoseLandmarker 的 WinForms 兼容实现。
    /// 加载官方 pose_landmarker.task；若文件是 TFLite MediaPipe 自定义图，则回退到同目录 ONNX。
    /// </summary>
    internal sealed class PoseLandmarker : IDisposable
    {
        public const int LandmarkCount = 33;

        /// <summary>官方 33 点骨骼连接，与 MediaPipe PoseLandmarksConnections 一致。</summary>
        public static readonly (int A, int B)[] Connections =
        {
            (0, 1), (1, 2), (2, 3), (3, 7),
            (0, 4), (4, 5), (5, 6), (6, 8),
            (9, 10),
            (11, 12),
            (11, 13), (13, 15), (15, 17), (15, 19), (15, 21), (17, 19),
            (12, 14), (14, 16), (16, 18), (16, 20), (16, 22), (18, 20),
            (11, 23), (12, 24), (23, 24),
            (23, 25), (24, 26), (25, 27), (26, 28),
            (27, 29), (28, 30), (29, 31), (30, 32),
            (27, 31), (28, 32)
        };

        private readonly CSharpVisionDemo.OnnxVisionSession _session;
        private readonly PoseLandmarkerOptions _options;

        private PoseLandmarker(CSharpVisionDemo.OnnxVisionSession session, PoseLandmarkerOptions options)
        {
            _session = session;
            _options = options;
        }

        public static PoseLandmarker CreateFromOptions(PoseLandmarkerOptions options)
        {
            var modelPath = ResolveRunnableModel(options.ModelAssetPath);
            var session = new CSharpVisionDemo.OnnxVisionSession(modelPath);
            return new PoseLandmarker(session, options);
        }

        public IReadOnlyList<CSharpVisionDemo.PoseResult> Detect(Mat bgrFrame, IReadOnlyList<Rectangle> rois)
        {
            var poses = new List<CSharpVisionDemo.PoseResult>(_options.NumPoses);
            foreach (var roi in rois)
            {
                if (poses.Count >= _options.NumPoses)
                    break;
                if (roi.Width < 16 || roi.Height < 16)
                    continue;

                using var crop = new Mat(bgrFrame, roi);
                using var outputs = _session.Run(crop);
                var landmarks = ParseLandmarks(
                    outputs.First().AsEnumerable<float>().ToArray(),
                    outputs.First().AsTensor<float>().Dimensions.ToArray(),
                    roi,
                    _options.MinPosePresenceConfidence);

                if (landmarks is null)
                    continue;
                poses.Add(new CSharpVisionDemo.PoseResult { Landmarks = landmarks });
            }

            return poses;
        }

        private static IReadOnlyList<CSharpVisionDemo.LandmarkPoint>? ParseLandmarks(
            float[] data,
            int[] shape,
            Rectangle roi,
            float minPresence)
        {
            var valuesPerPoint = InferStride(data.Length, shape);
            var count = data.Length / valuesPerPoint;
            if (count < LandmarkCount)
                return null;

            var points = new CSharpVisionDemo.LandmarkPoint[LandmarkCount];
            var visible = 0;
            for (var i = 0; i < LandmarkCount; i++)
            {
                var offset = i * valuesPerPoint;
                var nx = data[offset];
                var ny = data[offset + 1];
                var z = valuesPerPoint > 2 ? data[offset + 2] : 0f;
                var vis = valuesPerPoint > 3 ? data[offset + 3] : 1f;
                if (valuesPerPoint > 4)
                    vis = Math.Min(vis, data[offset + 4]);

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

            return visible >= 4 ? points : null;
        }

        private static int InferStride(int length, int[] shape)
        {
            if (shape.Length >= 2 && shape[^1] is 3 or 4 or 5 && length % shape[^1] == 0)
                return shape[^1];
            if (length % 5 == 0 && length / 5 is LandmarkCount or 39)
                return 5;
            if (length % 4 == 0 && length / 4 >= LandmarkCount)
                return 4;
            return 3;
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

            candidates.Add(Path.Combine(dir, "pose_landmarker.onnx"));
            candidates.Add(Path.Combine(dir, "pose_landmark.onnx"));
            candidates.Add(Path.Combine(dir, "pose_landmark_lite.onnx"));
            candidates.Add(Path.Combine(dir, "pose_landmarker_lite.onnx"));

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

            var hint = "请将官方 pose_landmarker.task 放到 models/。若 ONNX Runtime 无法直接加载 .task（MediaPipe TFLite 自定义算子），请同时放置转换后的 pose_landmarker.onnx。";
            throw new FileNotFoundException(last is null ? hint : $"{hint} 最后错误: {last.Message}", requestedPath);
        }

        public void Dispose() => _session.Dispose();
    }
}
