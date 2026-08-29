"""Training parameter defaults and validation advice."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional

import yaml

from .device import DeviceInfo


@dataclass
class AdviceItem:
    severity: str  # info, warn, error
    message: str
    fix_key: str
    fix_value: Any
    fix_label: str


def _parse_dataset_yaml(path: str) -> dict:
    with open(path, encoding="utf-8") as f:
        return yaml.safe_load(f) or {}


def _count_dataset_images(data: dict) -> tuple[int, int, dict]:
    root = Path(data.get("path", "."))
    train = data.get("train", "images/train")
    val = data.get("val", "images/val")
    names = data.get("names", {})
    if isinstance(names, list):
        names = {i: n for i, n in enumerate(names)}

    def count_split(split: str) -> int:
        p = root / split if not Path(split).is_absolute() else Path(split)
        if not p.exists():
            return 0
        exts = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}
        return sum(1 for f in p.rglob("*") if f.suffix.lower() in exts)

    train_n = count_split(train)
    val_n = count_split(val)
    return train_n, val_n, names


def default_params(dataset: Optional[str] = None) -> tuple[dict, dict]:
    """Return optimal params dict and rationale strings per key."""
    train_n, val_n, names = 0, 0, {}
    num_classes = 0
    if dataset and os.path.isfile(dataset):
        try:
            data = _parse_dataset_yaml(dataset)
            train_n, val_n, names = _count_dataset_images(data)
            num_classes = len(names) if names else 80
        except Exception:
            num_classes = 80
    else:
        num_classes = 80

    # Scale epochs/batch by dataset size
    if train_n < 100:
        epochs, batch, imgsz = 100, 8, 640
    elif train_n < 500:
        epochs, batch, imgsz = 80, 16, 640
    else:
        epochs, batch, imgsz = 50, 16, 640

    params = {
        "epochs": epochs,
        "lr0": 0.01,
        "batch": batch,
        "imgsz": imgsz,
        "optimizer": "SGD",
        "momentum": 0.937,
        "weight_decay": 0.0005,
        "warmup_epochs": 3.0,
        "patience": 20,
        "cos_lr": True,
        "close_mosaic": 10,
        "hsv_h": 0.015,
        "hsv_s": 0.7,
        "hsv_v": 0.4,
        "degrees": 0.0,
        "translate": 0.1,
        "scale": 0.5,
        "fliplr": 0.5,
        "mosaic": 1.0,
        "mixup": 0.0,
        "workers": 4,
        "pretrained": True,
        "model": "yolov8n.pt",
    }

    rationale = {
        "epochs": f"{epochs} epochs balances convergence vs overfit for ~{train_n} train images.",
        "lr0": "0.01 is YOLOv8 default SGD peak LR; works well for fine-tuning.",
        "batch": f"Batch {batch} fits typical GPU VRAM; increase if memory allows.",
        "imgsz": "640 is standard YOLO input; good speed/accuracy tradeoff.",
        "optimizer": "SGD with momentum is Ultralytics default and stable for detection.",
        "momentum": "0.937 matches YOLOv8 defaults.",
        "weight_decay": "0.0005 regularizes weights without underfitting small datasets.",
        "warmup_epochs": "3-epoch warmup stabilizes early training.",
        "patience": "Early stopping after 20 epochs without val improvement.",
    }
    return params, rationale


def build_train_args(params: dict, dataset_yaml: str, project: str, name: str, device: int | str) -> dict:
    args = {
        "data": dataset_yaml,
        "epochs": int(params.get("epochs", 50)),
        "lr0": float(params.get("lr0", 0.01)),
        "batch": int(params.get("batch", 16)),
        "imgsz": int(params.get("imgsz", 640)),
        "optimizer": params.get("optimizer", "SGD"),
        "momentum": float(params.get("momentum", 0.937)),
        "weight_decay": float(params.get("weight_decay", 0.0005)),
        "warmup_epochs": float(params.get("warmup_epochs", 3)),
        "patience": int(params.get("patience", 20)),
        "cos_lr": bool(params.get("cos_lr", True)),
        "close_mosaic": int(params.get("close_mosaic", 10)),
        "hsv_h": float(params.get("hsv_h", 0.015)),
        "hsv_s": float(params.get("hsv_s", 0.7)),
        "hsv_v": float(params.get("hsv_v", 0.4)),
        "degrees": float(params.get("degrees", 0)),
        "translate": float(params.get("translate", 0.1)),
        "scale": float(params.get("scale", 0.5)),
        "fliplr": float(params.get("fliplr", 0.5)),
        "mosaic": float(params.get("mosaic", 1.0)),
        "mixup": float(params.get("mixup", 0.0)),
        "workers": int(params.get("workers", 4)),
        "pretrained": bool(params.get("pretrained", True)),
        "project": project,
        "name": name,
        "device": device,
        "exist_ok": True,
        "verbose": True,
    }
    model = params.get("model", "yolov8n.pt")
    return {"model": model, **args}


def validate_params(params: dict, dataset: Optional[str], device_info: DeviceInfo) -> List[AdviceItem]:
    advice: List[AdviceItem] = []
    batch = int(params.get("batch", 16))
    lr0 = float(params.get("lr0", 0.01))
    epochs = int(params.get("epochs", 50))
    imgsz = int(params.get("imgsz", 640))

    vram = device_info.gpu_mem_total_gb or 0
    if device_info.cuda_available and vram > 0:
        est_gb = batch * (imgsz / 640) ** 2 * 0.25
        if est_gb > vram * 0.85:
            new_batch = max(1, batch // 2)
            advice.append(
                AdviceItem(
                    "error",
                    f"Batch {batch} likely too large for {vram:.1f} GB VRAM → reduce batch.",
                    "batch",
                    new_batch,
                    f"Set batch to {new_batch}",
                )
            )
    elif not device_info.cuda_available:
        if batch > 4:
            advice.append(
                AdviceItem(
                    "warn",
                    "Training on CPU with large batch will be very slow.",
                    "batch",
                    4,
                    "Set batch to 4 for CPU",
                )
            )

    if lr0 > 0.05:
        advice.append(
            AdviceItem("warn", "lr0 too high — may cause divergence.", "lr0", 0.01, "Set lr0 to 0.01")
        )
    elif lr0 < 0.0001:
        advice.append(
            AdviceItem("warn", "lr0 very low — training may stall.", "lr0", 0.001, "Set lr0 to 0.001")
        )

    if epochs < 20:
        advice.append(
            AdviceItem("warn", "Epochs too few for reliable convergence.", "epochs", 50, "Set epochs to 50")
        )

    train_n, val_n, names = 0, 0, {}
    if dataset and os.path.isfile(dataset):
        try:
            data = _parse_dataset_yaml(dataset)
            train_n, val_n, names = _count_dataset_images(data)
        except Exception:
            pass

        if val_n == 0:
            advice.append(
                AdviceItem(
                    "error",
                    "No validation split — metrics will be unreliable.",
                    "val_split",
                    0.2,
                    "Create 20% val split in Tool 1",
                )
            )

        if train_n < 50:
            advice.append(
                AdviceItem(
                    "warn",
                    f"Only {train_n} training images — augment data in Tool 1.",
                    "augment",
                    True,
                    "Send to Tool 1 for augmentation",
                )
            )

        # class imbalance
        if names:
            from .dataset_qc import check

            try:
                data_path = _parse_dataset_yaml(dataset)
                root = Path(data_path.get("path", "."))
                train_path = root / data_path.get("train", "images/train")
                if train_path.exists():
                    labels_dir = train_path.parent.parent / "labels" / train_path.name
                    if not labels_dir.exists():
                        labels_dir = train_path
                    rep = check(train_path, labels_dir if labels_dir.exists() else None)
                    if rep.class_counts:
                        counts = list(rep.class_counts.values())
                        if counts and max(counts) / max(min(counts), 1) > 10:
                            advice.append(
                                AdviceItem(
                                    "warn",
                                    "Severe class imbalance detected.",
                                    "augment",
                                    True,
                                    "Balance classes via augmentation",
                                )
                            )
            except Exception:
                pass

    return advice
