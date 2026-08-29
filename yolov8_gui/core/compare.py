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
        cv2.rectangle(diff, (x1, y1), (x2, y2), (220, 38, 38), 3)
        cv2.putText(diff, "仅A", (x1, y1 - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (220, 38, 38), 1)
    for j in only_b:
        d = dets_b[j]
        x1, y1, x2, y2 = [int(v) for v in d.xyxy]
        cv2.rectangle(diff, (x1, y1), (x2, y2), (217, 119, 6), 3)
        cv2.putText(diff, "仅B", (x1, y1 - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (217, 119, 6), 1)
    for _i, _j, iou in pairs:
        pass  # agreement shown in side-by-side

    panel = np.zeros((h, w * 3 + 20, 3), dtype=np.uint8)
    panel[:, :w] = left
    panel[:, w + 10 : w * 2 + 10] = right
    panel[:, w * 2 + 20 :] = diff

    cv2.putText(panel, "Model A", (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (37, 99, 235), 2)
    cv2.putText(panel, "Model B", (w + 20, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (5, 150, 105), 2)
    cv2.putText(panel, "Diff", (w * 2 + 30, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (220, 38, 38), 2)
    return panel


def _generate_analysis(stats: CompareStats, name_a: str, name_b: str) -> Tuple[str, List[str]]:
    lines = [
        f"=== 对比：{name_a} vs {name_b} ===",
        f"检测总数 — A：{stats.count_a}，B：{stats.count_b}",
        f"匹配（一致）：{stats.agreement}",
        f"仅 A：{stats.only_a} | 仅 B：{stats.only_b}",
        f"平均 IoU（匹配）：{stats.mean_iou:.3f}",
        f"推理耗时 — A：{stats.ms_a:.1f} ms/图，B：{stats.ms_b:.1f} ms/图",
        f"A 漏检（B 检出）：{stats.missed_by_a}",
        f"B 漏检（A 检出）：{stats.missed_by_b}",
    ]
    if stats.class_counts_a or stats.class_counts_b:
        lines.append("类别分布 A：" + str(dict(stats.class_counts_a)))
        lines.append("类别分布 B：" + str(dict(stats.class_counts_b)))
    if stats.conf_a:
        lines.append(f"平均置信度 A：{np.mean(stats.conf_a):.3f}")
    if stats.conf_b:
        lines.append(f"平均置信度 B：{np.mean(stats.conf_b):.3f}")

    suggestions = []
    if stats.ms_a < stats.ms_b * 0.8:
        suggestions.append(f"{name_a} 明显更快，可考虑用于部署。")
    elif stats.ms_b < stats.ms_a * 0.8:
        suggestions.append(f"{name_b} 明显更快，可考虑用于部署。")

    if stats.only_a > stats.only_b * 1.5:
        suggestions.append(f"{name_a} 独有检测更多 — 可能召回更高，或误检更多。")
    elif stats.only_b > stats.only_a * 1.5:
        suggestions.append(f"{name_b} 独有检测更多 — 请权衡精确率/召回率。")

    if stats.mean_iou < 0.5 and stats.agreement > 0:
        suggestions.append("匹配框 IoU 偏低 — 两模型定位差异较大，请复查训练数据。")

    if stats.missed_by_a > stats.missed_by_b:
        suggestions.append(f"{name_b} 能检出 {name_a} 漏掉的目标 — 可考虑集成或重训 A。")
    elif stats.missed_by_b > stats.missed_by_a:
        suggestions.append(f"{name_a} 能检出 {name_b} 漏掉的目标 — 可考虑集成或重训 B。")

    if not suggestions:
        suggestions.append("两模型整体相近；可按速度与精度需求选型。")

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

    name_a = model_a_path or "默认模型"
    name_b = model_b_path or "默认模型"

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
        report.analysis = "无法加载媒体文件。"
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
<html><head><meta charset="utf-8"><title>YOLO 模型对比报告</title>
<style>body{{font-family:sans-serif;background:#f5f7fa;color:#1f2937;padding:20px}}
pre{{background:#ffffff;padding:12px;border-radius:8px;border:1px solid #d1d5db}}ul{{line-height:1.8}}</style></head>
<body><h1>模型对比报告</h1>
<pre>{report.analysis}</pre>
<h2>改进建议</h2><ul>{"".join(f"<li>{s}</li>" for s in report.suggestions)}</ul>
"""
    if chart_path:
        html += f'<h2>图表</h2><img src="{chart_path}" style="max-width:100%"/>'
    html += "</body></html>"
    with open(path, "w", encoding="utf-8") as f:
        f.write(html)
