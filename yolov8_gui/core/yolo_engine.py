"""YOLO inference wrapper and detection utilities."""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, List, Optional, Tuple

import cv2
import numpy as np

from .io_utils import extract_frames, imread_unicode, imwrite_unicode

DEFAULT_MODEL = "yolov8n.pt"

OFFICIAL_MODELS = [
    "yolov8n.pt",
    "yolov8s.pt",
    "yolov8m.pt",
    "yolov8l.pt",
    "yolov8x.pt",
    "yolov8n-seg.pt",
    "yolov8n-cls.pt",
    "yolov8n-pose.pt",
]


@dataclass
class Det:
    cls_id: int
    cls_name: str
    conf: float
    xyxy: Tuple[float, float, float, float]  # x1,y1,x2,y2 pixel coords
    xywhn: Tuple[float, float, float, float] = field(default_factory=lambda: (0, 0, 0, 0))

    @property
    def area(self) -> float:
        x1, y1, x2, y2 = self.xyxy
        return max(0, x2 - x1) * max(0, y2 - y1)

    @property
    def center(self) -> Tuple[float, float]:
        x1, y1, x2, y2 = self.xyxy
        return ((x1 + x2) / 2, (y1 + y2) / 2)


Dets = List[Det]


def _xyxy_to_xywhn(xyxy: Tuple[float, float, float, float], w: int, h: int) -> Tuple[float, float, float, float]:
    x1, y1, x2, y2 = xyxy
    cx = ((x1 + x2) / 2) / w
    cy = ((y1 + y2) / 2) / h
    bw = (x2 - x1) / w
    bh = (y2 - y1) / h
    return (cx, cy, bw, bh)


def load_model(path_or_none: Optional[str] = None, device: int | str = "cpu"):
    """Load YOLO model; downloads default pretrained when path is None."""
    from ultralytics import YOLO

    if path_or_none and os.path.isfile(path_or_none):
        model = YOLO(path_or_none)
        model._is_default = False  # type: ignore[attr-defined]
        model._model_path = path_or_none  # type: ignore[attr-defined]
    else:
        model = YOLO(DEFAULT_MODEL)
        model._is_default = True  # type: ignore[attr-defined]
        model._model_path = DEFAULT_MODEL  # type: ignore[attr-defined]
    return model


def is_default_model(model) -> bool:
    return getattr(model, "_is_default", False)


def model_display_name(model) -> str:
    if is_default_model(model):
        return f"{DEFAULT_MODEL} (default COCO pretrained)"
    return getattr(model, "_model_path", "custom model")


class Predictor:
    """Thread-safe-ish wrapper around ultralytics YOLO predict."""

    def __init__(self, model_path: Optional[str] = None, device: int | str = "cpu") -> None:
        self.device = device
        self.model_path = model_path
        self.model = None
        self.names: dict = {}

    def load(self, model_path: Optional[str] = None) -> None:
        if model_path is not None:
            self.model_path = model_path
        self.model = load_model(self.model_path, self.device)
        self.names = self.model.names or {}

    def ensure_loaded(self) -> None:
        if self.model is None:
            self.load()

    def predict_image(self, image: np.ndarray, conf: float = 0.25, iou: float = 0.45) -> Dets:
        self.ensure_loaded()
        h, w = image.shape[:2]
        results = self.model.predict(source=image, conf=conf, iou=iou, device=self.device, verbose=False)
        return self._parse_results(results, w, h)

    def predict_path(self, path: str, conf: float = 0.25, iou: float = 0.45) -> Dets:
        img = imread_unicode(path)
        if img is None:
            return []
        return self.predict_image(img, conf=conf, iou=iou)

    def _parse_results(self, results, w: int, h: int) -> Dets:
        dets: Dets = []
        if not results:
            return dets
        r = results[0]
        if r.boxes is None:
            return dets
        boxes = r.boxes
        for i in range(len(boxes)):
            xyxy = boxes.xyxy[i].cpu().numpy().tolist()
            cls_id = int(boxes.cls[i].item())
            conf = float(boxes.conf[i].item())
            name = self.names.get(cls_id, str(cls_id))
            xywhn = _xyxy_to_xywhn(tuple(xyxy), w, h)
            dets.append(Det(cls_id=cls_id, cls_name=name, conf=conf, xyxy=tuple(xyxy), xywhn=xywhn))
        return dets

    def write_yolo_labels(self, dets: Dets, label_path: str | Path) -> None:
        label_path = Path(label_path)
        label_path.parent.mkdir(parents=True, exist_ok=True)
        lines = []
        for d in dets:
            cx, cy, bw, bh = d.xywhn
            lines.append(f"{d.cls_id} {cx:.6f} {cy:.6f} {bw:.6f} {bh:.6f}")
        with open(label_path, "w", encoding="utf-8") as f:
            f.write("\n".join(lines))


def draw_dets(
    image: np.ndarray,
    dets: Dets,
    classes: Optional[dict] = None,
    thickness: int = 2,
) -> np.ndarray:
    """Draw bounding boxes on image copy."""
    out = image.copy()
    palette = [
        (137, 180, 250),
        (166, 227, 161),
        (249, 226, 175),
        (243, 139, 168),
        (203, 166, 247),
        (148, 226, 213),
    ]
    for i, d in enumerate(dets):
        x1, y1, x2, y2 = [int(v) for v in d.xyxy]
        color = palette[d.cls_id % len(palette)]
        cv2.rectangle(out, (x1, y1), (x2, y2), color, thickness)
        label = d.cls_name
        if classes and d.cls_id in classes:
            label = classes[d.cls_id]
        text = f"{label} {d.conf:.2f}"
        (tw, th), _ = cv2.getTextSize(text, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 1)
        cv2.rectangle(out, (x1, y1 - th - 6), (x1 + tw + 4, y1), color, -1)
        cv2.putText(out, text, (x1 + 2, y1 - 4), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (30, 30, 46), 1)
    return out


def annotate_video(
    video_path: str,
    out_path: str,
    predictor: Predictor,
    conf: float = 0.25,
) -> bool:
    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        return False
    w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    fps = cap.get(cv2.CAP_PROP_FPS) or 25
    fourcc = cv2.VideoWriter_fourcc(*"mp4v")
    writer = cv2.VideoWriter(out_path, fourcc, fps, (w, h))
    while True:
        ret, frame = cap.read()
        if not ret:
            break
        dets = predictor.predict_image(frame, conf=conf)
        annotated = draw_dets(frame, dets, predictor.names)
        writer.write(annotated)
    cap.release()
    writer.release()
    return True


__all__ = [
    "DEFAULT_MODEL",
    "OFFICIAL_MODELS",
    "Det",
    "Dets",
    "Predictor",
    "annotate_video",
    "draw_dets",
    "extract_frames",
    "is_default_model",
    "load_model",
    "model_display_name",
]
