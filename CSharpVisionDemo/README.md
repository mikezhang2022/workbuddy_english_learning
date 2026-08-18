# CSharpVisionDemo

.NET 8 WinForms 实时视觉演示：默认摄像头取帧，在同一画面上叠加 **YOLOv8 目标检测**、**MediaPipe Pose（33 关键点）** 与 **MediaPipe Hands（21 关键点）**。

采集用 Emgu.CV；YOLOv8 用 Microsoft.ML.OnnxRuntime 推理并自行做 letterbox / 解码 / NMS；Pose / Hands 使用与官方 Tasks Vision 对齐的 `PoseLandmarker` / `HandLandmarker` API，从 `models/` 加载模型。采集与三模型推理跑在后台 `Task` + `CancellationTokenSource` 上，避免卡住 UI。

## 运行前置条件

1. 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。
2. Windows 上安装 Visual Studio 2022（含“.NET 桌面开发”工作负载），或仅用 SDK 命令行。
3. 可用的摄像头（默认设备索引 0）。
4. 将下面三个模型文件放到 **`CSharpVisionDemo/models/`**（与 `.csproj` 同级的 `models` 目录）。

本程序面向 **Windows**（`net8.0-windows`）。Linux/macOS 可还原包并交叉编译，但不能运行 WinForms 界面。

## 模型文件（需自行下载）

放到：

```text
CSharpVisionDemo/models/yolov8n.onnx
CSharpVisionDemo/models/pose_landmarker.task
CSharpVisionDemo/models/hand_landmarker.task
```

### 1. `yolov8n.onnx`（Ultralytics 官方导出或下载）

推荐用 Ultralytics 官方权重自行导出，保证与当前 YOLOv8 输出布局一致：

```bash
pip install ultralytics
yolo export model=yolov8n.pt format=onnx
```

官方 PyTorch 权重：<https://github.com/ultralytics/assets/releases>（`yolov8n.pt`）。  
文档：<https://docs.ultralytics.com/modes/export/>

导出得到 `yolov8n.onnx` 后复制到 `CSharpVisionDemo/models/`。

### 2. `pose_landmarker.task`（Google MediaPipe 官方 storage）

从 Google MediaPipe 模型库下载 lite 或 full，**另存为** `pose_landmarker.task`：

- Lite：<https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task>
- Full：<https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_full/float16/1/pose_landmarker_full.task>
- Heavy：<https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_heavy/float16/1/pose_landmarker_heavy.task>

说明：<https://developers.google.com/edge/mediapipe/solutions/vision/pose_landmarker>

```bash
curl -L -o CSharpVisionDemo/models/pose_landmarker.task ^
  https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task
```

### 3. `hand_landmarker.task`（Google MediaPipe 官方 storage）

- <https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task>

说明：<https://developers.google.com/edge/mediapipe/solutions/vision/hand_landmarker>

```bash
curl -L -o CSharpVisionDemo/models/hand_landmarker.task ^
  https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task
```

### 关于 MediaPipe `.task` 与桌面推理后端

NuGet 上的 MediaPipe Tasks Vision 绑定目前主要面向 Android / iOS（例如 `HolisticWare.Google.MediaPipe.Tasks.Vision`、`MediaPipeTasksVision.Android`），**没有官方 Windows WinForms 包 `Google.MediaPipe.Tasks.Vision`**。

本项目因此在 `PoseDetector.cs` / `HandDetector.cs` 中实现了同名 API：

- `Google.MediaPipe.Tasks.Vision.PoseLandmarker`
- `Google.MediaPipe.Tasks.Vision.HandLandmarker`

并从相对路径 `models/pose_landmarker.task`、`models/hand_landmarker.task` 加载官方模型资产。桌面推理走 **ONNX Runtime**。官方 `.task` 是带 MediaPipe 自定义算子的 TFLite 图，ONNX Runtime 往往无法直接打开；若加载失败，把转换后的 ONNX 放到同一目录即可（程序会自动探测）：

```text
models/pose_landmarker.onnx
models/pose_landmark.onnx
models/hand_landmarker.onnx
models/hand_landmark.onnx
```

缺少某个模型时，摄像头画面仍会显示，对应分支跳过，状态栏会提示未加载项。

## 启动方式

在仓库根目录：

```bash
dotnet restore CSharpVisionDemo/CSharpVisionDemo.csproj
dotnet build CSharpVisionDemo/CSharpVisionDemo.csproj -c Release
dotnet run --project CSharpVisionDemo/CSharpVisionDemo.csproj -c Release
```

或在 Visual Studio 中打开 `CSharpVisionDemo/CSharpVisionDemo.csproj`，F5 运行。

UI：`PictureBox` 显示画面；**Start** 开摄像头与推理；**Stop** 停止。底部状态栏显示 FPS、当前人数（姿态结果，否则用 YOLO `person` 框）、手的数量。

## 项目结构

| 文件 | 作用 |
| --- | --- |
| `Program.cs` | WinForms 入口 |
| `Form1.cs` / `Form1.Designer.cs` | 主窗体、后台采集循环、Start/Stop |
| `YoloDetector.cs` | YOLOv8 ONNX + NMS |
| `PoseDetector.cs` | PoseLandmarker，33 点与骨骼连接 |
| `HandDetector.cs` | HandLandmarker，21 点与简单手势 |
| `FrameRenderer.cs` | 三路结果画到同一 Bitmap |
| `VisionModels.cs` | 共用类型、模型路径、ONNX 会话 |
| `models/` | 模型文件目录（需自行下载） |

同一帧上依次跑 YOLO → Pose → Hands，结果叠加绘制。YOLO 检出的 `person` 框会作为姿态 ROI；手部会在全图及人体上半身左右区域尝试检测。
