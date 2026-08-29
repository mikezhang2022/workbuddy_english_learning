"""工具 3 — 模型对比。"""

from __future__ import annotations

import threading
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk
from typing import Optional

import cv2
import numpy as np
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure
from PIL import Image, ImageTk

from ..core.compare import compare, export_report_html
from ..core.context import AppContext
from ..core.io_utils import MediaItem
from ..core.theme import (
    ACCENT,
    ACCENT2,
    BG_CARD,
    BORDER,
    FG,
    center_window,
    configure_theme,
    f,
    get_font_size,
    get_ui_scale,
    setup_matplotlib,
)
from ..core.yolo_engine import DEFAULT_MODEL


def _cv2_to_tk(img: np.ndarray, max_size=(900, 500)) -> ImageTk.PhotoImage:
    rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    pil = Image.fromarray(rgb)
    pil.thumbnail(max_size, Image.Resampling.LANCZOS)
    return ImageTk.PhotoImage(pil)


class CompareTool:
    """在同一媒体上对比两个 YOLO 模型。"""

    def __init__(
        self,
        ctx: AppContext,
        model_a: Optional[str] = None,
        model_b: Optional[str] = None,
    ) -> None:
        self.ctx = ctx
        self.model_a = model_a or ctx.last_model_a or ctx.last_weights
        self.model_b = model_b or ctx.last_model_b
        self.media_path: Optional[str] = None
        self._photo = None
        self._report = None

        setup_matplotlib()
        self.win = tk.Toplevel()
        self.win.title("工具 3 — 模型对比工具")
        configure_theme(self.win, font_size=get_font_size(), ui_scale=get_ui_scale())
        center_window(self.win, 1100, 800)

        self._build_ui()
        self.win.protocol("WM_DELETE_WINDOW", self.win.destroy)

    def _build_ui(self) -> None:
        top = ttk.Frame(self.win, padding=8)
        top.pack(fill=tk.X)

        ttk.Label(top, text="模型 A：").grid(row=0, column=0, sticky=tk.W)
        self.model_a_var = tk.StringVar(value=self.model_a or "")
        ttk.Entry(top, textvariable=self.model_a_var, width=40).grid(row=0, column=1, padx=4)
        ttk.Button(top, text="浏览", command=lambda: self._browse("a")).grid(row=0, column=2)
        ttk.Button(top, text="默认", command=lambda: self._use_default("a")).grid(row=0, column=3, padx=2)

        ttk.Label(top, text="模型 B：").grid(row=1, column=0, sticky=tk.W, pady=4)
        self.model_b_var = tk.StringVar(value=self.model_b or "")
        ttk.Entry(top, textvariable=self.model_b_var, width=40).grid(row=1, column=1, padx=4)
        ttk.Button(top, text="浏览", command=lambda: self._browse("b")).grid(row=1, column=2)
        ttk.Button(top, text="默认", command=lambda: self._use_default("b")).grid(row=1, column=3, padx=2)

        media_row = ttk.Frame(self.win, padding=4)
        media_row.pack(fill=tk.X)
        ttk.Button(media_row, text="选择图片", command=self._select_image).pack(side=tk.LEFT, padx=4)
        ttk.Button(media_row, text="选择视频", command=self._select_video).pack(side=tk.LEFT, padx=4)
        ttk.Button(media_row, text="运行对比", command=self._run_compare, style="Accent.TButton").pack(
            side=tk.LEFT, padx=16
        )

        paned = ttk.PanedWindow(self.win, orient=tk.VERTICAL)
        paned.pack(fill=tk.BOTH, expand=True, padx=8, pady=4)

        preview_frame = ttk.LabelFrame(paned, text="并排预览 + 差异叠加", padding=4)
        paned.add(preview_frame, weight=2)
        self.preview = ttk.Label(preview_frame, text="运行对比后显示叠加图")
        self.preview.pack(fill=tk.BOTH, expand=True)

        bottom = ttk.PanedWindow(paned, orient=tk.HORIZONTAL)
        paned.add(bottom, weight=1)

        analysis_frame = ttk.LabelFrame(bottom, text="分析报告", padding=4)
        bottom.add(analysis_frame, weight=2)
        self.analysis_text = tk.Text(
            analysis_frame, height=12, bg=BG_CARD, fg=FG, wrap=tk.WORD,
            insertbackground=FG, highlightbackground=BORDER, relief=tk.FLAT,
        )
        self.analysis_text.pack(fill=tk.BOTH, expand=True)

        chart_frame = ttk.LabelFrame(bottom, text="置信度分布", padding=4)
        bottom.add(chart_frame, weight=1)
        self.fig = Figure(figsize=(4, 3), dpi=90)
        self.ax = self.fig.add_subplot(111)
        self.canvas = FigureCanvasTkAgg(self.fig, chart_frame)
        self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True)

        export_row = ttk.Frame(self.win, padding=8)
        export_row.pack(fill=tk.X)
        ttk.Button(export_row, text="导出 HTML 报告", command=self._export_html).pack(side=tk.LEFT, padx=4)
        ttk.Button(export_row, text="导出文本报告", command=self._export_text).pack(side=tk.LEFT, padx=4)
        ttk.Button(export_row, text="保存图表 PNG", command=self._export_chart).pack(side=tk.LEFT, padx=4)

        self.status = ttk.Label(self.win, text="就绪", style="Dim.TLabel")
        self.status.pack(fill=tk.X, padx=8, pady=4)

    def _browse(self, which: str) -> None:
        p = filedialog.askopenfilename(filetypes=[("PyTorch", "*.pt"), ("全部", "*.*")])
        if p:
            if which == "a":
                self.model_a_var.set(p)
                self.ctx.last_model_a = p
            else:
                self.model_b_var.set(p)
                self.ctx.last_model_b = p

    def _use_default(self, which: str) -> None:
        if which == "a":
            self.model_a_var.set("")
        else:
            self.model_b_var.set("")

    def _select_image(self) -> None:
        p = filedialog.askopenfilename(filetypes=[("图片", "*.jpg *.png *.jpeg"), ("全部", "*.*")])
        if p:
            self.media_path = p
            self._media_kind = "image"
            self.status.config(text=f"媒体：{Path(p).name}")

    def _select_video(self) -> None:
        p = filedialog.askopenfilename(filetypes=[("视频", "*.mp4 *.avi *.mov"), ("全部", "*.*")])
        if p:
            self.media_path = p
            self._media_kind = "video"
            self.status.config(text=f"媒体：{Path(p).name}")

    def _run_compare(self) -> None:
        if not self.media_path:
            messagebox.showwarning("对比", "请先选择图片或视频")
            return

        model_a = self.model_a_var.get() or None
        model_b = self.model_b_var.get() or None
        media = MediaItem(path=self.media_path, kind=getattr(self, "_media_kind", "image"))

        def work():
            self.status.config(text="正在运行对比…")
            report = compare(
                model_a,
                model_b,
                media,
                device=self.ctx.device_info.device_arg(),
            )
            self._report = report

            def ui():
                self.analysis_text.delete("1.0", tk.END)
                self.analysis_text.insert(tk.END, report.analysis + "\n\n--- 改进建议 ---\n")
                for s in report.suggestions:
                    self.analysis_text.insert(tk.END, f"• {s}\n")
                if report.overlay_image is not None:
                    photo = _cv2_to_tk(report.overlay_image)
                    self._photo = photo
                    self.preview.config(image=photo, text="")
                self._draw_chart(report)
                self.status.config(text=f"已对比 {report.frames_compared} 帧")

            self.win.after(0, ui)

        threading.Thread(target=work, daemon=True).start()

    def _draw_chart(self, report) -> None:
        self.ax.clear()
        st = report.stats
        if st.conf_a:
            self.ax.hist(st.conf_a, bins=15, alpha=0.6, label="模型 A", color=ACCENT)
        if st.conf_b:
            self.ax.hist(st.conf_b, bins=15, alpha=0.6, label="模型 B", color=ACCENT2)
        self.ax.set_xlabel("置信度")
        self.ax.set_ylabel("数量")
        self.ax.legend(fontsize=8)
        self.canvas.draw()

    def _export_html(self) -> None:
        if not self._report:
            messagebox.showinfo("导出", "请先运行对比")
            return
        p = filedialog.asksaveasfilename(defaultextension=".html", filetypes=[("HTML", "*.html")])
        if p:
            chart_p = str(Path(p).with_suffix(".png"))
            self.fig.savefig(chart_p)
            export_report_html(self._report, p, chart_p)
            messagebox.showinfo("导出", f"已保存到 {p}")

    def _export_text(self) -> None:
        if not self._report:
            return
        p = filedialog.asksaveasfilename(defaultextension=".txt", filetypes=[("文本", "*.txt")])
        if p:
            with open(p, "w", encoding="utf-8") as f:
                f.write(self._report.analysis + "\n\n")
                for s in self._report.suggestions:
                    f.write(f"- {s}\n")
            messagebox.showinfo("导出", f"已保存到 {p}")

    def _export_chart(self) -> None:
        if not self._report:
            return
        p = filedialog.asksaveasfilename(defaultextension=".png", filetypes=[("PNG", "*.png")])
        if p:
            self.fig.savefig(p)
            messagebox.showinfo("导出", f"图表已保存到 {p}")
