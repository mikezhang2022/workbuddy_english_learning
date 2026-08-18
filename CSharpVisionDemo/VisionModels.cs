using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace CSharpVisionDemo;

internal readonly record struct LandmarkPoint(float X, float Y, float Z, float Visibility)
{
    public bool IsVisible(float threshold = 0.5f) => Visibility >= threshold;
}

internal sealed class YoloDetection
{
    public required string Label { get; init; }
    public required int ClassId { get; init; }
    public required float Score { get; init; }
    public required RectangleF Box { get; init; }
}

internal sealed class PoseResult
{
    public required IReadOnlyList<LandmarkPoint> Landmarks { get; init; }
}

internal sealed class HandResult
{
    public required IReadOnlyList<LandmarkPoint> Landmarks { get; init; }
    public string Handedness { get; init; } = "Unknown";
    public string Gesture { get; init; } = "";
}

internal static class ModelLocator
{
    public static string ModelsDirectory { get; } = ResolveModelsDirectory();

    public static string YoloOnnx => Path.Combine(ModelsDirectory, "yolov8n.onnx");
    public static string PoseTask => Path.Combine(ModelsDirectory, "pose_landmarker.task");
    public static string HandTask => Path.Combine(ModelsDirectory, "hand_landmarker.task");

    public static string ResolveExisting(params string[] fileNames)
    {
        foreach (var name in fileNames)
        {
            var path = Path.IsPathRooted(name) ? name : Path.Combine(ModelsDirectory, name);
            if (File.Exists(path))
                return path;
        }

        return Path.Combine(ModelsDirectory, fileNames[0]);
    }

    private static string ResolveModelsDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "models"),
            Path.Combine(Directory.GetCurrentDirectory(), "models"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "models"))
        };

        foreach (var dir in candidates)
        {
            if (Directory.Exists(dir))
                return dir;
        }

        return candidates[0];
    }
}

internal sealed class OnnxVisionSession : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly int[] _inputShape;
    private readonly bool _nhwc;
    private readonly string[] _outputNames;

    public OnnxVisionSession(string modelPath)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            InterOpNumThreads = 1,
            IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2)
        };
        try
        {
            options.AppendExecutionProvider_CPU();
        }
        catch (Exception)
        {
            // CPU EP 为默认执行提供程序；部分运行时版本无需显式追加。
        }

        _session = new InferenceSession(File.ReadAllBytes(modelPath), options);
        _inputName = _session.InputMetadata.Keys.First();
        _inputShape = NormalizeShape(_session.InputMetadata[_inputName].Dimensions);
        _nhwc = _inputShape.Length >= 4 && _inputShape[3] == 3;
        _outputNames = _session.OutputMetadata.Keys.ToArray();
        InputWidth = _nhwc ? _inputShape[2] : _inputShape[3];
        InputHeight = _nhwc ? _inputShape[1] : _inputShape[2];
        if (InputWidth <= 0) InputWidth = 256;
        if (InputHeight <= 0) InputHeight = 256;
    }

    public int InputWidth { get; }
    public int InputHeight { get; }
    public IReadOnlyList<string> OutputNames => _outputNames;

    public IDisposableReadOnlyCollection<DisposableNamedOnnxValue> Run(Mat bgrImage, bool swapRb = true)
    {
        using var blob = CreateBlob(bgrImage, swapRb);
        var tensor = ToTensor(blob);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, tensor)
        };
        return _session.Run(inputs, _outputNames);
    }

    public IDisposableReadOnlyCollection<DisposableNamedOnnxValue> RunTensor(DenseTensor<float> tensor)
    {
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, tensor)
        };
        return _session.Run(inputs, _outputNames);
    }

    public DenseTensor<float> CreateInputTensor(Mat bgrImage, bool swapRb = true)
    {
        using var blob = CreateBlob(bgrImage, swapRb);
        return ToTensor(blob);
    }

    private Mat CreateBlob(Mat bgrImage, bool swapRb)
    {
        if (_nhwc)
        {
            using var resized = new Mat();
            CvInvoke.Resize(bgrImage, resized, new Size(InputWidth, InputHeight), interpolation: Inter.Linear);
            using var rgb = new Mat();
            if (swapRb)
                CvInvoke.CvtColor(resized, rgb, ColorConversion.Bgr2Rgb);
            else
                resized.CopyTo(rgb);

            using var fp = new Mat();
            rgb.ConvertTo(fp, DepthType.Cv32F, 1.0 / 255.0);
            return fp.Clone();
        }

        return DnnInvoke.BlobFromImage(
            bgrImage,
            1.0 / 255.0,
            new Size(InputWidth, InputHeight),
            new Emgu.CV.Structure.MCvScalar(),
            swapRb,
            false);
    }

    private DenseTensor<float> ToTensor(Mat blob)
    {
        int count = InputWidth * InputHeight * 3;
        var tensor = _nhwc
            ? new DenseTensor<float>(new[] { 1, InputHeight, InputWidth, 3 })
            : new DenseTensor<float>(new[] { 1, 3, InputHeight, InputWidth });

        unsafe
        {
            var src = new Span<float>((void*)blob.DataPointer, count);
            src.CopyTo(tensor.Buffer.Span);
        }

        return tensor;
    }

    private static int[] NormalizeShape(int[] dims)
    {
        var copy = (int[])dims.Clone();
        for (var i = 0; i < copy.Length; i++)
        {
            if (copy[i] <= 0)
                copy[i] = i == 0 ? 1 : copy[i];
        }

        return copy;
    }

    public void Dispose() => _session.Dispose();
}

internal static class Letterbox
{
    public static Mat Apply(Mat source, int targetW, int targetH, out float scale, out float padX, out float padY)
    {
        scale = Math.Min(targetW / (float)source.Width, targetH / (float)source.Height);
        var newW = Math.Max(1, (int)Math.Round(source.Width * scale));
        var newH = Math.Max(1, (int)Math.Round(source.Height * scale));
        padX = (targetW - newW) / 2f;
        padY = (targetH - newH) / 2f;

        using var resized = new Mat();
        CvInvoke.Resize(source, resized, new Size(newW, newH), interpolation: Inter.Linear);

        var boxed = new Mat(targetH, targetW, DepthType.Cv8U, source.NumberOfChannels);
        boxed.SetTo(new Emgu.CV.Structure.MCvScalar(114, 114, 114));
        using var roi = new Mat(boxed, new Rectangle((int)padX, (int)padY, newW, newH));
        resized.CopyTo(roi);
        return boxed;
    }

    public static RectangleF MapBack(RectangleF box, float scale, float padX, float padY, int imageW, int imageH)
    {
        var x = (box.X - padX) / scale;
        var y = (box.Y - padY) / scale;
        var w = box.Width / scale;
        var h = box.Height / scale;
        x = Math.Clamp(x, 0, imageW - 1);
        y = Math.Clamp(y, 0, imageH - 1);
        w = Math.Clamp(w, 1, imageW - x);
        h = Math.Clamp(h, 1, imageH - y);
        return new RectangleF(x, y, w, h);
    }
}

internal static class Geometry
{
    public static float IoU(RectangleF a, RectangleF b)
    {
        var x1 = Math.Max(a.Left, b.Left);
        var y1 = Math.Max(a.Top, b.Top);
        var x2 = Math.Min(a.Right, b.Right);
        var y2 = Math.Min(a.Bottom, b.Bottom);
        var inter = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var union = a.Width * a.Height + b.Width * b.Height - inter;
        return union <= 0 ? 0 : inter / union;
    }

    public static Rectangle Clamp(RectangleF box, int width, int height)
    {
        var x = (int)Math.Floor(Math.Clamp(box.X, 0, Math.Max(0, width - 1)));
        var y = (int)Math.Floor(Math.Clamp(box.Y, 0, Math.Max(0, height - 1)));
        var w = (int)Math.Ceiling(Math.Clamp(box.Width, 1, width - x));
        var h = (int)Math.Ceiling(Math.Clamp(box.Height, 1, height - y));
        return new Rectangle(x, y, Math.Max(1, w), Math.Max(1, h));
    }

    public static RectangleF Expand(RectangleF box, float ratio, int width, int height)
    {
        var dx = box.Width * ratio;
        var dy = box.Height * ratio;
        return new RectangleF(
            Math.Max(0, box.X - dx),
            Math.Max(0, box.Y - dy),
            Math.Min(width - Math.Max(0, box.X - dx), box.Width + dx * 2),
            Math.Min(height - Math.Max(0, box.Y - dy), box.Height + dy * 2));
    }

    public static float Distance(LandmarkPoint a, LandmarkPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
