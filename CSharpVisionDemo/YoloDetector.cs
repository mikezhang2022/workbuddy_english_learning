using System.Drawing;
using Emgu.CV;

namespace CSharpVisionDemo;

/// <summary>
/// YOLOv8 检测器：Microsoft.ML.OnnxRuntime 加载 yolov8n.onnx，自行完成 letterbox、解码与 NMS。
/// 官方导出输出通常为 [1, 84, 8400]（4 个框回归 + 80 类分数，8400 个候选）。
/// </summary>
internal sealed class YoloDetector : IDisposable
{
    public const int PersonClassId = 0;

    private static readonly string[] CocoNames =
    {
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
        "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog",
        "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella",
        "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite",
        "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
        "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich",
        "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch",
        "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse", "remote",
        "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book",
        "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
    };

    private readonly OnnxVisionSession _session;
    private readonly float _scoreThreshold;
    private readonly float _iouThreshold;

    public bool IsReady { get; }
    public string Status { get; }

    public YoloDetector(string? modelPath = null, float scoreThreshold = 0.25f, float iouThreshold = 0.45f)
    {
        _scoreThreshold = scoreThreshold;
        _iouThreshold = iouThreshold;
        modelPath ??= ModelLocator.YoloOnnx;
        if (!File.Exists(modelPath))
        {
            Status = $"未找到 {modelPath}";
            IsReady = false;
            _session = null!;
            return;
        }

        try
        {
            _session = new OnnxVisionSession(modelPath);
            IsReady = true;
            Status = "YOLOv8 已加载";
        }
        catch (Exception ex)
        {
            Status = $"YOLOv8 加载失败: {ex.Message}";
            IsReady = false;
            _session = null!;
        }
    }

    public IReadOnlyList<YoloDetection> Detect(Mat bgrFrame)
    {
        if (!IsReady || bgrFrame.IsEmpty)
            return Array.Empty<YoloDetection>();

        using var letterboxed = Letterbox.Apply(
            bgrFrame,
            _session.InputWidth,
            _session.InputHeight,
            out var scale,
            out var padX,
            out var padY);

        using var results = _session.Run(letterboxed);
        var output = results.First().AsEnumerable<float>().ToArray();
        var shape = results.First().AsTensor<float>().Dimensions.ToArray();
        var candidates = Decode(output, shape, scale, padX, padY, bgrFrame.Width, bgrFrame.Height);
        return NonMaxSuppression(candidates, _iouThreshold);
    }

    private List<YoloDetection> Decode(
        float[] data,
        int[] shape,
        float scale,
        float padX,
        float padY,
        int imageW,
        int imageH)
    {
        // 兼容 [1,84,N] 与 [1,N,84]
        var rank = shape.Length;
        int channels;
        int preds;
        bool channelFirst;
        if (rank >= 3 && shape[^2] < shape[^1])
        {
            channels = shape[^2];
            preds = shape[^1];
            channelFirst = true;
        }
        else
        {
            preds = shape[^2];
            channels = shape[^1];
            channelFirst = false;
        }

        var classCount = Math.Max(1, channels - 4);
        var detections = new List<YoloDetection>(64);

        for (var i = 0; i < preds; i++)
        {
            float cx, cy, w, h;
            var bestClass = 0;
            var bestScore = 0f;
            if (channelFirst)
            {
                cx = data[0 * preds + i];
                cy = data[1 * preds + i];
                w = data[2 * preds + i];
                h = data[3 * preds + i];
                for (var c = 0; c < classCount; c++)
                {
                    var score = data[(4 + c) * preds + i];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClass = c;
                    }
                }
            }
            else
            {
                var offset = i * channels;
                cx = data[offset + 0];
                cy = data[offset + 1];
                w = data[offset + 2];
                h = data[offset + 3];
                for (var c = 0; c < classCount; c++)
                {
                    var score = data[offset + 4 + c];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClass = c;
                    }
                }
            }

            if (bestScore < _scoreThreshold)
                continue;

            var box = new RectangleF(cx - w / 2f, cy - h / 2f, w, h);
            box = Letterbox.MapBack(box, scale, padX, padY, imageW, imageH);
            var label = bestClass >= 0 && bestClass < CocoNames.Length ? CocoNames[bestClass] : $"cls{bestClass}";
            detections.Add(new YoloDetection
            {
                Label = label,
                ClassId = bestClass,
                Score = bestScore,
                Box = box
            });
        }

        return detections;
    }

    private static List<YoloDetection> NonMaxSuppression(List<YoloDetection> detections, float iouThreshold)
    {
        var keep = new List<YoloDetection>(detections.Count);
        foreach (var group in detections.GroupBy(d => d.ClassId))
        {
            var list = group.OrderByDescending(d => d.Score).ToList();
            var suppressed = new bool[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                if (suppressed[i])
                    continue;
                keep.Add(list[i]);
                for (var j = i + 1; j < list.Count; j++)
                {
                    if (!suppressed[j] && Geometry.IoU(list[i].Box, list[j].Box) > iouThreshold)
                        suppressed[j] = true;
                }
            }
        }

        return keep;
    }

    public void Dispose()
    {
        if (IsReady)
            _session.Dispose();
    }
}
