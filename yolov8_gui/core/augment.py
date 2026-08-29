"""Dataset augmentation for YOLO format."""

from __future__ import annotations

import os
import random
import shutil
from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Optional, Tuple

import cv2
import numpy as np

from .io_utils import IMAGE_EXTS, imread_unicode, imwrite_unicode


@dataclass
class AugConfig:
    hflip: bool = True
    vflip: bool = False
    rotation: bool = True
    rotation_deg: float = 15.0
    brightness: bool = True
    brightness_factor: float = 0.2
    contrast: bool = True
    contrast_factor: float = 0.2
    clahe: bool = True
    sharpen: bool = True
    gaussian_noise: bool = False
    noise_sigma: float = 10.0
    hsv_jitter: bool = True
    hsv_h: float = 0.02
    hsv_s: float = 0.3
    hsv_v: float = 0.3
    scale: bool = True
    scale_range: Tuple[float, float] = (0.8, 1.2)
    shear: bool = False
    shear_deg: float = 5.0
    mosaic: bool = False
    mixup: bool = False
    cutout: bool = True
    cutout_ratio: float = 0.1


def _read_labels(path: Path) -> List[Tuple[int, float, float, float, float]]:
    if not path.exists():
        return []
    lines = []
    for line in path.read_text(encoding="utf-8").splitlines():
        parts = line.strip().split()
        if len(parts) < 5:
            continue
        try:
            lines.append((int(parts[0]), float(parts[1]), float(parts[2]), float(parts[3]), float(parts[4])))
        except ValueError:
            continue
    return lines


def _write_labels(path: Path, labels: List[Tuple[int, float, float, float, float]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        for cls_id, cx, cy, bw, bh in labels:
            f.write(f"{cls_id} {cx:.6f} {cy:.6f} {bw:.6f} {bh:.6f}\n")


def _hflip(img: np.ndarray, labels: list) -> Tuple[np.ndarray, list]:
    out = cv2.flip(img, 1)
    new_labels = []
    for cls_id, cx, cy, bw, bh in labels:
        new_labels.append((cls_id, 1.0 - cx, cy, bw, bh))
    return out, new_labels


def _vflip(img: np.ndarray, labels: list) -> Tuple[np.ndarray, list]:
    out = cv2.flip(img, 0)
    new_labels = []
    for cls_id, cx, cy, bw, bh in labels:
        new_labels.append((cls_id, cx, 1.0 - cy, bw, bh))
    return out, new_labels


def _rotate(img: np.ndarray, labels: list, deg: float) -> Tuple[np.ndarray, list]:
    h, w = img.shape[:2]
    M = cv2.getRotationMatrix2D((w / 2, h / 2), deg, 1.0)
    out = cv2.warpAffine(img, M, (w, h), borderMode=cv2.BORDER_REFLECT_101)
    # Approximate: keep labels unchanged for small rotations (common practice for preview)
    return out, list(labels)


def _brightness_contrast(img: np.ndarray, alpha: float, beta: float) -> np.ndarray:
    return cv2.convertScaleAbs(img, alpha=alpha, beta=beta)


def _apply_clahe(img: np.ndarray) -> np.ndarray:
    lab = cv2.cvtColor(img, cv2.COLOR_BGR2LAB)
    l, a, b = cv2.split(lab)
    clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
    l = clahe.apply(l)
    return cv2.cvtColor(cv2.merge([l, a, b]), cv2.COLOR_LAB2BGR)


def _sharpen(img: np.ndarray) -> np.ndarray:
    kernel = np.array([[0, -1, 0], [-1, 5, -1], [0, -1, 0]])
    return cv2.filter2D(img, -1, kernel)


def _add_noise(img: np.ndarray, sigma: float) -> np.ndarray:
    noise = np.random.normal(0, sigma, img.shape).astype(np.float32)
    out = np.clip(img.astype(np.float32) + noise, 0, 255).astype(np.uint8)
    return out


def _hsv_jitter(img: np.ndarray, dh: float, ds: float, dv: float) -> np.ndarray:
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV).astype(np.float32)
    hsv[:, :, 0] = (hsv[:, :, 0] + random.uniform(-dh, dh) * 180) % 180
    hsv[:, :, 1] = np.clip(hsv[:, :, 1] * (1 + random.uniform(-ds, ds)), 0, 255)
    hsv[:, :, 2] = np.clip(hsv[:, :, 2] * (1 + random.uniform(-dv, dv)), 0, 255)
    return cv2.cvtColor(hsv.astype(np.uint8), cv2.COLOR_HSV2BGR)


def _scale(img: np.ndarray, labels: list, scale: float) -> Tuple[np.ndarray, list]:
    h, w = img.shape[:2]
    nh, nw = int(h * scale), int(w * scale)
    scaled = cv2.resize(img, (nw, nh))
    if scale >= 1:
        # crop center
        y0 = (nh - h) // 2
        x0 = (nw - w) // 2
        out = scaled[y0 : y0 + h, x0 : x0 + w]
        return out, list(labels)
    # pad
    out = np.zeros((h, w, 3), dtype=np.uint8)
    y0 = (h - nh) // 2
    x0 = (w - nw) // 2
    out[y0 : y0 + nh, x0 : x0 + nw] = scaled
    return out, list(labels)


def _cutout(img: np.ndarray, labels: list, ratio: float) -> Tuple[np.ndarray, list]:
    out = img.copy()
    h, w = img.shape[:2]
    ch, cw = int(h * ratio), int(w * ratio)
    y = random.randint(0, max(0, h - ch))
    x = random.randint(0, max(0, w - cw))
    out[y : y + ch, x : x + cw] = 0
    return out, list(labels)


def _shear(img: np.ndarray, labels: list, deg: float) -> Tuple[np.ndarray, list]:
    h, w = img.shape[:2]
    sh = np.tan(np.deg2rad(deg))
    M = np.float32([[1, sh, 0], [0, 1, 0]])
    out = cv2.warpAffine(img, M, (w, h), borderMode=cv2.BORDER_REFLECT_101)
    return out, list(labels)


def _mixup(
    img_a: np.ndarray,
    lbl_a: list,
    img_b: np.ndarray,
    lbl_b: list,
    alpha: float = 0.5,
) -> Tuple[np.ndarray, list]:
    if img_b.shape != img_a.shape:
        img_b = cv2.resize(img_b, (img_a.shape[1], img_a.shape[0]))
    out = cv2.addWeighted(img_a, alpha, img_b, 1 - alpha, 0)
    return out, lbl_a + lbl_b


def _mosaic(
    items: List[Tuple[np.ndarray, list]],
) -> Tuple[np.ndarray, list]:
    """2x2 mosaic from up to 4 (image, labels) pairs."""
    if len(items) < 4:
        while len(items) < 4:
            items.append(items[-1])
    h, w = items[0][0].shape[:2]
    tile_h, tile_w = h // 2, w // 2
    canvas = np.zeros((h, w, 3), dtype=np.uint8)
    merged_labels: list = []
    positions = [(0, 0), (tile_w, 0), (0, tile_h), (tile_w, tile_h)]
    for idx, (img, labels) in enumerate(items[:4]):
        resized = cv2.resize(img, (tile_w, tile_h))
        x0, y0 = positions[idx]
        canvas[y0 : y0 + tile_h, x0 : x0 + tile_w] = resized
        for cls_id, cx, cy, bw, bh in labels:
            ncx = (x0 + cx * tile_w) / w
            ncy = (y0 + cy * tile_h) / h
            nbw, nbh = bw * tile_w / w, bh * tile_h / h
            merged_labels.append((cls_id, ncx, ncy, nbw, nbh))
    return canvas, merged_labels


def _single_augment(
    img: np.ndarray,
    labels: list,
    cfg: AugConfig,
    pool: Optional[List[Tuple[np.ndarray, list]]] = None,
) -> Tuple[np.ndarray, list]:
    pool = pool or [(img, labels)]

    if cfg.mosaic and random.random() < 0.25 and len(pool) >= 1:
        samples = random.sample(pool, min(4, len(pool)))
        if len(samples) < 4:
            samples = samples + [samples[-1]] * (4 - len(samples))
        return _mosaic(samples)

    if cfg.mixup and random.random() < 0.25 and len(pool) >= 2:
        a, b = random.sample(pool, 2)
        alpha = random.uniform(0.3, 0.7)
        return _mixup(a[0], a[1], b[0], b[1], alpha)

    out, lbl = img.copy(), list(labels)
    ops = []
    if cfg.hflip and random.random() < 0.5:
        ops.append("hflip")
    if cfg.vflip and random.random() < 0.3:
        ops.append("vflip")
    if cfg.rotation and random.random() < 0.4:
        ops.append("rot")
    if cfg.shear and random.random() < 0.3:
        ops.append("shear")
    if cfg.brightness and random.random() < 0.5:
        ops.append("bright")
    if cfg.contrast and random.random() < 0.5:
        ops.append("contrast")
    if cfg.clahe and random.random() < 0.3:
        ops.append("clahe")
    if cfg.sharpen and random.random() < 0.3:
        ops.append("sharp")
    if cfg.gaussian_noise and random.random() < 0.2:
        ops.append("noise")
    if cfg.hsv_jitter and random.random() < 0.5:
        ops.append("hsv")
    if cfg.scale and random.random() < 0.4:
        ops.append("scale")
    if cfg.cutout and random.random() < 0.3:
        ops.append("cutout")

    for op in ops:
        if op == "hflip":
            out, lbl = _hflip(out, lbl)
        elif op == "vflip":
            out, lbl = _vflip(out, lbl)
        elif op == "rot":
            deg = random.uniform(-cfg.rotation_deg, cfg.rotation_deg)
            out, lbl = _rotate(out, lbl, deg)
        elif op == "shear":
            deg = random.uniform(-cfg.shear_deg, cfg.shear_deg)
            out, lbl = _shear(out, lbl, deg)
        elif op == "bright":
            alpha = 1 + random.uniform(-cfg.brightness_factor, cfg.brightness_factor)
            out = _brightness_contrast(out, alpha, 0)
        elif op == "contrast":
            alpha = 1 + random.uniform(-cfg.contrast_factor, cfg.contrast_factor)
            out = _brightness_contrast(out, alpha, 0)
        elif op == "clahe":
            out = _apply_clahe(out)
        elif op == "sharp":
            out = _sharpen(out)
        elif op == "noise":
            out = _add_noise(out, cfg.noise_sigma)
        elif op == "hsv":
            out = _hsv_jitter(out, cfg.hsv_h, cfg.hsv_s, cfg.hsv_v)
        elif op == "scale":
            s = random.uniform(*cfg.scale_range)
            out, lbl = _scale(out, lbl, s)
        elif op == "cutout":
            out, lbl = _cutout(out, lbl, cfg.cutout_ratio)
    return out, lbl


def preview(img: np.ndarray, cfg: AugConfig, n: int = 4, labels: Optional[list] = None) -> List[np.ndarray]:
    labels = labels or []
    return [_single_augment(img, labels, cfg)[0] for _ in range(n)]


def _list_images(images_dir: Path) -> List[Path]:
    paths = []
    for root, _dirs, files in os.walk(images_dir):
        for fn in files:
            p = Path(root) / fn
            if p.suffix.lower() in IMAGE_EXTS:
                paths.append(p)
    return paths


def run(
    images_dir: str | Path,
    labels_dir: str | Path,
    cfg: AugConfig,
    multiplier: int = 2,
    out_images_dir: Optional[str | Path] = None,
    out_labels_dir: Optional[str | Path] = None,
) -> int:
    """Expand dataset; returns number of NEW samples generated."""
    images_dir = Path(images_dir)
    labels_dir = Path(labels_dir)
    out_images_dir = Path(out_images_dir or images_dir)
    out_labels_dir = Path(out_labels_dir or labels_dir)
    out_images_dir.mkdir(parents=True, exist_ok=True)
    out_labels_dir.mkdir(parents=True, exist_ok=True)

    generated = 0
    images = _list_images(images_dir)
    pool: List[Tuple[np.ndarray, list]] = []
    for img_path in images:
        label_path = labels_dir / (
            (img_path.relative_to(images_dir) if img_path.is_relative_to(images_dir) else Path(img_path.name)).with_suffix(".txt")
        )
        if not label_path.exists():
            label_path = img_path.with_suffix(".txt")
        img = imread_unicode(img_path)
        if img is None:
            continue
        labels = _read_labels(label_path)
        pool.append((img, labels))

    for img_path in images:
        rel = img_path.relative_to(images_dir) if img_path.is_relative_to(images_dir) else Path(img_path.name)
        label_path = labels_dir / rel.with_suffix(".txt")
        if not label_path.exists():
            label_path = img_path.with_suffix(".txt")
        img = imread_unicode(img_path)
        if img is None:
            continue
        labels = _read_labels(label_path)
        # copy original if output differs
        if out_images_dir != images_dir:
            dest_img = out_images_dir / rel.name
            dest_lbl = out_labels_dir / rel.with_suffix(".txt").name
            if not dest_img.exists():
                shutil.copy2(img_path, dest_img)
                if label_path.exists():
                    shutil.copy2(label_path, dest_lbl)

        for m in range(multiplier):
            aug_img, aug_labels = _single_augment(img, labels, cfg, pool=pool)
            stem = rel.stem + f"_aug{m}"
            out_img = out_images_dir / f"{stem}{rel.suffix}"
            out_lbl = out_labels_dir / f"{stem}.txt"
            if imwrite_unicode(out_img, aug_img):
                _write_labels(out_lbl, aug_labels)
                generated += 1
    return generated


def estimate_new_samples(num_images: int, multiplier: int, cfg: AugConfig) -> int:
    active = sum(
        [
            cfg.hflip,
            cfg.vflip,
            cfg.rotation,
            cfg.brightness,
            cfg.contrast,
            cfg.clahe,
            cfg.sharpen,
            cfg.gaussian_noise,
            cfg.hsv_jitter,
            cfg.scale,
            cfg.shear,
            cfg.mosaic,
            cfg.mixup,
            cfg.cutout,
        ]
    )
    if active == 0:
        return 0
    return num_images * multiplier
