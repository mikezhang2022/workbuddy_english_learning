# YOLOv8 可视化工作台

面向 YOLOv8 工作流的桌面应用：导入与标注数据、训练模型、双模型并排对比。基于 **Tkinter (ttk)**、**Ultralytics YOLOv8** 与 **PyTorch**，界面为简体中文浅色主题。

更完整的操作说明见 [USER_MANUAL.md](USER_MANUAL.md)。

## 快速开始

```bash
cd yolov8_gui
python -m venv .venv
source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt
python main.py
```

直接打开指定工具：

```bash
python main.py --tool image
python main.py --tool train
python main.py --tool compare
```

通过环境变量设置工作区：

```bash
export YOLOV8_WORKSPACE=~/my_yolo_project
python main.py
```

## GPU 使用

- 启动时自动检测 PyTorch CUDA。
- 有 GPU 时，训练与推理使用 `device=0`。
- 无 CUDA 时回退到 CPU，启动器会给出明确提示，不会崩溃。

## 默认模型与自定义模型

- 未提供 `.pt` 文件时，加载 **`yolov8n.pt`**（Ultralytics 默认，首次会自动下载）。
- 界面会标明这是 **默认 COCO 预训练模型**，类别为 COCO 类别。
- 自定义类别请在「模型训练工具」中训练，或在「图片/视频工具」「模型对比工具」中自行指定 `.pt`。

## 各工具功能

### 工具 1 — 图片/视频工具
- 导入单张图片、视频或文件夹（混合媒体）
- 缩略列表与预览
- 用自定义或默认模型自动标注，写出 YOLO `.txt` 标签
- 导出标注视频
- **数据质检**：模糊、曝光、重复、标签问题
- **一键优化**：去重、CLAHE、锐化、标签修复
- **数据增强**：实时预览与样本数量估算
- 导出 `train/val` 划分 + `data.yaml`
- **发送到模型训练工具** 一键联动

### 工具 2 — 模型训练工具
- **自动模式**：推荐超参及说明
- **手动模式**：编辑轮数、学习率、批次大小、输入尺寸、优化器等
- 实时**建议面板**，一键修复（批次/显存、学习率、轮数、验证集、类别不平衡）
- 样本不足时可联动回图片/视频工具做数据增强
- 后台训练，实时日志与 matplotlib 损失/mAP 曲线
- 可停止训练；最佳权重保存到工作区

### 工具 3 — 模型对比工具
- 在同一图片/视频上加载两个 `.pt`（或默认）模型
- 并排可视化 + 差异叠加（一致 / 仅 A / 仅 B）
- 分析：检测数、类别分布、置信度、IoU、漏检误检、速度（ms/图）
- 改进建议
- 导出 HTML、文本或图表 PNG 报告

## 共享工作区

三个工具通过工作区文件夹共享状态（默认 `~/yolov8_workspace`）：

| 文件 / 键 | 用途 |
|-----------|------|
| `workspace_state.json` | 最近的 dataset yaml、权重、模型路径 |
| `dataset/` | 工具 1 的图片与标签 |
| `export/data.yaml` | 可训练数据集 |
| `runs/train/` | 训练输出与 best.pt |

典型流程：**图片/视频工具**（导入 → 增强 → 导出）→ **模型训练工具**（训练）→ **模型对比工具**（新旧权重对比）。

## 项目结构

```
yolov8_gui/
├── main.py                 # 启动器 + CLI
├── requirements.txt
├── README.md
├── USER_MANUAL.md          # 中文操作手册
├── __init__.py
├── core/
│   ├── device.py           # GPU/CUDA 检测
│   ├── theme.py            # 浅色 ttk 主题
│   ├── context.py          # AppContext / 工作区
│   ├── io_utils.py         # Unicode I/O、MediaItem
│   ├── yolo_engine.py      # 推理、绘制检测框
│   ├── dataset_qc.py       # 数据集质检
│   ├── augment.py          # 数据增强流水线
│   ├── train_engine.py     # 参数与建议
│   ├── train_runner.py     # 后台训练
│   └── compare.py          # 模型对比引擎
├── tools/
│   ├── image_tool.py       # 工具 1 GUI
│   ├── train_tool.py       # 工具 2 GUI
│   └── compare_tool.py     # 工具 3 GUI
└── tests/
    └── smoke_test.py       # 无界面冒烟测试
```

## 测试

```bash
python -m compileall yolov8_gui
python yolov8_gui/tests/smoke_test.py
```

冒烟测试覆盖导入、增强、质检、对比 IoU 匹配、训练参数校验、Unicode I/O。GUI 与重度 GPU 路径不要求在 CI 中运行。

## 环境要求

- Python 3.10+
- 依赖见 `requirements.txt`
- 可选：NVIDIA GPU + CUDA，以加速训练/推理
