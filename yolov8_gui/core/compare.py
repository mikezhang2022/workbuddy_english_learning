"""Model comparison engine."""

from __future__ import annotations

import time
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple

import cv2
import numpy as np

from .io_utils import MediaItem, imread_unicode
from .yolo_engine import Det, Dets, Predictor, draw_dets


@dataclass
class CompareStats:
    count_a: int = 0
    count_b: int = 0
    agreement: int = 0
    only_a: int = 0
    only_b: int = 0
    mean_iou: float = 0.0
    class_counts_a: Dict[str, int] = field(default_factory=dict)
    class_counts_b: Dict[str, int] = field(default_factory=dict)
    conf_a: List[float] = field(default_factory=list)
    conf_b: List[float] = field(default_factory=list)
    missed_by_a: int = 0
    missed_by_b: int = 0
    fp_a: int = 0
    fp_b: int = 0
    disagree_sizes: List[float] = field(default_factory=list)
    ms_a: float = 0.0
    ms_b: float = 0.0


@dataclass
class CompareReport:
    stats: CompareStats = field(default_factory=CompareStats)
    overlay_image: Optional[np.ndarray] = None
    analysis: str = ""
    suggestions: List[str] = field(default_factory=list)
    frames_compared: int = 0


def _iou(a: Det, b: Det) -> float:
    ax1, ay1, ax2, ay2 = a.xyxy
    bx1, by1, bx2, by2 = b.xyxy
    ix1, iy1 = max(ax1, bx1), max(ay1, by1)
    ix2, iy2 = min(ax2, bx2), min(ay2, by2)
    inter = max(0, ix2 - ix1) * max(0, iy2 - iy1)
    if inter <= 0:
        return 0.0
    area_a = a.area
    area_b = b.area
    union = area_a + area_b - inter
    return inter / union if union > 0 else 0.0


def _match_boxes(dets_a: Dets, dets_b: Dets, iou_thresh: float = 0.5) -> Tuple[List, List, List]:
    matched_a, matched_b, pairs = set(), set(), []
    for i, da in enumerate(dets_a):
        best_j, best_iou = -1, 0.0
        for j, db in enumerate(dets_b):
            if j in matched_b:
                continue
            if da.cls_id != db.cls_id:
                continue
            iou = _iou(da, db)
            if iou > best_iou:
                best_iou, best_j = iou, j
        if best_j >= 0 and best_iou >= iou_thresh:
            matched_a.add(i)
            matched_b.add(best_j)
            pairs.append((i, best_j, best_iou))
    only_a = [i for i in range(len(dets_a)) if i not in matched_a]
    only_b = [j for j in range(len(dets_b)) if j not in matched_b]
    return pairs, only_a, only_b


def _compare_frame(dets_a: Dets, dets_b: Dets, stats: CompareStats) -> None:
    stats.count_a += len(dets_a)
    stats.count_b += len(dets_b)
    for d in dets_a:
        stats.class_counts_a[d.cls_name] = stats.class_counts_a.get(d.cls_name, 0) + 1
        stats.conf_a.append(d.conf)
    for d in dets_b:
        stats.class_counts_b[d.cls_name] = stats.class_counts_b.get(d.cls_name, 0) + 1
        stats.conf_b.append(d.conf)

    pairs, only_a, only_b = _match_boxes(dets_a, dets_b)
    stats.agreement += len(pairs)
    stats.only_a += len(only_a)
    stats.only_b += len(only_b)
    stats.missed_by_a += len(only_b)
    stats.missed_by_b += len(only_a)
    stats.fp_a += len(only_a)
    stats.fp_b += len(only_b)

    ious = [p[2] for p in pairs]
    if ious:
        prev = stats.mean_iou
        stats.mean_iou = (prev * (stats.agreement - len(pairs)) + sum(ious)) / max(1, stats.agreement)

    for idx in only_a:
        stats.disagree_sizes.append(dets_a[idx].area)
    for idx in only_b:
        stats.disagree_sizes.append(dets_b[idx].area)


def _build_overlay(
    image: np.ndarray,
    dets_a: Dets,
    dets_b: Dets,
    pairs: list,
    only_a: list,
    only_b: list,
) -> np.ndarray:
    h, w = image.shape[:2]
    left = draw_dets(image, dets_a)
    right = draw_dets(image, dets_b)

    # diff highlight on right copy
    diff = image.copy()
    for i in only_a:
        d = dets_a[i]
        x1, y1, x2, y2 = [int(v) for v in d.xyxy]
        cv2.rectangle(diff, (x1, y1), (x2, y2), (243, 139, 168), 3)
        cv2.putText(diff, "A only", (x1, y1 - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (243, 139, 168), 1)
    for j in only_b:
        d = dets_b[j]
        x1, y1, x2, y2 = [int(v) for v in d.xyxy]
        cv2.rectangle(diff, (x1, y1), (x2, y2), (249, 226, 175), 3)
        cv2.putText(diff, "B only", (x1, y1 - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (249, 226, 175), 1)
    for _i, _j, iou in pairs:
        pass  # agreement shown in side-by-side

    panel = np.zeros((h, w * 3 + 20, 3), dtype=np.uint8)
    panel[:, :w] = left
    panel[:, w + 10 : w * 2 + 10] = right
    panel[:, w * 2 + 20 :] = diff

    cv2.putText(panel, "Model A", (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (137, 180, 250), 2)
    cv2.putText(panel, "Model B", (w + 20, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (166, 227, 161), 2)
    cv2.putText(panel, "Diff", (w * 2 + 30, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (243, 139, 168), 2)
    return panel


def _generate_analysis(stats: CompareStats, name_a: str, name_b: str) -> Tuple[str, List[str]]:
    lines = [
        f"=== Comparison: {name_a} vs {name_b} ===",
        f"Total detections — A: {stats.count_a}, B: {stats.count_b}",
        f"Matched (agreement): {stats.agreement}",
        f"Only A: {stats.only_a} | Only B: {stats.only_b}",
        f"Mean IoU (matched): {stats.mean_iou:.3f}",
        f"Inference — A: {stats.ms_a:.1f} ms/img, B: {stats.ms_b:.1f} ms/img",
        f"Missed by A (B found): {stats.missed_by_a}",
        f"Missed by B (A found): {stats.missed_by_b}",
    ]
    if stats.class_counts_a or stats.class_counts_b:
        lines.append("Class distribution A: " + str(dict(stats.class_counts_a)))
        lines.append("Class distribution B: " + str(dict(stats.class_counts_b)))
    if stats.conf_a:
        lines.append(f"Avg confidence A: {np.mean(stats.conf_a):.3f}")
    if stats.conf_b:
        lines.append(f"Avg confidence B: {np.mean(stats.conf_b):.3f}")

    suggestions = []
    if stats.ms_a < stats.ms_b * 0.8:
        suggestions.append(f"{name_a} is significantly faster; consider for deployment.")
    elif stats.ms_b < stats.ms_a * 0.8:
        suggestions.append(f"{name_b} is significantly faster; consider for deployment.")

    if stats.only_a > stats.only_b * 1.5:
        suggestions.append(f"{name_a} produces more unique detections — may have higher recall or more FP.")
    elif stats.only_b > stats.only_a * 1.5:
        suggestions.append(f"{name_b} produces more unique detections — review precision/recall tradeoff.")

    if stats.mean_iou < 0.5 and stats.agreement > 0:
        suggestions.append("Low IoU on matches — models localize differently; review training data.")

    if stats.missed_by_a > stats.missed_by_b:
        suggestions.append(f"{name_b} detects objects {name_a} misses — consider ensembling or retrain A.")
    elif stats.missed_by_b > stats.missed_by_a:
        suggestions.append(f"{name_a} detects objects {name_b} misses — consider ensembling or retrain B.")

    if not suggestions:
        suggestions.append("Models are broadly similar; choose based on speed vs accuracy needs.")

    return "\n".join(lines), suggestions


def compare(
    model_a_path: Optional[str],
    model_b_path: Optional[str],
    media: MediaItem,
    device: int | str = "cpu",
    conf: float = 0.25,
) -> CompareReport:
    report = CompareReport()
    stats = CompareStats()

    pred_a = Predictor(model_a_path, device)
    pred_b = Predictor(model_b_path, device)
    pred_a.load()
    pred_b.load()

    name_a = model_a_path or "default"
    name_b = model_b_path or "default"

    frames: List[np.ndarray] = []
    if media.kind == "image":
        img = imread_unicode(media.path)
        if img is not None:
            frames = [img]
    else:
        cap = cv2.VideoCapture(media.path)
        max_frames = 30
        while len(frames) < max_frames:
            ret, frame = cap.read()
            if not ret:
                break
            frames.append(frame)
        cap.release()

    if not frames:
        report.analysis = "Could not load media."
        return report

    total_ms_a = total_ms_b = 0.0
    last_overlay = None

    for frame in frames:
        t0 = time.perf_counter()
        dets_a = pred_a.predict_image(frame, conf=conf)
        total_ms_a += (time.perf_counter() - t0) * 1000

        t0 = time.perf_counter()
        dets_b = pred_b.predict_image(frame, conf=conf)
        total_ms_b += (time.perf_counter() - t0) * 1000

        _compare_frame(dets_a, dets_b, stats)
        pairs, only_a, only_b = _match_boxes(dets_a, dets_b)
        last_overlay = _build_overlay(frame, dets_a, dets_b, pairs, only_a, only_b)
        report.frames_compared += 1

    n = max(1, report.frames_compared)
    stats.ms_a = total_ms_a / n
    stats.ms_b = total_ms_b / n
    report.stats = stats
    report.overlay_image = last_overlay
    report.analysis, report.suggestions = _generate_analysis(stats, name_a, name_b)
    return report


def export_report_html(report: CompareReport, path: str, chart_path: Optional[str] = None) -> None:
    html = f"""<!DOCTYPE html>
<html><head><meta charset="utf-8"><title>YOLO Compare Report</title>
<style>body{{font-family:sans-serif;background:#1e1e2e;color:#cdd6f4;padding:20px}}
pre{{background:#2a2a3d;padding:12px;border-radius:8px}}ul{{line-height:1.8}}</style></head>
<body><h1>Model Comparison Report</h1>
<pre>{report.analysis}</pre>
<h2>Suggestions</h2><ul>{"".join(f"<li>{s}</li>" for s in report.suggestions)}</ul>
"""
    if chart_path:
        html += f'<h2>Chart</h2><img src="{chart_path}" style="max-width:100%"/>'
    html += "</body></html>"
    with open(path, "w", encoding="utf-8") as f:
        f.write(html)
