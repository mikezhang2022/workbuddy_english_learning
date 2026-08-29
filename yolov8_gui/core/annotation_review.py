"""AI 预标注 + 人工审核精修：加载/保存标注、预览叠加、类别映射与审核面板。

纯逻辑函数可在无头环境导入；UI 类 AnnotationReviewPanel 延迟导入 Tk/Pillow。
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Dict, List, Optional, Sequence, Tuple

import cv2
import numpy as np

from .io_utils import IMAGE_EXTS, imread_unicode
from .yolo_engine import Det, Dets, Predictor

# COCO 80 类名（默认模型回退）
COCO_NAMES: List[str] = [
    "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck",
    "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench",
    "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra",
    "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
    "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove",
    "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup",
    "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
    "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch",
    "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse",
    "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink",
    "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier",
    "toothbrush",
]

# 预览框调色板（BGR）
_PALETTE_BGR = [
    (250, 180, 137),
    (161, 227, 166),
    (175, 226, 249),
    (168, 139, 243),
    (247, 166, 203),
    (213, 226, 148),
    (100, 180, 255),
    (80, 200, 180),
]


@dataclass
class Annotation:
    """单条 YOLO 标注（归一化坐标）+ 可选置信度。"""

    cls_id: int
    cls_name: str
    conf: float
    x_center: float
    y_center: float
    width: float
    height: float

    def to_yolo_line(self, with_conf: bool = False) -> str:
        base = (
            f"{self.cls_id} {self.x_center:.6f} {self.y_center:.6f} "
            f"{self.width:.6f} {self.height:.6f}"
        )
        if with_conf:
            return f"{base} {self.conf:.6f}"
        return base

    def to_xyxy(self, img_w: int, img_h: int) -> Tuple[float, float, float, float]:
        bw = self.width * img_w
        bh = self.height * img_h
        cx = self.x_center * img_w
        cy = self.y_center * img_h
        return (cx - bw / 2, cy - bh / 2, cx + bw / 2, cy + bh / 2)

    def to_det(self, img_w: int, img_h: int) -> Det:
        return Det(
            cls_id=self.cls_id,
            cls_name=self.cls_name,
            conf=self.conf,
            xyxy=self.to_xyxy(img_w, img_h),
            xywhn=(self.x_center, self.y_center, self.width, self.height),
        )


Annotations = List[Annotation]


def remap_class_names_to_ids(
    names: Sequence[str],
    data_yaml_path: Optional[str | Path] = None,
) -> Dict[str, int]:
    """类别名 → ID。优先 data.yaml，否则用传入 names，再回退 COCO。"""
    loaded = load_class_names(data_yaml_path) if data_yaml_path else []
    source = loaded if loaded else list(names) if names else list(COCO_NAMES)
    if not source:
        source = ["object"]
    return {str(n): i for i, n in enumerate(source)}


def load_class_names(data_yaml_path: Optional[str | Path] = None) -> List[str]:
    """从 data.yaml 解析 names；失败则返回空列表。"""
    if not data_yaml_path:
        return []
    path = Path(data_yaml_path)
    if not path.is_file():
        return []
    try:
        text = path.read_text(encoding="utf-8")
    except OSError:
        return []

    m = re.search(r"names\s*:\s*\[([^\]]*)\]", text)
    if m:
        raw = m.group(1)
        parts = [p.strip().strip("'\"") for p in raw.split(",") if p.strip()]
        if parts:
            return parts

    block = re.search(r"names\s*:\s*\n((?:\s+\S.*\n?)+)", text)
    if block:
        names_map: Dict[int, str] = {}
        listed: List[str] = []
        for line in block.group(1).splitlines():
            lm = re.match(r"\s*(\d+)\s*:\s*(.+?)\s*$", line)
            if lm:
                names_map[int(lm.group(1))] = lm.group(2).strip().strip("'\"")
                continue
            lm2 = re.match(r"\s*-\s*(.+?)\s*$", line)
            if lm2:
                listed.append(lm2.group(1).strip().strip("'\""))
        if names_map:
            return [names_map[i] for i in sorted(names_map)]
        if listed:
            return listed
    return []


def resolve_class_names(
    data_yaml_path: Optional[str | Path] = None,
    predictor_names: Optional[dict] = None,
) -> List[str]:
    """解析可用类别名列表。"""
    names = load_class_names(data_yaml_path)
    if names:
        return names
    if predictor_names:
        out: Dict[int, str] = {}
        for k, v in predictor_names.items():
            try:
                out[int(k)] = str(v)
            except (TypeError, ValueError):
                continue
        if out:
            return [out[i] for i in sorted(out)]
    return list(COCO_NAMES)


def dets_to_annotations(dets: Dets, class_names: Optional[Sequence[str]] = None) -> Annotations:
    anns: Annotations = []
    for d in dets:
        name = d.cls_name
        if class_names and 0 <= d.cls_id < len(class_names):
            name = class_names[d.cls_id]
        cx, cy, bw, bh = d.xywhn
        anns.append(
            Annotation(
                cls_id=d.cls_id,
                cls_name=name,
                conf=float(d.conf),
                x_center=float(cx),
                y_center=float(cy),
                width=float(bw),
                height=float(bh),
            )
        )
    return anns


def load_annotations(
    image_path: str | Path,
    labels_dir: str | Path,
    class_names: Optional[Sequence[str]] = None,
) -> Annotations:
    """读取与图片同名的 YOLO txt；支持可选第 6 列置信度。"""
    image_path = Path(image_path)
    labels_dir = Path(labels_dir)
    label_path = labels_dir / (image_path.stem + ".txt")
    if not label_path.is_file():
        return []

    names = list(class_names) if class_names else list(COCO_NAMES)
    anns: Annotations = []
    try:
        lines = label_path.read_text(encoding="utf-8").splitlines()
    except OSError:
        return []

    for line in lines:
        parts = line.strip().split()
        if len(parts) < 5:
            continue
        try:
            cls_id = int(float(parts[0]))
            xc, yc, w, h = (float(parts[1]), float(parts[2]), float(parts[3]), float(parts[4]))
            conf = float(parts[5]) if len(parts) >= 6 else 1.0
        except ValueError:
            continue
        if names and 0 <= cls_id < len(names):
            cls_name = names[cls_id]
        elif names:
            cls_name = f"class_{cls_id}"
        else:
            cls_name = "object"
        anns.append(
            Annotation(
                cls_id=cls_id,
                cls_name=cls_name,
                conf=conf,
                x_center=xc,
                y_center=yc,
                width=w,
                height=h,
            )
        )
    return anns


def save_annotations(
    image_path: str | Path,
    labels_dir: str | Path,
    annotations: Annotations,
    with_conf: bool = False,
) -> Path:
    """保存 YOLO 格式标注到 labels/同名.txt，自动创建目录。"""
    image_path = Path(image_path)
    labels_dir = Path(labels_dir)
    labels_dir.mkdir(parents=True, exist_ok=True)
    label_path = labels_dir / (image_path.stem + ".txt")
    lines = [a.to_yolo_line(with_conf=with_conf) for a in annotations]
    content = ("\n".join(lines) + "\n") if lines else ""
    label_path.write_text(content, encoding="utf-8")
    return label_path


def delete_below_threshold(annotations: Annotations, threshold: float) -> Annotations:
    """删除置信度低于阈值的框，返回保留列表。"""
    return [a for a in annotations if a.conf >= threshold]


def count_below_threshold(annotations: Annotations, threshold: float) -> int:
    return sum(1 for a in annotations if a.conf < threshold)


def list_review_images(images_dir: str | Path) -> List[Path]:
    images_dir = Path(images_dir)
    if not images_dir.is_dir():
        return []
    return sorted(
        p for p in images_dir.iterdir()
        if p.is_file() and p.suffix.lower() in IMAGE_EXTS
    )


def label_exists(image_path: str | Path, labels_dir: str | Path) -> bool:
    return (Path(labels_dir) / (Path(image_path).stem + ".txt")).is_file()


def draw_preview(
    image_path: str | Path,
    annotations: Annotations,
    *,
    highlight_index: Optional[int] = None,
    conf_threshold: float = 0.25,
    class_names: Optional[Sequence[str]] = None,
) -> Optional[np.ndarray]:
    """在图片上叠加半透明标注框，返回 BGR 图像；失败返回 None。"""
    img = imread_unicode(image_path)
    if img is None:
        return None
    h, w = img.shape[:2]
    overlay = img.copy()
    names = list(class_names) if class_names else None

    for i, ann in enumerate(annotations):
        x1, y1, x2, y2 = [int(v) for v in ann.to_xyxy(w, h)]
        x1, y1 = max(0, x1), max(0, y1)
        x2, y2 = min(w - 1, x2), min(h - 1, y2)
        low = ann.conf < conf_threshold
        if low:
            color = (0, 0, 220)
        else:
            color = _PALETTE_BGR[ann.cls_id % len(_PALETTE_BGR)]
        thickness = 3 if highlight_index is not None and i == highlight_index else 2
        if highlight_index is not None and i == highlight_index:
            color = (0, 0, 255)
            thickness = 3
        cv2.rectangle(overlay, (x1, y1), (x2, y2), color, thickness)
        label = ann.cls_name
        if names and 0 <= ann.cls_id < len(names):
            label = names[ann.cls_id]
        text = f"{label} {ann.conf:.2f}"
        (tw, th), _ = cv2.getTextSize(text, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 1)
        ty = max(0, y1 - th - 6)
        cv2.rectangle(overlay, (x1, ty), (x1 + tw + 4, y1), color, -1)
        cv2.putText(
            overlay, text, (x1 + 2, y1 - 4),
            cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1, cv2.LINE_AA,
        )

    return cv2.addWeighted(overlay, 0.72, img, 0.28, 0)


def preannotate_if_missing(
    image_path: str | Path,
    labels_dir: str | Path,
    predictor: Predictor,
    class_names: Optional[Sequence[str]] = None,
    conf: float = 0.25,
) -> Annotations:
    """无标注时用模型预标注并落盘；已有标注则直接读取。"""
    image_path = Path(image_path)
    labels_dir = Path(labels_dir)
    if label_exists(image_path, labels_dir):
        return load_annotations(image_path, labels_dir, class_names)

    dets = predictor.predict_path(str(image_path), conf=conf)
    anns = dets_to_annotations(dets, class_names)
    save_annotations(image_path, labels_dir, anns, with_conf=True)
    return anns


# ---------------------------------------------------------------------------
# UI：标注审核面板（延迟导入 Tk，避免无头冒烟测试失败）
# ---------------------------------------------------------------------------


class AnnotationReviewPanel:
    """嵌入 Notebook 的「标注审核」三栏面板。"""

    def __init__(
        self,
        parent,
        *,
        images_dir: Path,
        labels_dir: Path,
        get_predictor: Callable[[], Predictor],
        get_model_path: Callable[[], Optional[str]],
        get_data_yaml: Callable[[], Optional[str]],
        root,
        on_status: Optional[Callable[[str], None]] = None,
    ) -> None:
        import tkinter as tk
        from tkinter import ttk

        from .theme import BG_CARD, BORDER, ERROR, FG, f, scaled

        self.tk = tk
        self.ttk = ttk
        self.messagebox = __import__("tkinter.messagebox", fromlist=["messagebox"])
        self.simpledialog = __import__("tkinter.simpledialog", fromlist=["simpledialog"])

        self.parent = parent
        self.images_dir = Path(images_dir)
        self.labels_dir = Path(labels_dir)
        self.get_predictor = get_predictor
        self.get_model_path = get_model_path
        self.get_data_yaml = get_data_yaml
        self.root = root
        self.on_status = on_status

        self.images: List[Path] = []
        self.index: int = 0
        self.annotations: Annotations = []
        self._dirty: bool = False
        self._dirty_count: int = 0
        self._photos: list = []
        self._highlight: Optional[int] = None
        self._loading: bool = False
        self._class_names: List[str] = list(COCO_NAMES)
        self._active: bool = False
        self._keys_installed: bool = False
        self._canvas_scale: float = 1.0
        self._canvas_offset: Tuple[int, int] = (0, 0)
        self._img_size: Tuple[int, int] = (1, 1)

        self.conf_var = tk.DoubleVar(value=0.25)
        self.low_only_var = tk.BooleanVar(value=False)
        self.progress_var = tk.StringVar(value="进度: 0 / 0")
        self.dir_var = tk.StringVar(value=f"目录: {self.images_dir}")
        self.panel_status = tk.StringVar(value="就绪")
        self.class_var = tk.StringVar()

        self._BG_CARD = BG_CARD
        self._BORDER = BORDER
        self._ERROR = ERROR
        self._FG = FG
        self._f = f
        self._scaled = scaled

        self._build()

    def _build(self) -> None:
        tk = self.tk
        ttk = self.ttk
        f = self._f
        scaled = self._scaled
        BG_CARD, BORDER, ERROR, FG = self._BG_CARD, self._BORDER, self._ERROR, self._FG

        outer = ttk.Frame(self.parent, padding=4)
        outer.pack(fill=tk.BOTH, expand=True)

        top = ttk.Frame(outer)
        top.pack(fill=tk.X, pady=(0, 4))

        ttk.Label(top, textvariable=self.dir_var, style="Dim.TLabel").pack(side=tk.LEFT, padx=4)
        ttk.Button(top, text="上一张 A", command=self.prev_image).pack(side=tk.LEFT, padx=2)
        ttk.Button(top, text="下一张 D", command=self.next_image).pack(side=tk.LEFT, padx=2)
        ttk.Button(top, text="保存 Ctrl+S", command=self.save_current, style="Accent.TButton").pack(
            side=tk.LEFT, padx=8
        )
        ttk.Button(top, text="刷新", command=self.reload).pack(side=tk.LEFT, padx=2)
        ttk.Label(top, textvariable=self.progress_var, font=f(16, "bold")).pack(side=tk.LEFT, padx=12)

        ttk.Label(top, text="置信度阈值:").pack(side=tk.LEFT, padx=(12, 2))
        scale = ttk.Scale(
            top, from_=0.05, to=0.90, variable=self.conf_var,
            orient=tk.HORIZONTAL, length=scaled(160),
            command=self._on_conf_change,
        )
        scale.pack(side=tk.LEFT)
        self.conf_label = ttk.Label(top, text="0.25", width=5)
        self.conf_label.pack(side=tk.LEFT, padx=2)
        ttk.Checkbutton(
            top, text="仅显示低置信度", variable=self.low_only_var,
            command=self._refresh_ann_list,
        ).pack(side=tk.LEFT, padx=8)

        paned = ttk.PanedWindow(outer, orient=tk.HORIZONTAL)
        paned.pack(fill=tk.BOTH, expand=True)

        left = ttk.Frame(paned)
        paned.add(left, weight=1)
        ttk.Label(left, text="图片列表", font=f(16, "bold")).pack(anchor=tk.W)
        self.img_tree = ttk.Treeview(left, columns=("name",), show="headings", selectmode="browse")
        self.img_tree.heading("name", text="文件名")
        self.img_tree.column("name", width=scaled(180), stretch=True)
        sb_l = ttk.Scrollbar(left, orient=tk.VERTICAL, command=self.img_tree.yview)
        self.img_tree.configure(yscrollcommand=sb_l.set)
        self.img_tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        sb_l.pack(side=tk.RIGHT, fill=tk.Y)
        self.img_tree.bind("<<TreeviewSelect>>", self._on_img_select)

        center = ttk.Frame(paned)
        paned.add(center, weight=3)
        ttk.Label(center, text="图片预览", font=f(16, "bold")).pack(anchor=tk.W)
        self.canvas = tk.Canvas(
            center, bg=BG_CARD, highlightthickness=1, highlightbackground=BORDER,
        )
        self.canvas.pack(fill=tk.BOTH, expand=True)
        self.canvas.bind("<Configure>", lambda _e: self._redraw_canvas())
        self.canvas.bind("<Motion>", self._on_canvas_motion)
        self.summary = tk.Text(
            center, height=4, bg=BG_CARD, fg=FG, wrap=tk.WORD,
            insertbackground=FG, highlightbackground=BORDER, relief=tk.FLAT,
            font=f(14),
        )
        self.summary.pack(fill=tk.X, pady=(4, 0))

        right = ttk.Frame(paned)
        paned.add(right, weight=2)
        ttk.Label(right, text="当前图标注", font=f(16, "bold")).pack(anchor=tk.W)

        ann_cols = ("idx", "cls", "conf", "xc", "yc", "w", "h")
        tree_wrap = ttk.Frame(right)
        tree_wrap.pack(side=tk.TOP, fill=tk.BOTH, expand=True)
        self.ann_tree = ttk.Treeview(
            tree_wrap, columns=ann_cols, show="headings", selectmode="browse", height=12,
        )
        headings = {
            "idx": "序号", "cls": "类别", "conf": "置信度",
            "xc": "x_center", "yc": "y_center", "w": "width", "h": "height",
        }
        widths = {"idx": 48, "cls": 90, "conf": 70, "xc": 70, "yc": 70, "w": 70, "h": 70}
        for c in ann_cols:
            self.ann_tree.heading(c, text=headings[c])
            self.ann_tree.column(c, width=scaled(widths[c]), stretch=(c == "cls"), anchor=tk.CENTER)
        sb_r = ttk.Scrollbar(tree_wrap, orient=tk.VERTICAL, command=self.ann_tree.yview)
        self.ann_tree.configure(yscrollcommand=sb_r.set)
        self.ann_tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        sb_r.pack(side=tk.RIGHT, fill=tk.Y)
        self.ann_tree.bind("<<TreeviewSelect>>", self._on_ann_select)
        self.ann_tree.bind("<Button-3>", self._on_ann_right_click)
        self.ann_tree.tag_configure("low", background="#fee2e2", foreground=ERROR)
        self.ann_tree.tag_configure("ok", background=BG_CARD, foreground=FG)

        btns = ttk.Frame(right)
        btns.pack(fill=tk.X, pady=4)
        ttk.Button(btns, text="删除选中", command=self.delete_selected).pack(fill=tk.X, pady=2)
        ttk.Label(btns, text="改类别：").pack(anchor=tk.W, pady=(6, 0))
        self.class_combo = ttk.Combobox(btns, textvariable=self.class_var, state="readonly")
        self.class_combo.pack(fill=tk.X, pady=2)
        ttk.Button(btns, text="应用类别修改", command=self.change_class).pack(fill=tk.X, pady=2)
        ttk.Button(
            btns, text="批量删低于阈值的框", command=self.batch_delete_low, style="Accent.TButton"
        ).pack(fill=tk.X, pady=6)

        ttk.Label(outer, textvariable=self.panel_status, style="Dim.TLabel").pack(fill=tk.X, pady=(4, 0))

        self._menu = tk.Menu(self.root, tearoff=0)
        self._menu.add_command(label="删除", command=self.delete_selected)
        self._menu.add_command(label="改类别…", command=self._prompt_change_class)

    def bind_shortcuts(self, widget) -> None:
        if self._keys_installed:
            return
        widget.bind_all("<KeyPress-a>", self._key_prev, add="+")
        widget.bind_all("<KeyPress-A>", self._key_prev, add="+")
        widget.bind_all("<KeyPress-d>", self._key_next, add="+")
        widget.bind_all("<KeyPress-D>", self._key_next, add="+")
        widget.bind_all("<Left>", self._key_prev, add="+")
        widget.bind_all("<Right>", self._key_next, add="+")
        widget.bind_all("<Control-s>", self._key_save, add="+")
        widget.bind_all("<Control-S>", self._key_save, add="+")
        self._keys_installed = True

    def unbind_shortcuts(self, widget) -> None:
        self._active = False

    def _key_prev(self, evt=None):
        if not self._active or self._should_ignore_key(evt):
            return
        self.prev_image()

    def _key_next(self, evt=None):
        if not self._active or self._should_ignore_key(evt):
            return
        self.next_image()

    def _key_save(self, evt=None):
        if not self._active or self._should_ignore_key(evt):
            return
        self.save_current()
        return "break"

    def _should_ignore_key(self, evt) -> bool:
        if evt is None:
            return False
        try:
            cls = evt.widget.winfo_class()
        except Exception:
            return False
        return cls in ("Entry", "TEntry", "Text", "TCombobox", "Spinbox", "TSpinbox")

    def on_tab_activated(self) -> None:
        self._active = True
        self.bind_shortcuts(self.root)
        self.reload()

    def on_tab_deactivated(self) -> None:
        if self._dirty and self.images:
            self.save_current(silent=True)
        self.unbind_shortcuts(self.root)

    def reload(self) -> None:
        if self._loading:
            return
        self.images_dir.mkdir(parents=True, exist_ok=True)
        self.labels_dir.mkdir(parents=True, exist_ok=True)
        self.dir_var.set(f"目录: {self.images_dir}")
        self._class_names = resolve_class_names(
            self.get_data_yaml(),
            self.get_predictor().names or None,
        )
        self.class_combo["values"] = self._class_names
        if self._class_names and not self.class_var.get():
            self.class_var.set(self._class_names[0])

        self.images = list_review_images(self.images_dir)
        self.img_tree.delete(*self.img_tree.get_children())
        for p in self.images:
            self.img_tree.insert("", self.tk.END, iid=str(p), values=(p.name,))

        if not self.images:
            self.annotations = []
            self._update_progress()
            self._refresh_ann_list()
            self._redraw_canvas()
            self._set_status("工作区 images/ 中暂无图片，请先导入并自动标注")
            return

        self.index = min(self.index, len(self.images) - 1)
        self._load_current(preannotate=True)

    def _load_current(self, preannotate: bool = False) -> None:
        import threading

        from .tk_safe import safe_ui

        if not self.images:
            return
        path = self.images[self.index]
        self._select_tree_image(path)

        if preannotate and not label_exists(path, self.labels_dir):
            self._set_status(f"正在预标注：{path.name} …")
            self._loading = True

            def work():
                try:
                    predictor = self.get_predictor()
                    mp = self.get_model_path()
                    predictor.load(mp or None)
                    anns = preannotate_if_missing(
                        path, self.labels_dir, predictor,
                        class_names=self._class_names,
                        conf=float(self.conf_var.get()),
                    )
                    err = None
                except Exception as exc:
                    anns = []
                    err = str(exc)

                def ui():
                    self._loading = False
                    if err:
                        self._set_status(f"预标注失败：{err}")
                        self.annotations = load_annotations(
                            path, self.labels_dir, self._class_names
                        )
                    else:
                        self.annotations = anns
                        self._set_status(f"已预标注 {path.name}，共 {len(anns)} 框")
                    self._dirty = False
                    self._highlight = None
                    self._refresh_ann_list()
                    self._redraw_canvas()
                    self._update_progress()
                    self._update_stats()

                safe_ui(self.root, ui)

            threading.Thread(target=work, daemon=True).start()
            return

        self.annotations = load_annotations(path, self.labels_dir, self._class_names)
        self._dirty = False
        self._highlight = None
        self._refresh_ann_list()
        self._redraw_canvas()
        self._update_progress()
        self._update_stats()

    def _select_tree_image(self, path: Path) -> None:
        iid = str(path)
        if self.img_tree.exists(iid):
            self.img_tree.selection_set(iid)
            self.img_tree.see(iid)

    def _on_img_select(self, _evt=None) -> None:
        sel = self.img_tree.selection()
        if not sel:
            return
        path = Path(sel[0])
        try:
            idx = self.images.index(path)
        except ValueError:
            return
        if self._dirty:
            self.save_current(silent=True)
        self.index = idx
        self._load_current(preannotate=True)

    def prev_image(self) -> None:
        if not self.images:
            return
        if self._dirty:
            self.save_current(silent=True)
        self.index = (self.index - 1) % len(self.images)
        self._load_current(preannotate=True)

    def next_image(self) -> None:
        if not self.images:
            return
        if self._dirty:
            self.save_current(silent=True)
        self.index = (self.index + 1) % len(self.images)
        self._load_current(preannotate=True)

    def _visible_indices(self) -> List[int]:
        thr = float(self.conf_var.get())
        low_only = self.low_only_var.get()
        out = []
        for i, a in enumerate(self.annotations):
            if low_only and a.conf >= thr:
                continue
            out.append(i)
        return out

    def _refresh_ann_list(self) -> None:
        self.ann_tree.delete(*self.ann_tree.get_children())
        thr = float(self.conf_var.get())
        for i in self._visible_indices():
            a = self.annotations[i]
            tag = "low" if a.conf < thr else "ok"
            self.ann_tree.insert(
                "", self.tk.END, iid=str(i),
                values=(
                    i + 1,
                    a.cls_name,
                    f"{a.conf:.2f}",
                    f"{a.x_center:.4f}",
                    f"{a.y_center:.4f}",
                    f"{a.width:.4f}",
                    f"{a.height:.4f}",
                ),
                tags=(tag,),
            )
        self._update_summary()
        self._update_stats()

    def _on_ann_select(self, _evt=None) -> None:
        sel = self.ann_tree.selection()
        if not sel:
            self._highlight = None
        else:
            try:
                self._highlight = int(sel[0])
                ann = self.annotations[self._highlight]
                if ann.cls_name in self._class_names:
                    self.class_var.set(ann.cls_name)
            except (ValueError, IndexError):
                self._highlight = None
        self._redraw_canvas()

    def _on_ann_right_click(self, evt) -> None:
        row = self.ann_tree.identify_row(evt.y)
        if row:
            self.ann_tree.selection_set(row)
            self._on_ann_select()
            try:
                self._menu.tk_popup(evt.x_root, evt.y_root)
            finally:
                self._menu.grab_release()

    def delete_selected(self) -> None:
        sel = self.ann_tree.selection()
        if not sel:
            self.messagebox.showinfo("删除", "请先选中要删除的标注框", parent=self.root)
            return
        try:
            idx = int(sel[0])
        except ValueError:
            return
        if 0 <= idx < len(self.annotations):
            del self.annotations[idx]
            self._dirty = True
            self._dirty_count += 1
            self._highlight = None
            self._refresh_ann_list()
            self._redraw_canvas()
            self._set_status(f"已删除第 {idx + 1} 框（未保存）")

    def change_class(self) -> None:
        sel = self.ann_tree.selection()
        if not sel:
            self.messagebox.showinfo("改类别", "请先选中要修改的标注框", parent=self.root)
            return
        new_name = self.class_var.get().strip()
        if not new_name:
            return
        name_to_id = remap_class_names_to_ids(self._class_names)
        if new_name not in name_to_id:
            self._class_names.append(new_name)
            self.class_combo["values"] = self._class_names
            name_to_id = remap_class_names_to_ids(self._class_names)
        new_id = name_to_id[new_name]
        try:
            idx = int(sel[0])
        except ValueError:
            return
        if 0 <= idx < len(self.annotations):
            self.annotations[idx].cls_id = new_id
            self.annotations[idx].cls_name = new_name
            self._dirty = True
            self._dirty_count += 1
            self._refresh_ann_list()
            self._redraw_canvas()
            self.ann_tree.selection_set(str(idx))
            self._set_status(f"已将第 {idx + 1} 框改为「{new_name}」（未保存）")

    def _prompt_change_class(self) -> None:
        if not self._class_names:
            return
        name = self.simpledialog.askstring(
            "改类别",
            "输入新类别名：\n" + "、".join(self._class_names[:20]),
            parent=self.root,
            initialvalue=self.class_var.get(),
        )
        if name:
            self.class_var.set(name)
            self.change_class()

    def batch_delete_low(self) -> None:
        thr = float(self.conf_var.get())
        before = len(self.annotations)
        self.annotations = delete_below_threshold(self.annotations, thr)
        removed = before - len(self.annotations)
        if removed == 0:
            self._set_status(f"没有低于 {thr:.2f} 的框")
            return
        self._dirty = True
        self._dirty_count += removed
        self._highlight = None
        self._refresh_ann_list()
        self._redraw_canvas()
        self._set_status(f"已批量删除 {removed} 个低置信度框（未保存）")

    def save_current(self, silent: bool = False) -> None:
        if not self.images:
            return
        path = self.images[self.index]
        save_annotations(path, self.labels_dir, self.annotations, with_conf=True)
        self._dirty = False
        n = len(self.annotations)
        msg = f"已保存 {n} 框 → {path.stem}.txt"
        self._set_status(msg)
        self._update_stats()
        if not silent:
            self.panel_status.set(msg)

    def _on_conf_change(self, _val=None) -> None:
        v = float(self.conf_var.get())
        self.conf_label.config(text=f"{v:.2f}")
        self._refresh_ann_list()
        self._redraw_canvas()

    def _redraw_canvas(self) -> None:
        from PIL import Image, ImageTk

        self.canvas.delete("all")
        if not self.images:
            self.canvas.create_text(
                20, 20, anchor=self.tk.NW, text="暂无图片", fill="#6b7280", font=self._f(16),
            )
            return
        path = self.images[self.index]
        preview = draw_preview(
            path, self.annotations,
            highlight_index=self._highlight,
            conf_threshold=float(self.conf_var.get()),
            class_names=self._class_names,
        )
        if preview is None:
            self.canvas.create_text(
                20, 20, anchor=self.tk.NW, text=f"无法读取：{path.name}",
                fill=self._ERROR, font=self._f(16),
            )
            return
        cw = max(self.canvas.winfo_width(), 100)
        ch = max(self.canvas.winfo_height(), 100)
        rgb = cv2.cvtColor(preview, cv2.COLOR_BGR2RGB)
        pil = Image.fromarray(rgb)
        iw, ih = pil.size
        scale = min(cw / iw, ch / ih, 1.0)
        nw, nh = max(1, int(iw * scale)), max(1, int(ih * scale))
        pil = pil.resize((nw, nh), Image.Resampling.LANCZOS)
        photo = ImageTk.PhotoImage(pil)
        self._photos = [photo]
        self._canvas_scale = scale
        self._canvas_offset = ((cw - nw) // 2, (ch - nh) // 2)
        self._img_size = (iw, ih)
        x0, y0 = self._canvas_offset
        self.canvas.create_image(x0, y0, anchor=self.tk.NW, image=photo)

    def _on_canvas_motion(self, evt) -> None:
        if not self.images or not self.annotations:
            return
        scale = self._canvas_scale
        x0, y0 = self._canvas_offset
        iw, ih = self._img_size
        px = (evt.x - x0) / scale
        py = (evt.y - y0) / scale
        if px < 0 or py < 0 or px > iw or py > ih:
            self.canvas.config(cursor="")
            return
        hit = None
        for i, a in enumerate(self.annotations):
            x1, y1, x2, y2 = a.to_xyxy(iw, ih)
            if x1 <= px <= x2 and y1 <= py <= y2:
                hit = i
                break
        if hit is not None:
            a = self.annotations[hit]
            self.canvas.config(cursor="hand2")
            self.panel_status.set(f"{a.cls_name}  conf={a.conf:.2f}")
        else:
            self.canvas.config(cursor="")

    def _update_summary(self) -> None:
        self.summary.delete("1.0", self.tk.END)
        if not self.annotations:
            self.summary.insert(self.tk.END, "（本图无标注框）")
            return
        thr = float(self.conf_var.get())
        lines = []
        for i, a in enumerate(self.annotations):
            flag = "⚠低置信度" if a.conf < thr else "✓"
            lines.append(
                f"{i + 1}. [{flag}] {a.cls_name} conf={a.conf:.2f} "
                f"xc={a.x_center:.3f} yc={a.y_center:.3f} w={a.width:.3f} h={a.height:.3f}"
            )
        self.summary.insert(self.tk.END, "\n".join(lines))

    def _update_progress(self) -> None:
        n = len(self.images)
        cur = self.index + 1 if n else 0
        self.progress_var.set(f"进度: {cur} / {n}")

    def _update_stats(self) -> None:
        n_img = len(self.images)
        cur = self.index + 1 if n_img else 0
        thr = float(self.conf_var.get())
        pending = count_below_threshold(self.annotations, thr)
        accepted = len(self.annotations) - pending
        dirty = "有未保存变更" if self._dirty else "已同步"
        text = (
            f"已加载 {n_img} 张，当前第 {cur} 张，"
            f"本图保留 {accepted} 框，待审（低置信度）{pending} 框，{dirty}"
        )
        self.panel_status.set(text)
        if self.on_status:
            self.on_status(text)

    def _set_status(self, msg: str) -> None:
        self.panel_status.set(msg)
        if self.on_status:
            self.on_status(msg)


__all__ = [
    "COCO_NAMES",
    "Annotation",
    "Annotations",
    "AnnotationReviewPanel",
    "count_below_threshold",
    "delete_below_threshold",
    "dets_to_annotations",
    "draw_preview",
    "label_exists",
    "list_review_images",
    "load_annotations",
    "load_class_names",
    "preannotate_if_missing",
    "remap_class_names_to_ids",
    "resolve_class_names",
    "save_annotations",
]
