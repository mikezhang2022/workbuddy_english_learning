"""Unicode-safe I/O and media helpers."""

from __future__ import annotations

import os
import shutil
from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Literal, Optional

import cv2
import numpy as np

IMAGE_EXTS = {".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff"}
VIDEO_EXTS = {".mp4", ".avi", ".mov", ".mkv", ".wmv", ".webm"}


def imread_unicode(path: str | Path, flags: int = cv2.IMREAD_COLOR) -> Optional[np.ndarray]:
    """Read image with unicode path support."""
    path = str(path)
    try:
        data = np.fromfile(path, dtype=np.uint8)
        if data.size == 0:
            return None
        img = cv2.imdecode(data, flags)
        return img
    except Exception:
        return None


def imwrite_unicode(path: str | Path, image: np.ndarray, ext: str = ".jpg") -> bool:
    """Write image with unicode path support."""
    path = str(path)
    try:
        ok, buf = cv2.imencode(ext, image)
        if not ok:
            return False
        buf.tofile(path)
        return True
    except Exception:
        return False


@dataclass
class MediaItem:
    path: str
    kind: Literal["image", "video"]
    dets: Optional[list] = None
    frames: Optional[list] = field(default=None)

    @property
    def name(self) -> str:
        return os.path.basename(self.path)

    @property
    def stem(self) -> str:
        return Path(self.path).stem


def is_image(path: str) -> bool:
    return Path(path).suffix.lower() in IMAGE_EXTS


def is_video(path: str) -> bool:
    return Path(path).suffix.lower() in VIDEO_EXTS


def list_media(directory: str | Path) -> List[MediaItem]:
    directory = Path(directory)
    items: List[MediaItem] = []
    if not directory.is_dir():
        return items
    for root, _dirs, files in os.walk(directory):
        for fn in sorted(files):
            p = Path(root) / fn
            ext = p.suffix.lower()
            if ext in IMAGE_EXTS:
                items.append(MediaItem(path=str(p), kind="image"))
            elif ext in VIDEO_EXTS:
                items.append(MediaItem(path=str(p), kind="video"))
    return items


def extract_frames(
    video_path: str | Path,
    out_dir: str | Path,
    every_n: int = 1,
    max_frames: int = 0,
) -> List[str]:
    """Extract frames from video; returns list of saved image paths."""
    video_path = str(video_path)
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        return []
    saved: List[str] = []
    idx = 0
    frame_i = 0
    stem = Path(video_path).stem
    while True:
        ret, frame = cap.read()
        if not ret:
            break
        if idx % every_n == 0:
            out_path = out_dir / f"{stem}_f{frame_i:06d}.jpg"
            if imwrite_unicode(out_path, frame):
                saved.append(str(out_path))
                frame_i += 1
                if max_frames and frame_i >= max_frames:
                    break
        idx += 1
    cap.release()
    return saved


def copy_to_workspace(src: str | Path, dest_dir: Path) -> Path:
    dest_dir.mkdir(parents=True, exist_ok=True)
    src = Path(src)
    dest = dest_dir / src.name
    if src.is_dir():
        if dest.exists():
            shutil.rmtree(dest)
        shutil.copytree(src, dest)
    else:
        shutil.copy2(src, dest)
    return dest


def ensure_label_path(image_path: str | Path) -> Path:
    p = Path(image_path)
    return p.with_suffix(".txt")


def yolo_label_path_for(image_path: str | Path, labels_root: Optional[Path] = None) -> Path:
    if labels_root:
        rel_name = Path(image_path).stem + ".txt"
        return labels_root / rel_name
    return Path(image_path).with_suffix(".txt")
