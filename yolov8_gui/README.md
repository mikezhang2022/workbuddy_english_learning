# YOLOv8 Visual Studio

A production-quality Python desktop application for YOLOv8 workflows: import & annotate data, train models, and compare two models side-by-side. Built with **Tkinter (ttk)**, **Ultralytics YOLOv8**, and **PyTorch**.

## Quick Start

```bash
cd yolov8_gui
python -m venv .venv
source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt
python main.py
```

Open a specific tool directly:

```bash
python main.py --tool image
python main.py --tool train
python main.py --tool compare
```

Set workspace via environment variable:

```bash
export YOLOV8_WORKSPACE=~/my_yolo_project
python main.py
```

## GPU Usage

- PyTorch CUDA is auto-detected at startup.
- When a GPU is available, training and inference use `device=0`.
- Without CUDA, the app falls back to CPU and shows a clear warning in the launcher — it will not crash.

## Default Model vs Custom Model

- If you do **not** provide a `.pt` file, the app loads **`yolov8n.pt`** (Ultralytics default, downloaded automatically).
- The UI clearly labels this as the **default COCO pretrained model** — class names are COCO categories.
- For your own classes, train a custom model in Tool 2 or supply your own `.pt` in Tool 1 / Tool 3.

## Features by Tool

### Tool 1 — Image / Video Processing
- Import single image, video, or folder (mixed media)
- Thumbnail list and preview
- Auto-annotate with custom or default model; write YOLO `.txt` labels
- Export annotated video
- **Data health check**: blur, exposure, duplicates, label issues
- **One-click optimize**: dedup, CLAHE, sharpen, label fix
- **Augmentation** with live preview and sample count estimate
- Export `train/val` split + `data.yaml`
- **Send to Training Tool** one-click handoff

### Tool 2 — Model Training
- **AUTO mode**: optimal hyperparameters with rationale
- **MANUAL mode**: edit epochs, lr0, batch, imgsz, optimizer, etc.
- Live **advice panel** with one-click fixes (batch/VRAM, lr, epochs, val split, class imbalance)
- Link back to Tool 1 for augmentation when data is insufficient
- Background training with live log and matplotlib loss/mAP charts
- Stop training; saves best weights to workspace

### Tool 3 — Model Comparison
- Load two `.pt` models (or default) on the same image/video
- Side-by-side visualization + diff overlay (agreement / only-A / only-B)
- Analysis: detection counts, class distribution, confidence, IoU, FP/FN, speed (ms/img)
- Improvement suggestions
- Export HTML, text, or chart PNG report

## Shared Workspace

All tools share state via the workspace folder (`~/yolov8_workspace` by default):

| File / key            | Purpose                          |
|-----------------------|----------------------------------|
| `workspace_state.json`| last dataset yaml, weights, models |
| `dataset/`            | images & labels from Tool 1      |
| `export/data.yaml`    | training-ready dataset           |
| `runs/train/`         | training outputs & best.pt       |

Typical flow: **Tool 1** (import → augment → export) → **Tool 2** (train) → **Tool 3** (compare old vs new weights).

## Project Tree

```
yolov8_gui/
├── main.py                 # Launcher + CLI
├── requirements.txt
├── README.md
├── __init__.py
├── core/
│   ├── __init__.py
│   ├── device.py           # GPU/CUDA probe
│   ├── theme.py            # Dark ttk theme
│   ├── context.py          # AppContext / workspace
│   ├── io_utils.py         # Unicode I/O, MediaItem
│   ├── yolo_engine.py      # Predictor, draw_dets
│   ├── dataset_qc.py       # Dataset health checks
│   ├── augment.py          # Augmentation pipeline
│   ├── train_engine.py     # Params & advice
│   ├── train_runner.py     # Background training
│   └── compare.py          # Model comparison engine
├── tools/
│   ├── __init__.py
│   ├── image_tool.py       # Tool 1 GUI
│   ├── train_tool.py       # Tool 2 GUI
│   └── compare_tool.py     # Tool 3 GUI
└── tests/
    └── smoke_test.py       # Headless logic tests
```

## Tests

```bash
python -m compileall yolov8_gui
python yolov8_gui/tests/smoke_test.py
```

Smoke tests cover imports, augment, dataset QC, compare IoU matching, train param validation, and unicode I/O. GUI and GPU-heavy paths are not required in CI.

## Requirements

- Python 3.10+
- See `requirements.txt` for Python packages
- Optional: NVIDIA GPU + CUDA for faster training/inference
