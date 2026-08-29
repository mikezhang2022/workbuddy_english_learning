"""工具 1 — 图片/视频处理。"""

from __future__ import annotations

import os
import random
import shutil
import threading
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk
from typing import List, Optional

import cv2
import numpy as np
from PIL import Image, ImageTk

from ..core.annotation_review import AnnotationReviewPanel
from ..core.augment import AugConfig, estimate_new_samples, preview, run as augment_run
from ..core.context import AppContext
from ..core.dataset_qc import apply_fixes, check as dataset_check
from ..core.io_utils import IMAGE_EXTS, MediaItem, extract_frames, imread_unicode, list_media
from ..core.theme import (
    ACCENT,
    BG,
    BG_CARD,
    BORDER,
    FG,
    FG_DIM,
    SELECT,
    WARN,
    center_window,
    configure_theme,
    f,
    get_font_size,
    get_ui_scale,
    scaled,
)
from ..core.tk_safe import safe_ui
from ..core.yolo_engine import (
    DEFAULT_MODEL,
    Predictor,
    annotate_video,
    draw_dets,
    is_default_model,
    model_display_name,
)

# 数据增强选项：内部字段名 → 中文显示名
AUG_LABELS = {
    "hflip": "水平翻转",
    "vflip": "垂直翻转",
    "rotation": "旋转",
    "brightness": "亮度",
    "contrast": "对比度",
    "clahe": "CLAHE 均衡",
    "sharpen": "锐化",
    "hsv_jitter": "HSV 抖动",
    "scale": "缩放",
    "cutout": "随机遮挡",
}


def _cv2_to_tk(
    img: np.ndarray,
    max_size: tuple | None = None,
    container: tk.Misc | None = None,
) -> ImageTk.PhotoImage:
    """按容器尺寸（优先宽度）自适应缩放，保持比例。"""
    rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    pil = Image.fromarray(rgb)
    if container is not None and max_size is None:
        try:
            container.update_idletasks()
            cw = int(container.winfo_width())
            ch = int(container.winfo_height())
            if cw < 80:
                cw = 640
            if ch < 80:
                ch = 480
            cw = max(cw - 8, 120)
            ch = max(ch - 8, 120)
            iw, ih = pil.size
            if iw > 0 and ih > 0:
                scale = cw / float(iw)
                nw = max(1, int(iw * scale))
                nh = max(1, int(ih * scale))
                if nh > ch:
                    scale = ch / float(ih)
                    nw = max(1, int(iw * scale))
                    nh = max(1, int(ih * scale))
                out = pil.resize((nw, nh), Image.Resampling.LANCZOS)
                return ImageTk.PhotoImage(out)
        except Exception:
            max_size = (640, 480)
    if max_size is None:
        max_size = (640, 480)
    out = pil.copy()
    out.thumbnail(max_size, Image.Resampling.LANCZOS)
    return ImageTk.PhotoImage(out)


class ImageTool:
    """YOLOv8 图片/视频导入、标注、质检、增强、导出。"""

    MIN_IMAGES = 50
    MIN_PER_CLASS = 5

    def __init__(self, ctx: AppContext, model_path: Optional[str] = None) -> None:
        self.ctx = ctx
        self.model_path = model_path
        self.items: List[MediaItem] = []
        self._photos: list = []
        self._preview_bgr: Optional[np.ndarray] = None
        self.dataset_dir = ctx.subdir("dataset")
        self.images_dir = self.dataset_dir / "images"
        self.labels_dir = self.dataset_dir / "labels"
        self.images_dir.mkdir(parents=True, exist_ok=True)
        self.labels_dir.mkdir(parents=True, exist_ok=True)

        self.win = tk.Toplevel()
        self.win.title("工具 1 — 图片/视频工具")
        configure_theme(self.win, font_size=get_font_size(), ui_scale=get_ui_scale())
        center_window(self.win, 1200, 800)

        device = ctx.device_info.device_arg()
        self.predictor = Predictor(model_path, device)

        self._build_ui()
        self.win.protocol("WM_DELETE_WINDOW", self.win.destroy)

    def _build_ui(self) -> None:
        top = ttk.Frame(self.win, padding=8)
        top.pack(fill=tk.X)

        ttk.Label(top, text="模型 (.pt)：", font=f(16)).pack(side=tk.LEFT)
        self.model_var = tk.StringVar(value=self.model_path or "")
        ttk.Entry(top, textvariable=self.model_var, width=40).pack(side=tk.LEFT, padx=4)
        ttk.Button(top, text="浏览", command=self._browse_model).pack(side=tk.LEFT)
        ttk.Button(top, text="使用默认", command=self._use_default).pack(side=tk.LEFT, padx=4)
        self.model_status = ttk.Label(top, text="", style="Dim.TLabel")
        self.model_status.pack(side=tk.LEFT, padx=8)

        btn_row = ttk.Frame(self.win, padding=4)
        btn_row.pack(fill=tk.X)
        ttk.Button(btn_row, text="导入图片", command=self._import_image).pack(side=tk.LEFT, padx=2)
        ttk.Button(btn_row, text="导入视频", command=self._import_video).pack(side=tk.LEFT, padx=2)
        ttk.Button(btn_row, text="导入文件夹", command=self._import_folder).pack(side=tk.LEFT, padx=2)
        ttk.Button(btn_row, text="自动标注", command=self._auto_annotate, style="Accent.TButton").pack(
            side=tk.LEFT, padx=8
        )
        ttk.Button(btn_row, text="导出标注视频", command=self._export_video).pack(side=tk.LEFT, padx=2)

        # 主 Notebook：标注与质检 | 标注审核
        self.main_nb = ttk.Notebook(self.win)
        self.main_nb.pack(fill=tk.BOTH, expand=True, padx=8, pady=4)

        work_tab = ttk.Frame(self.main_nb)
        self.main_nb.add(work_tab, text="标注与质检")

        paned = ttk.PanedWindow(work_tab, orient=tk.HORIZONTAL)
        paned.pack(fill=tk.BOTH, expand=True)

        left = ttk.Frame(paned)
        paned.add(left, weight=1)
        try:
            paned.paneconfigure(left, minsize=scaled(260))
        except Exception:
            pass
        ttk.Label(left, text="媒体列表", font=f(16, "bold")).pack(anchor=tk.W)
        self.listbox = tk.Listbox(
            left, bg=BG_CARD, fg=FG, selectbackground=SELECT, selectforeground=FG,
            highlightbackground=BORDER, relief=tk.FLAT, height=20,
            font=f(16),
        )
        self.listbox.pack(fill=tk.BOTH, expand=True)
        self.listbox.bind("<<ListboxSelect>>", self._on_select)

        center = ttk.Frame(paned)
        paned.add(center, weight=2)
        self.preview_frame = center
        self.preview_label = ttk.Label(center, text="请选择媒体以预览（双击可放大）")
        self.preview_label.pack(fill=tk.BOTH, expand=True)
        self.preview_label.bind("<Double-Button-1>", self._on_preview_double_click)

        right = ttk.Notebook(paned)
        paned.add(right, weight=1)

        qc_frame = ttk.Frame(right, padding=8)
        right.add(qc_frame, text="数据质检")
        ttk.Button(qc_frame, text="运行质检", command=self._run_qc).pack(fill=tk.X, pady=4)
        self.qc_text = tk.Text(
            qc_frame, height=12, bg=BG_CARD, fg=FG, wrap=tk.WORD,
            insertbackground=FG, highlightbackground=BORDER, relief=tk.FLAT,
        )
        self.qc_text.pack(fill=tk.BOTH, expand=True)
        ttk.Button(qc_frame, text="一键优化", command=self._optimize).pack(fill=tk.X, pady=4)

        aug_frame = ttk.Frame(right, padding=8)
        right.add(aug_frame, text="数据增强")
        self.aug_cfg = AugConfig()
        self._aug_vars = {}
        for name, label in AUG_LABELS.items():
            v = tk.BooleanVar(value=getattr(self.aug_cfg, name))
            self._aug_vars[name] = v
            ttk.Checkbutton(aug_frame, text=label, variable=v).pack(anchor=tk.W)
        ttk.Label(aug_frame, text="倍数：").pack(anchor=tk.W)
        self.mult_var = tk.IntVar(value=2)
        ttk.Spinbox(aug_frame, from_=1, to=10, textvariable=self.mult_var, width=6).pack(anchor=tk.W)
        self.aug_count_label = ttk.Label(aug_frame, text="预计新增样本：0")
        self.aug_count_label.pack(anchor=tk.W, pady=4)
        ttk.Button(aug_frame, text="预览增强效果", command=self._preview_aug).pack(fill=tk.X, pady=2)
        ttk.Button(aug_frame, text="运行数据增强", command=self._run_aug).pack(fill=tk.X, pady=2)

        export_frame = ttk.Frame(right, padding=8)
        right.add(export_frame, text="导出")
        ttk.Label(export_frame, text="最少图片阈值：").pack(anchor=tk.W)
        self.min_img_var = tk.IntVar(value=self.MIN_IMAGES)
        ttk.Spinbox(export_frame, from_=10, to=1000, textvariable=self.min_img_var, width=8).pack(anchor=tk.W)
        ttk.Button(export_frame, text="导出 train/val + data.yaml", command=self._export_dataset).pack(
            fill=tk.X, pady=8
        )
        ttk.Button(
            export_frame, text="发送到模型训练工具 →", command=self._send_to_train, style="Accent.TButton"
        ).pack(fill=tk.X)

        # 标注审核页签
        review_tab = ttk.Frame(self.main_nb)
        self.main_nb.add(review_tab, text="标注审核")
        self.review_panel = AnnotationReviewPanel(
            review_tab,
            images_dir=self.images_dir,
            labels_dir=self.labels_dir,
            get_predictor=lambda: self.predictor,
            get_model_path=lambda: self.model_path or self.model_var.get() or None,
            get_data_yaml=self._resolve_data_yaml,
            root=self.win,
            on_status=self._set_status,
        )
        self.main_nb.bind("<<NotebookTabChanged>>", self._on_main_tab_changed)

        self.status = ttk.Label(self.win, text="就绪", style="Dim.TLabel")
        self.status.pack(fill=tk.X, padx=8, pady=4)
        self._update_model_status()

    def _resolve_data_yaml(self) -> Optional[str]:
        """优先工作区导出的 data.yaml，其次 dataset/data.yaml。"""
        if self.ctx.last_dataset_yaml and Path(self.ctx.last_dataset_yaml).is_file():
            return self.ctx.last_dataset_yaml
        local = self.dataset_dir / "data.yaml"
        if local.is_file():
            return str(local)
        export_yaml = self.ctx.subdir("export") / "data.yaml"
        if export_yaml.is_file():
            return str(export_yaml)
        return None

    def _on_main_tab_changed(self, _evt=None) -> None:
        try:
            tab_id = self.main_nb.select()
            tab_text = self.main_nb.tab(tab_id, "text")
        except Exception:
            return
        if tab_text == "标注审核":
            self.review_panel.on_tab_activated()
        else:
            self.review_panel.on_tab_deactivated()

    def _browse_model(self) -> None:
        p = filedialog.askopenfilename(filetypes=[("PyTorch", "*.pt"), ("全部", "*.*")])
        if p:
            self.model_var.set(p)
            self.model_path = p
            self.predictor.model = None
            self._update_model_status()

    def _use_default(self) -> None:
        self.model_var.set("")
        self.model_path = None
        self.predictor.model = None
        self._update_model_status()

    def _update_model_status(self) -> None:
        if self.model_path:
            self.model_status.config(text="自定义模型", foreground=ACCENT)
        else:
            self.model_status.config(
                text=f"默认：{DEFAULT_MODEL}（COCO 类别）", foreground=WARN
            )

    def _set_status(self, msg: str) -> None:
        safe_ui(self.win, lambda: self.status.config(text=msg))

    def _import_image(self) -> None:
        paths = filedialog.askopenfilenames(
            filetypes=[("图片", "*.jpg *.jpeg *.png *.bmp *.webp"), ("全部", "*.*")]
        )
        for p in paths:
            dest = self._copy_media(p, "image")
            self.items.append(MediaItem(path=str(dest), kind="image"))
        self._refresh_list()

    def _import_video(self) -> None:
        p = filedialog.askopenfilename(
            filetypes=[("视频", "*.mp4 *.avi *.mov *.mkv"), ("全部", "*.*")]
        )
        if p:
            dest = self._copy_media(p, "video")
            self.items.append(MediaItem(path=str(dest), kind="video"))
            self._refresh_list()

    def _import_folder(self) -> None:
        d = filedialog.askdirectory()
        if d:
            for item in list_media(d):
                dest = self._copy_media(item.path, item.kind)
                self.items.append(MediaItem(path=str(dest), kind=item.kind))
            self._refresh_list()

    def _copy_media(self, src: str, kind: str) -> Path:
        sub = self.ctx.subdir("imports")
        dest = sub / Path(src).name
        if not dest.exists():
            shutil.copy2(src, dest)
        return dest

    def _refresh_list(self) -> None:
        self.listbox.delete(0, tk.END)
        kind_map = {"image": "图片", "video": "视频"}
        for it in self.items:
            kind_cn = kind_map.get(it.kind, it.kind)
            self.listbox.insert(tk.END, f"[{kind_cn}] {it.name}")
        self._update_aug_estimate()

    def _on_select(self, _evt=None) -> None:
        sel = self.listbox.curselection()
        if not sel:
            return
        item = self.items[sel[0]]
        if item.kind == "image":
            img = imread_unicode(item.path)
            if img is not None:
                if item.dets:
                    img = draw_dets(img, item.dets)
                self._show_preview(img)
        else:
            cap = cv2.VideoCapture(item.path)
            ret, frame = cap.read()
            cap.release()
            if ret:
                self._show_preview(frame)

    def _show_preview(self, img: np.ndarray) -> None:
        self._preview_bgr = img.copy()
        photo = _cv2_to_tk(img, container=self.preview_frame)
        self._photos = [photo]
        self.preview_label.config(image=photo, text="")

    def _on_preview_double_click(self, _evt=None) -> None:
        if self._preview_bgr is None:
            return
        self._open_enlarged_preview(self._preview_bgr)

    def _open_enlarged_preview(self, img: np.ndarray) -> None:
        top = tk.Toplevel(self.win)
        top.title("图片预览（双击关闭）")
        configure_theme(top, font_size=get_font_size(), ui_scale=get_ui_scale())
        photo = _cv2_to_tk(img, max_size=(1200, 800))
        lbl = ttk.Label(top, image=photo)
        lbl.image = photo  # type: ignore[attr-defined]
        lbl.pack(fill=tk.BOTH, expand=True)
        lbl.bind("<Double-Button-1>", lambda _e: top.destroy())
        top.bind("<Double-Button-1>", lambda _e: top.destroy())
        try:
            center_window(top, min(1200, photo.width() + 40), min(800, photo.height() + 60))
        except Exception:
            pass

    def _auto_annotate(self) -> None:
        def work():
            self._set_status("正在加载模型…")
            self.predictor.load(self.model_path or None)
            safe_ui(self.win, self._update_model_status)
            for i, item in enumerate(self.items):
                self._set_status(f"正在标注 {i+1}/{len(self.items)}：{item.name}")
                if item.kind == "image":
                    img = imread_unicode(item.path)
                    if img is None:
                        continue
                    dets = self.predictor.predict_image(img)
                    item.dets = dets
                    lbl = self.labels_dir / (Path(item.path).stem + ".txt")
                    self.predictor.write_yolo_labels(dets, lbl)
                    ds_img = self.images_dir / Path(item.path).name
                    if not ds_img.exists():
                        shutil.copy2(item.path, ds_img)
                else:
                    frames_dir = self.ctx.subdir("frames") / Path(item.path).stem
                    paths = extract_frames(item.path, frames_dir, every_n=5)
                    for fp in paths:
                        img = imread_unicode(fp)
                        if img is None:
                            continue
                        dets = self.predictor.predict_image(img)
                        lbl = self.labels_dir / (Path(fp).stem + ".txt")
                        self.predictor.write_yolo_labels(dets, lbl)
                        ds_img = self.images_dir / Path(fp).name
                        if not ds_img.exists():
                            shutil.copy2(fp, ds_img)
            self._set_status("标注完成")
            self._check_insufficient_data()

        threading.Thread(target=work, daemon=True).start()

    def _export_video(self) -> None:
        sel = self.listbox.curselection()
        if not sel:
            messagebox.showwarning("导出", "请先选择一个视频")
            return
        item = self.items[sel[0]]
        if item.kind != "video":
            messagebox.showwarning("导出", "所选项目不是视频")
            return
        out = filedialog.asksaveasfilename(defaultextension=".mp4", filetypes=[("MP4", "*.mp4")])

        def work():
            self._set_status("正在导出标注视频…")
            try:
                self.predictor.load(self.model_path or None)
                annotate_video(item.path, out, self.predictor)
                self._set_status("视频已导出")
                safe_ui(self.win, lambda: messagebox.showinfo("导出", f"视频已导出到：\n{out}"))
            except Exception as exc:
                err = str(exc)
                self._set_status(f"导出失败：{err}")
                safe_ui(
                    self.win,
                    lambda e=err: messagebox.showerror("视频写出失败", f"视频写出失败：{e}"),
                )

        if out:
            threading.Thread(target=work, daemon=True).start()

    def _run_qc(self) -> None:
        def work():
            self._set_status("正在运行质检…")
            report = dataset_check(self.images_dir, self.labels_dir)
            text = report.summary()

            def ui():
                self.qc_text.delete("1.0", tk.END)
                self.qc_text.insert(tk.END, text)
                self._last_report = report
                self.status.config(text="质检完成")

            safe_ui(self.win, ui)

        threading.Thread(target=work, daemon=True).start()

    def _optimize(self) -> None:
        if not hasattr(self, "_last_report"):
            messagebox.showinfo("优化", "请先运行质检")
            return

        def work():
            n = apply_fixes(self._last_report, self.images_dir, self.labels_dir)
            self._set_status(f"已应用 {n} 项修复")
            self._run_qc()

        threading.Thread(target=work, daemon=True).start()

    def _get_aug_config(self) -> AugConfig:
        cfg = AugConfig()
        for name, var in self._aug_vars.items():
            setattr(cfg, name, var.get())
        return cfg

    def _update_aug_estimate(self) -> None:
        n_imgs = len(list(self.images_dir.glob("*")))
        cfg = self._get_aug_config()
        est = estimate_new_samples(n_imgs, self.mult_var.get(), cfg)
        self.aug_count_label.config(text=f"预计新增样本：{est}")

    def _preview_aug(self) -> None:
        imgs = list(self.images_dir.glob("*"))
        if not imgs:
            messagebox.showinfo("预览", "数据集文件夹中没有图片")
            return
        img = imread_unicode(str(imgs[0]))
        if img is None:
            return
        cfg = self._get_aug_config()
        samples = preview(img, cfg, n=4)
        if samples:
            self._show_preview(samples[0])
            self.preview_label.config(text="数据增强预览（1/4）")

    def _run_aug(self) -> None:
        cfg = self._get_aug_config()
        mult = self.mult_var.get()

        def work():
            self._set_status("正在运行数据增强…")
            n = augment_run(self.images_dir, self.labels_dir, cfg, mult)
            self._set_status(f"已生成 {n} 个新样本")
            safe_ui(self.win, self._update_aug_estimate)
            self._check_insufficient_data()

        threading.Thread(target=work, daemon=True).start()

    def _check_insufficient_data(self) -> None:
        n = len(list(self.images_dir.glob("*")))
        threshold = self.min_img_var.get()
        if n < threshold:
            msg = (
                f"数据集仅有 {n} 张图片（少于 {threshold}）。\n"
                "建议：在「数据增强」选项卡中启用增强，准备就绪后再导出。"
            )
            safe_ui(self.win, lambda: messagebox.showinfo("样本不足", msg))

    def _class_name_lookup(self) -> dict:
        """优先用已加载模型的类别名（含 COCO），否则回退 class_<id>。"""
        names = dict(self.predictor.names or {})
        # ultralytics 可能是 {0: 'person'} 或 {'0': 'person'}
        out = {}
        for k, v in names.items():
            try:
                out[int(k)] = str(v)
            except (TypeError, ValueError):
                continue
        return out

    def _export_dataset(self) -> None:
        imgs = [
            p
            for p in self.images_dir.iterdir()
            if p.is_file() and p.suffix.lower() in IMAGE_EXTS
        ]
        if not imgs:
            messagebox.showwarning("导出", "数据集中没有图片")
            return

        # 扫描全部标签，收集原始 class id，建立连续索引映射
        orig_ids: set[int] = set()
        for lbl in self.labels_dir.glob("*.txt"):
            for line in lbl.read_text(encoding="utf-8").splitlines():
                parts = line.split()
                if not parts:
                    continue
                try:
                    orig_ids.add(int(parts[0]))
                except ValueError:
                    continue
        id_map = {old: new for new, old in enumerate(sorted(orig_ids))}
        name_lookup = self._class_name_lookup()
        names_list = [
            name_lookup.get(old, f"class_{old}") for old in sorted(orig_ids)
        ]

        random.shuffle(imgs)
        split = max(1, int(len(imgs) * 0.2)) if len(imgs) > 1 else 0
        if len(imgs) == 1:
            val_imgs, train_imgs = [], imgs
        else:
            val_imgs = imgs[:split]
            train_imgs = imgs[split:] or imgs[:1]

        export_root = self.ctx.subdir("export")
        for split_name, subset in [("train", train_imgs), ("val", val_imgs)]:
            idir = export_root / "images" / split_name
            ldir = export_root / "labels" / split_name
            idir.mkdir(parents=True, exist_ok=True)
            ldir.mkdir(parents=True, exist_ok=True)
            for img in subset:
                shutil.copy2(img, idir / img.name)
                lbl = self.labels_dir / (img.stem + ".txt")
                if not lbl.exists():
                    continue
                # 重写标签：原始 id → 连续 0..nc-1
                new_lines = []
                for line in lbl.read_text(encoding="utf-8").splitlines():
                    parts = line.strip().split()
                    if len(parts) < 5:
                        continue
                    try:
                        old_id = int(parts[0])
                    except ValueError:
                        continue
                    if old_id not in id_map:
                        continue
                    new_id = id_map[old_id]
                    # 仅保留 YOLO 标准 5 列（忽略审核用的 conf 第 6 列）
                    coords = parts[1:5]
                    if len(coords) < 4:
                        continue
                    new_lines.append(f"{new_id} " + " ".join(coords))
                (ldir / lbl.name).write_text(
                    ("\n".join(new_lines) + "\n") if new_lines else "",
                    encoding="utf-8",
                )

        yaml_path = export_root / "data.yaml"
        yaml_content = (
            f"path: {export_root}\n"
            f"train: images/train\n"
            f"val: images/val\n"
            f"nc: {len(names_list)}\n"
            f"names: {names_list}\n"
        )
        yaml_path.write_text(yaml_content, encoding="utf-8")
        self.ctx.last_dataset_yaml = str(yaml_path)
        messagebox.showinfo("导出", f"数据集已导出到：\n{yaml_path}")
        self._set_status(f"已导出训练集 {len(train_imgs)} 张、验证集 {len(val_imgs)} 张")

    def _send_to_train(self) -> None:
        yaml = self.ctx.last_dataset_yaml
        if not yaml or not Path(yaml).exists():
            self._export_dataset()
            yaml = self.ctx.last_dataset_yaml
        if yaml:
            try:
                self.ctx.open_tool("train", dataset_yaml=yaml)
            except Exception as exc:
                messagebox.showerror("错误", str(exc))
