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

from ..core.augment import AugConfig, estimate_new_samples, preview, run as augment_run
from ..core.context import AppContext
from ..core.dataset_qc import apply_fixes, check as dataset_check
from ..core.io_utils import MediaItem, extract_frames, imread_unicode, list_media
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
)
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


def _cv2_to_tk(img: np.ndarray, max_size: tuple = (640, 480)) -> ImageTk.PhotoImage:
    rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    pil = Image.fromarray(rgb)
    pil.thumbnail(max_size, Image.Resampling.LANCZOS)
    return ImageTk.PhotoImage(pil)


class ImageTool:
    """YOLOv8 图片/视频导入、标注、质检、增强、导出。"""

    MIN_IMAGES = 50
    MIN_PER_CLASS = 5

    def __init__(self, ctx: AppContext, model_path: Optional[str] = None) -> None:
        self.ctx = ctx
        self.model_path = model_path
        self.items: List[MediaItem] = []
        self._photos: list = []
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

        ttk.Label(top, text="模型 (.pt)：", font=f(10)).pack(side=tk.LEFT)
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

        paned = ttk.PanedWindow(self.win, orient=tk.HORIZONTAL)
        paned.pack(fill=tk.BOTH, expand=True, padx=8, pady=4)

        left = ttk.Frame(paned)
        paned.add(left, weight=1)
        ttk.Label(left, text="媒体列表", font=f(10, "bold")).pack(anchor=tk.W)
        self.listbox = tk.Listbox(
            left, bg=BG_CARD, fg=FG, selectbackground=SELECT, selectforeground=FG,
            highlightbackground=BORDER, relief=tk.FLAT, height=20,
        )
        self.listbox.pack(fill=tk.BOTH, expand=True)
        self.listbox.bind("<<ListboxSelect>>", self._on_select)

        center = ttk.Frame(paned)
        paned.add(center, weight=2)
        self.preview_label = ttk.Label(center, text="请选择媒体以预览")
        self.preview_label.pack(fill=tk.BOTH, expand=True)

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

        self.status = ttk.Label(self.win, text="就绪", style="Dim.TLabel")
        self.status.pack(fill=tk.X, padx=8, pady=4)
        self._update_model_status()

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
        self.status.config(text=msg)
        self.win.update_idletasks()

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
                photo = _cv2_to_tk(img)
                self._photos = [photo]
                self.preview_label.config(image=photo, text="")
        else:
            cap = cv2.VideoCapture(item.path)
            ret, frame = cap.read()
            cap.release()
            if ret:
                photo = _cv2_to_tk(frame)
                self._photos = [photo]
                self.preview_label.config(image=photo, text="")

    def _auto_annotate(self) -> None:
        def work():
            self._set_status("正在加载模型…")
            self.predictor.load(self.model_path or None)
            self.win.after(0, lambda: self._update_model_status())
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
            self.predictor.load(self.model_path or None)
            ok = annotate_video(item.path, out, self.predictor)
            self._set_status("视频已导出" if ok else "导出失败")

        if out:
            threading.Thread(target=work, daemon=True).start()

    def _run_qc(self) -> None:
        def work():
            self._set_status("正在运行质检…")
            report = dataset_check(self.images_dir, self.labels_dir)
            text = report.summary()
            self.qc_text.delete("1.0", tk.END)
            self.qc_text.insert(tk.END, text)
            self._last_report = report
            self._set_status("质检完成")

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
            photo = _cv2_to_tk(samples[0])
            self._photos = [photo]
            self.preview_label.config(image=photo, text="数据增强预览（1/4）")

    def _run_aug(self) -> None:
        cfg = self._get_aug_config()
        mult = self.mult_var.get()

        def work():
            self._set_status("正在运行数据增强…")
            n = augment_run(self.images_dir, self.labels_dir, cfg, mult)
            self._set_status(f"已生成 {n} 个新样本")
            self._update_aug_estimate()
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
            self.win.after(0, lambda: messagebox.showinfo("样本不足", msg))

    def _export_dataset(self) -> None:
        imgs = [p for p in self.images_dir.iterdir() if p.is_file()]
        if not imgs:
            messagebox.showwarning("导出", "数据集中没有图片")
            return
        random.shuffle(imgs)
        split = max(1, int(len(imgs) * 0.2))
        val_imgs = imgs[:split]
        train_imgs = imgs[split:]

        export_root = self.ctx.subdir("export")
        for split_name, subset in [("train", train_imgs), ("val", val_imgs)]:
            idir = export_root / "images" / split_name
            ldir = export_root / "labels" / split_name
            idir.mkdir(parents=True, exist_ok=True)
            ldir.mkdir(parents=True, exist_ok=True)
            for img in subset:
                shutil.copy2(img, idir / img.name)
                lbl = self.labels_dir / (img.stem + ".txt")
                if lbl.exists():
                    shutil.copy2(lbl, ldir / lbl.name)

        classes: dict = {}
        for lbl in self.labels_dir.glob("*.txt"):
            for line in lbl.read_text(encoding="utf-8").splitlines():
                parts = line.split()
                if parts:
                    cid = int(parts[0])
                    classes.setdefault(cid, f"class_{cid}")

        yaml_path = export_root / "data.yaml"
        names_list = [classes[i] for i in sorted(classes)]
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
