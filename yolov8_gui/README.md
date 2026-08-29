# YOLOv8 可视化工作台

面向 YOLOv8 工作流的桌面应用：导入与标注数据、训练模型、双模型并排对比。基于 **Tkinter (ttk)**、**Ultralytics YOLOv8** 与 **PyTorch**，界面为简体中文浅色主题（当前版本 **v1.0.2**）。

更完整的操作说明见 [USER_MANUAL.md](USER_MANUAL.md)。

## 设置（字体与缩放）

主窗口右上角「⚙ 设置」，或菜单 **工具 → 设置**：

- **界面字体大小**：滑块 10–20，默认 12；拖动时可在设置窗口内实时预览
- **界面缩放 (DPI)**：0.8–1.5（步进 0.05），默认 1.0，放大按钮、间距与窗口尺寸

配置写入程序目录 `yolov8_gui_config.json`，并同步到工作区 `config.json`；下次启动自动加载。保存后主窗口立即刷新；已打开的工具窗口请关闭后重新打开。

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
│   ├── theme.py            # 浅色 ttk 主题（可配置字号/缩放）
│   ├── app_config.py       # 界面配置读写
│   ├── settings_dialog.py  # 设置窗口
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

本文档供自动化/人工（含另一 AI Agent，如 Codex）**按步骤执行验收**。  
除非另有说明，命令均在**仓库根目录**执行；GUI 步骤假定工作目录为 `yolov8_gui/`。

---

### 0. 前置环境

1. **Python 版本**：确认 `python --version` ≥ **3.10**。
2. **虚拟环境与依赖**（推荐在仓库根或 `yolov8_gui` 内建 venv）：

```bash
cd yolov8_gui
python -m venv .venv
source .venv/bin/activate          # Windows: .venv\Scripts\activate
pip install -r requirements.txt
```

3. **默认权重**：首次加载推理/标注时会自动下载 **`yolov8n.pt`**（约 **6MB**），**需要联网**。无网环境请事先将权重放到 Ultralytics 缓存目录或当前工作目录。
4. **显示 / Headless**：
   - 有桌面：直接 `python main.py`
   - **无显示的 Linux**（CI / SSH / 无 DISPLAY）：

```bash
export MPLBACKEND=Agg
xvfb-run -a python main.py
# 或直接打开某工具：
xvfb-run -a python main.py --tool image
```

5. **GPU**：可选 NVIDIA GPU + CUDA。无 GPU / 无 CUDA 时必须回退 **CPU**，启动与工具打开**不得崩溃**；启动器「环境 / 硬件」区应给出明确提示。

---

### 1. 自动化测试（必须全部通过，无需 GUI）

在**仓库根目录**执行：

```bash
python -m compileall yolov8_gui
# 期望输出含成功编译；无 SyntaxError。可记作：compileall: OK

python yolov8_gui/tests/smoke_test.py
# 期望末尾类似：
#   Ran 8 tests in …s
#   OK
# （smoke_test 入口也会先跑 compileall 并打印 compileall: OK）
```

**判定**：`compileall` 成功 + smoke 报告 **Ran 8 tests … OK**。任一失败即阻断后续 GUI 验收。

#### 8 个用例各自验证什么

| 类名 | 方法 | 验证内容 |
|------|------|----------|
| **TestImports** | `test_core_imports` | 可从 `yolov8_gui.core` 导入 `AppContext`、`AugConfig`、`CompareReport`、`DeviceInfo`、`Predictor`、`default_params`、`probe`；`probe` / `default_params` 可调用 |
| **TestDevice** | `test_probe` | `probe()` 返回的 `python_version` 为 str、`cpu_count` 为 int；`device_arg()` ∈ `{0, "cpu"}` |
| **TestAugment** | `test_preview_and_run` | `preview` 按配置生成 2 张样例；`run` 对带标签小图写出增强结果且数量 ≥ 1 |
| **TestDatasetQC** | `test_check_tiny_sample` | `check` 对单张小图+标签返回 `total_images == 1` |
| **TestTrainEngine** | `test_default_and_validate` | `default_params()` 含 `epochs` 及说明；`validate_params` 返回 list（建议列表） |
| **TestCompareEngine** | `test_dummy_compare` | `_iou` 对相近框 > 0.5；`_match_boxes` 匹配对数为 1 |
| **TestIOUtils** | `test_imread_write_unicode` | 中文文件名路径下 `imwrite_unicode` / `imread_unicode` 读写成功 |
| **TestContext** | `test_workspace_state` | `AppContext` 写入 `last_dataset_yaml` 后，同目录新实例能读回 |

---

### 2. 启动健康检查（无需 GUI）

在仓库根目录执行下列脚本（一次性验证 version / theme / app_config / device）：

```bash
python - <<'PY'
from yolov8_gui.core.version import __version__
from yolov8_gui.core.theme import configure_theme, get_font_size
from yolov8_gui.core.app_config import save_config, load_config, default_config
from yolov8_gui.core.device import probe
import tempfile
from pathlib import Path

assert __version__ == "1.0.2", __version__

# configure_theme 改变全局字号（无需真正弹出窗口时用 Tk 根或 mock）
import tkinter as tk
root = tk.Tk()
root.withdraw()
configure_theme(root, font_size=16, ui_scale=1.0)
assert get_font_size() == 16, get_font_size()
configure_theme(root, font_size=12, ui_scale=1.0)  # 恢复默认便于后续 GUI
root.destroy()

with tempfile.TemporaryDirectory() as td:
    save_config(14, 1.25, workspace=td)
    cfg = load_config(workspace=td)
    assert cfg["font_size"] == 14 and abs(cfg["ui_scale"] - 1.25) < 1e-6, cfg

info = probe()  # 不抛异常即可；有/无 CUDA 都接受
assert info.python_version and info.cpu_count >= 1
print("health_check: OK", __version__, info.device_arg())
PY
```

**通过标准**：

- `__version__ == "1.0.2"`
- `configure_theme(..., font_size=16)` 后 `get_font_size() == 16`
- `save_config` → `load_config` 往返一致（字号 / 缩放）
- `probe()` **不抛异常**

---

### 3. GUI 启动与「设置」页面测试

**启动**（有显示用第一条；无显示用第二条）：

```bash
cd yolov8_gui
python main.py
# 无显示：
# export MPLBACKEND=Agg && xvfb-run -a python main.py
```

**检查项**：

1. **主窗口标题**含 `YOLOv8 可视化工作台` 与 **`v1.0.2`**。
2. **浅色主题**（浅灰/白底、深色文字），非深色模式。
3. 主界面有 **三张工具卡片**：「图片/视频工具」「模型训练工具」「模型对比工具」，各有「打开」。
4. 底部 **「环境 / 硬件」** 文本框显示 **Python**、**CPU**、**CUDA**（是/否）等；**无 GPU 时有中文提示且进程不崩溃**。
5. 点击右上角 **「⚙ 设置」**（或菜单 **工具 → 设置**）弹出标题为 **「设置」** 的窗口。
6. 在设置窗中：
   - 拖动 **界面字体大小**（约 **10–20**）：设置窗内文字**实时预览**变大/变小。
   - 拖动 **界面缩放 (DPI)**（约 **0.8–1.5**）：按钮/间距/窗口尺寸可预览变化。
7. 点 **「保存并应用」**：主窗口立即按新配置刷新；配置写入 `yolov8_gui/yolov8_gui_config.json` 与工作区 `config.json`。
8. **关闭并重新启动** `main.py`：字号与缩放与保存值一致（持久化）。

可选：点「恢复默认」后字号回 12、缩放回 1.0。

---

### 4. 工具 1 — 图片/视频工具测试

打开方式：启动器卡片「打开」，或 `python main.py --tool image`（无显示加 `xvfb-run -a`）。

按顺序操作并确认无未捕获异常：

| 步骤 | 操作 | 期望 |
|------|------|------|
| 4.1 模型 | 「浏览」选 `.pt`，或点「**使用默认**」 | 状态标明默认 COCO 模型或自定义路径；首次默认会下载 `yolov8n.pt` |
| 4.2 导入 | 「导入图片」/「导入视频」/「导入文件夹」 | 左侧媒体列表出现条目，可点选预览 |
| 4.3 自动标注 | 「自动标注」 | 写出 YOLO `.txt` 到工作区 `dataset/labels`；状态栏有进度/完成 |
| 4.4 质检 | 「运行质检」 | 报告含模糊/曝光/重复/标签等问题统计 |
| 4.5 优化 | 「一键优化」 | 需先质检；完成后无崩溃（去重/CLAHE/锐化/标签修复等） |
| 4.6 增强预览 | 「数据增强」选项卡勾选变换 →「预览增强效果」 | 预览区显示增强样例 |
| 4.7 增强运行 | 「运行数据增强」 | 新增样本写入数据集；「预计新增样本」合理 |
| 4.8 导出数据集 | 「导出 train/val + data.yaml」 | 生成划分与 `export/data.yaml` |
| 4.9 联动训练 | 「发送到模型训练工具 →」 | 打开训练工具且带上 yaml 路径 |
| 4.10 标注视频 | 选中列表中的**视频** →「导出标注视频」 | 导出带检测框的视频文件 |

**冒烟建议**：用少量小图（几张）即可；完整大数据集非本指南必测项。

---

### 5. 工具 2 — 模型训练工具测试

打开：`python main.py --tool train`，或由工具 1「发送到模型训练工具」。

| 步骤 | 操作 | 期望 |
|------|------|------|
| 5.1 数据集 | 「浏览」选择 `data.yaml`（可用工具 1 刚导出的） | 路径填入顶部 |
| 5.2 模式 | 切换 **自动** / **手动** | 自动显示推荐超参与说明；手动可改 epochs/lr/batch/imgsz/优化器等 |
| 5.3 建议 | 查看「训练建议」列表；选一条 →「应用所选修复」 | 参数或联动跳转符合建议文案（如降 batch、去增强） |
| 5.4 冒烟训练 | 将 **epochs 设为很小**（如 1–2）、batch 调小；「开始训练」 | 日志滚动；有损失/mAP 曲线更新（CPU 可较慢）；**不要求**完整收敛 |
| 5.5 停止 | 训练中点「停止」 | 训练中止，无死锁/崩溃 |
| 5.6 联动回图 | 「发送到图片/视频工具 →」 | 打开图片/视频工具 |
| 5.7 权重 | 冒烟训练跑完后 | 工作区 `runs/` 下出现训练输出；**`best.pt`** 可定位（路径写入工作区状态） |

**注意**：完整长时间训练不作为必过项；本指南只要求**少 epoch 冒烟**证明训练管线可跑通。

---

### 6. 工具 3 — 模型对比工具测试

打开：`python main.py --tool compare`。

| 步骤 | 操作 | 期望 |
|------|------|------|
| 6.1 模型 | 模型 A / B 分别「浏览」或「默认」 | 可加载两个 `.pt`（可用两次默认，或 A=`best.pt`、B=`yolov8n.pt`） |
| 6.2 媒体 | 「选择图片」或「选择视频」 | 路径就绪 |
| 6.3 对比 | 「运行对比」 | 并排可视化；差异图（一致 / 仅 A / 仅 B）；状态显示对比帧数 |
| 6.4 报告 | 查看分析区 | 含检测数、类别分布、置信度、IoU、漏检/误检相关统计、速度（ms/图）及改进建议 |
| 6.5 导出 | 「导出 HTML 报告」「导出文本报告」「保存图表 PNG」 | 分别生成可读 HTML、文本、PNG，路径提示成功 |

---

### 7. 验收清单 Checklist

执行者在全部完成后勾选：

- [ ] `python -m compileall yolov8_gui` 成功，且 `python yolov8_gui/tests/smoke_test.py` 报告 **Ran 8 tests … OK**
- [ ] 启动健康检查脚本通过（`__version__=="1.0.2"`、主题字号、配置往返、`probe()`）
- [ ] GUI 启动：标题含 v1.0.2、浅色主题、三卡片；「⚙ 设置」字体/缩放实时预览，**保存后重启仍保持**
- [ ] 工具 1：模型选择、导入、自动标注、质检、一键优化、增强预览/运行、导出 train/val+yaml、发送到训练、导出标注视频（按可用素材尽量覆盖）
- [ ] 工具 2：浏览数据集、自动/手动、建议与一键修复、少 epoch 冒烟训练、停止、联动回图片工具、`best.pt` 可找到
- [ ] 工具 3：模型 A/B、选图/视频、运行对比、差异图与分析报告、导出 HTML/文本/PNG
- [ ] **无 GPU** 环境：启动与推理/训练冒烟回退 CPU，有提示、不崩溃
- [ ] 界面与路径：**中文文案**正常；含中文/Unicode 的文件名读写无异常（与 TestIOUtils 一致）

---

### 8. 已知限制与注意事项

- **GUI 与重度训练**不要求在 CI 中自动跑；本指南第 3–6 节以人工或带显示/`xvfb-run` 的 Agent 执行为准。
- **首次**下载 `yolov8n.pt` 与部分依赖资源**需要联网**。
- 完整训练耗时长：验收只用**少 epoch、小数据集**冒烟，勿默认跑满推荐轮数。
- 无图形显示的 Linux：务必 `export MPLBACKEND=Agg`，GUI 用 `xvfb-run -a python main.py …`。
- 设置保存后主窗口立即刷新；**已打开的工具窗口**需关闭后重开才能完全套用新字体/缩放。
- 默认 `yolov8n.pt` 为 COCO 类别；自定义业务类别需先自训再标注/对比。

## 环境要求

- Python 3.10+
- 依赖见 `requirements.txt`
- 可选：NVIDIA GPU + CUDA，以加速训练/推理
