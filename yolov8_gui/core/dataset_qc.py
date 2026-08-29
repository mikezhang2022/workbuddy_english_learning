"""Dataset quality control checks."""

from __future__ import annotations

import hashlib
import os
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple

import cv2
import numpy as np

from .io_utils import IMAGE_EXTS, imread_unicode, is_image, list_media


@dataclass
class Issue:
    kind: str
    path: str
    detail: str
    fixable: bool = True
    fix_action: str = ""


@dataclass
class DatasetReport:
    total_images: int = 0
    total_labels: int = 0
    blurry_count: int = 0
    dark_count: int = 0
    bright_count: int = 0
    duplicate_count: int = 0
    missing_label_count: int = 0
    empty_label_count: int = 0
    invalid_box_count: int = 0
    class_names: Dict[int, str] = field(default_factory=dict)
    class_counts: Dict[int, int] = field(default_factory=dict)
    issues: List[Issue] = field(default_factory=list)

    def summary(self) -> str:
        lines = [
            f"图片数：{self.total_images}",
            f"标签数：{self.total_labels}",
            f"模糊：{self.blurry_count}",
            f"过暗：{self.dark_count}",
            f"过亮：{self.bright_count}",
            f"近重复：{self.duplicate_count}",
            f"缺失标签：{self.missing_label_count}",
            f"空标签：{self.empty_label_count}",
            f"无效框：{self.invalid_box_count}",
        ]
        if self.class_counts:
            lines.append("类别分布：")
            for cid, cnt in sorted(self.class_counts.items()):
                name = self.class_names.get(cid, str(cid))
                lines.append(f"  [{cid}] {name}：{cnt}")
        return "\n".join(lines)


def _laplacian_variance(gray: np.ndarray) -> float:
    return float(cv2.Laplacian(gray, cv2.CV_64F).var())


def _perceptual_hash(img: np.ndarray, size: int = 8) -> str:
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY) if len(img.shape) == 3 else img
    resized = cv2.resize(gray, (size + 1, size), interpolation=cv2.INTER_AREA)
    diff = resized[:, 1:] > resized[:, :-1]
    return hashlib.md5(diff.tobytes()).hexdigest()


def _parse_yolo_label(path: Path, w: int, h: int) -> Tuple[List[Tuple], List[str]]:
    boxes = []
    errors = []
    if not path.exists():
        return boxes, ["missing"]
    text = path.read_text(encoding="utf-8").strip()
    if not text:
        return boxes, ["empty"]
    for ln, line in enumerate(text.splitlines(), 1):
        parts = line.strip().split()
        if len(parts) < 5:
            errors.append(f"第 {ln} 行：格式错误")
            continue
        try:
            cls_id = int(parts[0])
            vals = [float(x) for x in parts[1:5]]
        except ValueError:
            errors.append(f"第 {ln} 行：非数字")
            continue
        cx, cy, bw, bh = vals
        if not all(0 <= v <= 1 for v in vals):
            errors.append(f"第 {ln} 行：数值超出 [0,1]")
        if bw <= 0 or bh <= 0:
            errors.append(f"第 {ln} 行：框尺寸为零")
        boxes.append((cls_id, cx, cy, bw, bh))
    return boxes, errors


def check(
    images_dir: str | Path,
    labels_dir: Optional[str | Path] = None,
    blur_threshold: float = 100.0,
    dark_threshold: float = 40.0,
    bright_threshold: float = 220.0,
) -> DatasetReport:
    images_dir = Path(images_dir)
    labels_dir = Path(labels_dir) if labels_dir else images_dir
    report = DatasetReport()

    image_paths: List[Path] = []
    for root, _dirs, files in os.walk(images_dir):
        for fn in files:
            p = Path(root) / fn
            if p.suffix.lower() in IMAGE_EXTS:
                image_paths.append(p)

    report.total_images = len(image_paths)
    hash_map: Dict[str, List[str]] = defaultdict(list)
    class_counter: Counter = Counter()

    for img_path in image_paths:
        img = imread_unicode(img_path)
        if img is None:
            report.issues.append(Issue("read_error", str(img_path), "无法读取图片", False))
            continue

        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        lap = _laplacian_variance(gray)
        mean_brightness = float(gray.mean())

        if lap < blur_threshold:
            report.blurry_count += 1
            report.issues.append(
                Issue("blur", str(img_path), f"拉普拉斯方差={lap:.1f}", True, "sharpen")
            )

        if mean_brightness < dark_threshold:
            report.dark_count += 1
            report.issues.append(
                Issue("dark", str(img_path), f"均值={mean_brightness:.1f}", True, "clahe")
            )
        elif mean_brightness > bright_threshold:
            report.bright_count += 1
            report.issues.append(
                Issue("bright", str(img_path), f"均值={mean_brightness:.1f}", True, "brightness")
            )

        ph = _perceptual_hash(img)
        hash_map[ph].append(str(img_path))

        rel = img_path.relative_to(images_dir) if img_path.is_relative_to(images_dir) else img_path.name
        label_path = labels_dir / Path(rel).with_suffix(".txt")
        if not label_path.exists():
            label_path = img_path.with_suffix(".txt")
        boxes, errs = _parse_yolo_label(label_path, img.shape[1], img.shape[0])
        if "missing" in errs:
            report.missing_label_count += 1
            report.issues.append(Issue("missing_label", str(img_path), "无标签文件", True, "remove_or_label"))
        elif "empty" in errs:
            report.empty_label_count += 1
            report.issues.append(Issue("empty_label", str(label_path), "空标签", True, "remove"))
        else:
            report.total_labels += 1
            for cls_id, *_ in boxes:
                class_counter[cls_id] += 1
            for e in errs:
                report.invalid_box_count += 1
                report.issues.append(
                    Issue("invalid_box", str(label_path), e, True, "fix_label")
                )

    for paths in hash_map.values():
        if len(paths) > 1:
            report.duplicate_count += len(paths) - 1
            for p in paths[1:]:
                report.issues.append(
                    Issue("duplicate", p, f"与 {paths[0]} 重复", True, "dedup")
                )

    report.class_counts = dict(class_counter)
    return report


def apply_fixes(
    report: DatasetReport,
    images_dir: Path,
    labels_dir: Optional[Path] = None,
    actions: Optional[Set[str]] = None,
) -> int:
    """Apply one-click fixes; returns number of items fixed."""
    labels_dir = Path(labels_dir) if labels_dir else Path(images_dir)
    images_dir = Path(images_dir)
    fixed = 0
    actions = actions or {"dedup", "clahe", "sharpen", "fix_label", "brightness"}

    # 去重：不依赖报告里重复标记数量，直接扫描全部图片按感知哈希去重
    if "dedup" in actions:
        fixed += _dedup_images(report, images_dir, labels_dir)

    for issue in report.issues:
        if issue.fix_action not in actions or issue.fix_action == "dedup":
            continue
        path = Path(issue.path)
        if issue.fix_action == "clahe" and issue.kind == "dark":
            img = imread_unicode(path)
            if img is not None:
                lab = cv2.cvtColor(img, cv2.COLOR_BGR2LAB)
                l, a, b = cv2.split(lab)
                clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
                l = clahe.apply(l)
                out = cv2.cvtColor(cv2.merge([l, a, b]), cv2.COLOR_LAB2BGR)
                from .io_utils import imwrite_unicode

                imwrite_unicode(path, out)
                fixed += 1
        elif issue.fix_action == "sharpen" and issue.kind == "blur":
            img = imread_unicode(path)
            if img is not None:
                kernel = np.array([[0, -1, 0], [-1, 5, -1], [0, -1, 0]])
                out = cv2.filter2D(img, -1, kernel)
                from .io_utils import imwrite_unicode

                imwrite_unicode(path, out)
                fixed += 1
        elif issue.fix_action == "brightness" and issue.kind == "bright":
            img = imread_unicode(path)
            if img is not None:
                out = cv2.convertScaleAbs(img, alpha=0.85, beta=-10)
                from .io_utils import imwrite_unicode

                imwrite_unicode(path, out)
                fixed += 1
        elif issue.fix_action == "fix_label" and path.suffix == ".txt":
            text = path.read_text(encoding="utf-8")
            new_lines = []
            for line in text.splitlines():
                parts = line.strip().split()
                if len(parts) < 5:
                    continue
                try:
                    cls_id = int(parts[0])
                    vals = [max(0, min(1, float(x))) for x in parts[1:5]]
                    cx, cy, bw, bh = vals
                    if bw <= 0 or bh <= 0:
                        continue
                    new_lines.append(f"{cls_id} {cx:.6f} {cy:.6f} {bw:.6f} {bh:.6f}")
                except ValueError:
                    continue
            path.write_text("\n".join(new_lines), encoding="utf-8")
            fixed += 1

    return fixed


def _label_path_for(img_path: Path, images_dir: Path, labels_dir: Path) -> Path:
    try:
        rel = img_path.relative_to(images_dir)
        candidate = labels_dir / Path(rel).with_suffix(".txt")
        if candidate.exists() or not img_path.with_suffix(".txt").exists():
            return candidate
    except ValueError:
        pass
    return img_path.with_suffix(".txt")


def _dedup_images(report: DatasetReport, images_dir: Path, labels_dir: Path) -> int:
    """保留每组感知哈希的第一张，删除其后所有完全相同的重复图及标签。"""
    image_paths: List[Path] = []
    for root, _dirs, files in os.walk(images_dir):
        for fn in files:
            p = Path(root) / fn
            if p.suffix.lower() in IMAGE_EXTS:
                image_paths.append(p)

    seen: Dict[str, Path] = {}
    deleted: Set[str] = set()
    fixed = 0

    for img_path in sorted(image_paths, key=lambda p: str(p)):
        img = imread_unicode(img_path)
        if img is None:
            continue
        ph = _perceptual_hash(img)
        if ph in seen:
            try:
                lbl = _label_path_for(img_path, images_dir, labels_dir)
                img_path.unlink(missing_ok=True)
                lbl.unlink(missing_ok=True)
                # 同目录旁路标签也尝试删除
                side = img_path.with_suffix(".txt")
                if side != lbl:
                    side.unlink(missing_ok=True)
                deleted.add(str(img_path))
                fixed += 1
            except Exception:
                pass
        else:
            seen[ph] = img_path

    if deleted:
        report.issues = [i for i in report.issues if i.path not in deleted]
        report.duplicate_count = max(0, report.duplicate_count - len(deleted))
        report.total_images = max(0, report.total_images - len(deleted))

    return fixed
