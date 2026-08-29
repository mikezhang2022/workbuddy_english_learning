"""设置对话框：字体大小与界面缩放。"""

from __future__ import annotations

import tkinter as tk
from tkinter import messagebox, ttk
from typing import Callable, Optional

from .app_config import DEFAULT_FONT_SIZE, DEFAULT_UI_SCALE, save_config
from .theme import (
    BG_CARD,
    BORDER,
    FG,
    apply_theme,
    center_window,
    configure_theme,
    get_font_size,
    get_ui_scale,
)

_FONT_FAMILY = "Segoe UI"


class SettingsDialog:
    """非模态设置窗口，支持字体/缩放实时预览。"""

    def __init__(
        self,
        master: tk.Tk | tk.Toplevel,
        workspace: Optional[str] = None,
        on_apply: Optional[Callable[[int, float], None]] = None,
    ) -> None:
        self.master = master
        self.workspace = workspace
        self.on_apply = on_apply

        self.win = tk.Toplevel(master)
        self.win.title("设置")
        self.win.transient(master)
        apply_theme(self.win)
        center_window(self.win, 480, 360)
        self.win.minsize(400, 300)

        self.font_var = tk.IntVar(value=get_font_size())
        self.scale_var = tk.DoubleVar(value=round(get_ui_scale() * 20) / 20)

        self._build_ui()
        self._preview()

        self.win.protocol("WM_DELETE_WINDOW", self._on_close)
        self.win.focus_set()

    def _build_ui(self) -> None:
        body = ttk.Frame(self.win, padding=12)
        body.pack(fill=tk.BOTH, expand=True)

        self.title_lbl = ttk.Label(body, text="界面外观", style="Title.TLabel")
        self.title_lbl.pack(anchor=tk.W, pady=(0, 8))
        self.desc_lbl = ttk.Label(
            body,
            text="调节字体大小与整体缩放，拖动时可实时预览效果。",
            style="Dim.TLabel",
        )
        self.desc_lbl.pack(anchor=tk.W, pady=(0, 12))

        font_frame = ttk.LabelFrame(body, text="界面字体大小", padding=10)
        font_frame.pack(fill=tk.X, pady=6)
        self.font_frame = font_frame
        row1 = ttk.Frame(font_frame)
        row1.pack(fill=tk.X)
        ttk.Label(row1, text="小").pack(side=tk.LEFT)
        self.font_value_lbl = ttk.Label(font_frame, text=f"{self.font_var.get()} pt")
        self.font_scale = ttk.Scale(
            row1,
            from_=10,
            to=20,
            orient=tk.HORIZONTAL,
            command=self._on_font_slide,
        )
        self.font_scale.pack(side=tk.LEFT, fill=tk.X, expand=True, padx=8)
        ttk.Label(row1, text="大").pack(side=tk.LEFT)
        self.font_value_lbl.pack(anchor=tk.E, pady=(4, 0))
        self.font_scale.set(self.font_var.get())

        scale_frame = ttk.LabelFrame(body, text="界面缩放 (DPI)", padding=10)
        scale_frame.pack(fill=tk.X, pady=6)
        self.scale_frame = scale_frame
        row2 = ttk.Frame(scale_frame)
        row2.pack(fill=tk.X)
        ttk.Label(row2, text="0.8").pack(side=tk.LEFT)
        self.scale_value_lbl = ttk.Label(scale_frame, text=f"{self.scale_var.get():.2f}×")
        self.ui_scale = ttk.Scale(
            row2,
            from_=0.8,
            to=1.5,
            orient=tk.HORIZONTAL,
            command=self._on_scale_slide,
        )
        self.ui_scale.pack(side=tk.LEFT, fill=tk.X, expand=True, padx=8)
        ttk.Label(row2, text="1.5").pack(side=tk.LEFT)
        self.scale_value_lbl.pack(anchor=tk.E, pady=(4, 0))
        self.ui_scale.set(self.scale_var.get())

        self.preview_label = tk.Label(
            body,
            text="预览文字：YOLOv8 可视化工作台 — 导入 · 训练 · 对比",
            bg=BG_CARD,
            fg=FG,
            font=(_FONT_FAMILY, self.font_var.get(), "normal"),
            highlightbackground=BORDER,
            highlightthickness=1,
            padx=12,
            pady=16,
            wraplength=400,
            justify=tk.LEFT,
        )
        self.preview_label.pack(fill=tk.X, pady=12)

        self.hint_label = ttk.Label(
            body,
            text="默认字体 12、缩放 1.0。保存后主窗口立即刷新；已打开的工具窗口请关闭后重新打开。",
            style="Dim.TLabel",
            wraplength=420,
        )
        self.hint_label.pack(anchor=tk.W, pady=(0, 8))

        btns = ttk.Frame(body)
        btns.pack(fill=tk.X, pady=(8, 0))
        self.btn_reset = ttk.Button(btns, text="恢复默认", command=self._reset)
        self.btn_reset.pack(side=tk.LEFT)
        self.btn_cancel = ttk.Button(btns, text="取消", command=self._on_close)
        self.btn_cancel.pack(side=tk.RIGHT)
        self.btn_save = ttk.Button(btns, text="保存并应用", style="Accent.TButton", command=self._save)
        self.btn_save.pack(side=tk.RIGHT, padx=8)

    def _snap_font(self) -> int:
        return int(round(float(self.font_scale.get())))

    def _snap_scale(self) -> float:
        return round(float(self.ui_scale.get()) * 20) / 20.0

    def _on_font_slide(self, _value: str | None = None) -> None:
        v = self._snap_font()
        self.font_var.set(v)
        lbl = getattr(self, "font_value_lbl", None)
        if lbl is not None:
            lbl.configure(text=f"{v} pt")
        if getattr(self, "preview_label", None) is not None:
            self._preview()

    def _on_scale_slide(self, _value: str | None = None) -> None:
        v = self._snap_scale()
        self.scale_var.set(v)
        lbl = getattr(self, "scale_value_lbl", None)
        if lbl is not None:
            lbl.configure(text=f"{v:.2f}×")
        if getattr(self, "preview_label", None) is not None:
            self._preview()

    def _preview(self) -> None:
        """拖动时立即改变本设置窗口的字体，便于预览。"""
        if getattr(self, "preview_label", None) is None:
            return
        fs = self._snap_font()
        sc = self._snap_scale()
        base = (_FONT_FAMILY, fs, "normal")
        bold = (_FONT_FAMILY, fs, "bold")
        title = (_FONT_FAMILY, max(14, int(round(fs * 18 / 12))), "bold")
        dim = (_FONT_FAMILY, max(9, fs - 1), "normal")

        self.preview_label.configure(font=base)
        if getattr(self, "font_value_lbl", None) is not None:
            self.font_value_lbl.configure(font=base)
        if getattr(self, "scale_value_lbl", None) is not None:
            self.scale_value_lbl.configure(font=base)
        self.title_lbl.configure(font=title)
        self.desc_lbl.configure(font=dim)
        self.hint_label.configure(font=dim)

        style = ttk.Style(self.win)
        pad = max(4, int(round(8 * sc)))
        style.configure("TLabel", font=base)
        style.configure("Dim.TLabel", font=dim)
        style.configure("Title.TLabel", font=title)
        style.configure("TButton", font=base, padding=pad)
        style.configure("Accent.TButton", font=bold, padding=pad)
        style.configure("TLabelframe.Label", font=bold)

    def _reset(self) -> None:
        self.font_scale.set(DEFAULT_FONT_SIZE)
        self.ui_scale.set(DEFAULT_UI_SCALE)
        self._on_font_slide()
        self._on_scale_slide()

    def _restore_global_styles(self) -> None:
        """取消/关闭时恢复已保存的全局主题样式（ttk.Style 全局共享）。"""
        configure_theme(self.master, font_size=get_font_size(), ui_scale=get_ui_scale())

    def _on_close(self) -> None:
        self._restore_global_styles()
        self.win.destroy()

    def _save(self) -> None:
        fs = self._snap_font()
        sc = self._snap_scale()
        save_config(fs, sc, workspace=self.workspace)
        configure_theme(self.master, font_size=fs, ui_scale=sc)
        if self.on_apply:
            self.on_apply(fs, sc)
        messagebox.showinfo(
            "设置",
            "已保存并应用到主窗口。\n已打开的工具窗口请关闭后重新打开，以完全应用新字体与缩放。",
            parent=self.win,
        )
        self.win.destroy()
