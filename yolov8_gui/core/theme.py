"""浅色 ttk 主题与辅助函数（支持字体大小 / UI 缩放）。"""

from __future__ import annotations

import tkinter as tk
from tkinter import ttk
from typing import Optional, Tuple

# 浅色色板
BG = "#f5f7fa"
BG_CARD = "#ffffff"
BG_INPUT = "#ffffff"
FG = "#1f2937"
FG_DIM = "#6b7280"
ACCENT = "#2563eb"
ACCENT2 = "#059669"
WARN = "#d97706"
ERROR = "#dc2626"
BORDER = "#d1d5db"
SELECT = "#e5e7eb"
FONT_FAMILY = "Segoe UI"

# 设计稿基准字号（相对尺寸换算用）
_DESIGN_BASE = 12

# 当前可配置主题状态
_FONT_SIZE: int = 12
_UI_SCALE: float = 1.0

COLORS = {
    "bg": BG,
    "card": BG_CARD,
    "input": BG_INPUT,
    "fg": FG,
    "fg_dim": FG_DIM,
    "accent": ACCENT,
    "accent2": ACCENT2,
    "warn": WARN,
    "error": ERROR,
    "border": BORDER,
    "select": SELECT,
}


def get_font_size() -> int:
    return _FONT_SIZE


def get_ui_scale() -> float:
    return _UI_SCALE


def scaled(value: float) -> int:
    """按当前 UI 缩放换算像素/间距。"""
    return max(1, int(round(value * _UI_SCALE)))


def f(size: int | None = None, weight: str = "normal") -> Tuple[str, int, str]:
    """返回字体元组。size 相对设计基准 12，随当前 font_size 等比缩放。"""
    base = _FONT_SIZE
    if size is None:
        sz = base
    else:
        sz = max(8, int(round(size * base / float(_DESIGN_BASE))))
    return (FONT_FAMILY, sz, weight)


def enable_dpi(root: tk.Misc, ui_scale: float | None = None) -> None:
    scale = _UI_SCALE if ui_scale is None else float(ui_scale)
    try:
        from ctypes import windll

        windll.shcore.SetProcessDpiAwareness(1)
    except Exception:
        pass
    try:
        root.tk.call("tk", "scaling", 1.25 * scale)
    except Exception:
        pass


def center_window(win: tk.Toplevel | tk.Tk, width: int, height: int) -> None:
    w = scaled(width)
    h = scaled(height)
    win.update_idletasks()
    sw = win.winfo_screenwidth()
    sh = win.winfo_screenheight()
    x = (sw - w) // 2
    y = (sh - h) // 2
    win.geometry(f"{w}x{h}+{x}+{y}")


def apply_theme(root: tk.Tk | tk.Toplevel) -> ttk.Style:
    """按当前 font_size / ui_scale 应用 ttk 样式。"""
    root.configure(bg=BG)
    style = ttk.Style(root)
    try:
        style.theme_use("clam")
    except Exception:
        pass

    pad = scaled(8)
    tab_pad_x = scaled(14)
    tab_pad_y = scaled(8)
    row_h = scaled(30)

    style.configure(".", background=BG, foreground=FG, fieldbackground=BG_INPUT, bordercolor=BORDER, font=f(12))
    style.configure("TFrame", background=BG)
    style.configure("Card.TFrame", background=BG_CARD, relief="flat")
    style.configure("TLabel", background=BG, foreground=FG, font=f(12))
    style.configure("Dim.TLabel", background=BG, foreground=FG_DIM, font=f(11))
    style.configure("Title.TLabel", background=BG, foreground=FG, font=f(18, "bold"))
    style.configure("CardTitle.TLabel", background=BG_CARD, foreground=FG, font=f(14, "bold"))
    style.configure("Card.TLabel", background=BG_CARD, foreground=FG_DIM, font=f(11))
    style.configure("TButton", background=BG_INPUT, foreground=FG, font=f(12), padding=pad)
    style.map("TButton", background=[("active", SELECT), ("pressed", BORDER)])
    style.configure("Accent.TButton", background=ACCENT, foreground="#ffffff", font=f(12, "bold"), padding=pad)
    style.map("Accent.TButton", background=[("active", "#1d4ed8")])
    style.configure("TEntry", fieldbackground=BG_INPUT, foreground=FG, insertcolor=FG, font=f(12))
    style.map("TEntry", fieldbackground=[("readonly", BG_INPUT)], foreground=[("readonly", FG)])
    style.configure("TCombobox", fieldbackground=BG_INPUT, foreground=FG, selectbackground=SELECT, font=f(12))
    style.map(
        "TCombobox",
        fieldbackground=[("readonly", BG_INPUT)],
        foreground=[("readonly", FG)],
        selectbackground=[("readonly", SELECT)],
    )
    style.configure("TSpinbox", fieldbackground=BG_INPUT, foreground=FG, insertcolor=FG, font=f(12))
    style.configure("TCheckbutton", background=BG, foreground=FG, font=f(12))
    style.map("TCheckbutton", background=[("active", BG)])
    style.configure("Card.TCheckbutton", background=BG_CARD, foreground=FG, font=f(12))
    style.configure("TRadiobutton", background=BG, foreground=FG, font=f(12))
    style.map("TRadiobutton", background=[("active", BG)])
    style.configure("TNotebook", background=BG, tabmargins=[scaled(2), scaled(5), scaled(2), 0])
    style.configure(
        "TNotebook.Tab",
        background=BG_CARD,
        foreground=FG_DIM,
        padding=[tab_pad_x, tab_pad_y],
        font=f(12),
    )
    style.map(
        "TNotebook.Tab",
        background=[("selected", SELECT)],
        foreground=[("selected", ACCENT)],
    )
    style.configure("Horizontal.TProgressbar", troughcolor=SELECT, background=ACCENT)
    style.configure("Vertical.TScrollbar", background=SELECT, troughcolor=BG, arrowcolor=FG)
    style.configure(
        "Treeview",
        background=BG_INPUT,
        foreground=FG,
        fieldbackground=BG_INPUT,
        rowheight=row_h,
        font=f(12),
    )
    style.map("Treeview", background=[("selected", SELECT)], foreground=[("selected", FG)])
    style.configure("Treeview.Heading", background=BG_CARD, foreground=FG, font=f(12, "bold"))
    style.configure("TLabelframe", background=BG, foreground=FG)
    style.configure("TLabelframe.Label", background=BG, foreground=ACCENT, font=f(12, "bold"))
    style.configure("Card.TLabelframe", background=BG_CARD, foreground=FG)
    style.configure("Card.TLabelframe.Label", background=BG_CARD, foreground=ACCENT, font=f(12, "bold"))
    style.configure("TScale", background=BG)
    return style


def configure_theme(
    root: tk.Tk | tk.Toplevel | None = None,
    font_size: int = 12,
    ui_scale: float = 1.0,
) -> Optional[ttk.Style]:
    """设置全局字体大小与 UI 缩放，并可选立即应用到 root。"""
    global _FONT_SIZE, _UI_SCALE
    _FONT_SIZE = max(8, min(24, int(font_size)))
    _UI_SCALE = max(0.8, min(1.5, float(ui_scale)))
    if root is not None:
        enable_dpi(root, _UI_SCALE)
        return apply_theme(root)
    return None


def setup_matplotlib() -> None:
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    plt.rcParams.update(
        {
            "figure.facecolor": "#ffffff",
            "axes.facecolor": "#ffffff",
            "axes.edgecolor": BORDER,
            "axes.labelcolor": FG,
            "text.color": FG,
            "xtick.color": FG_DIM,
            "ytick.color": FG_DIM,
            "grid.color": BORDER,
            "legend.facecolor": "#ffffff",
            "legend.edgecolor": BORDER,
        }
    )


def card(parent: tk.Widget, **kw) -> ttk.Frame:
    return ttk.Frame(parent, style="Card.TFrame", **kw)
