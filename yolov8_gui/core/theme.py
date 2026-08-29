"""Modern dark ttk theme and helpers."""

from __future__ import annotations

import tkinter as tk
from tkinter import ttk
from typing import Optional, Tuple

# Color palette
BG = "#1e1e2e"
BG_CARD = "#2a2a3d"
BG_INPUT = "#313244"
FG = "#cdd6f4"
FG_DIM = "#a6adc8"
ACCENT = "#89b4fa"
ACCENT2 = "#a6e3a1"
WARN = "#f9e2af"
ERROR = "#f38ba8"
BORDER = "#45475a"
SELECT = "#585b70"
FONT_FAMILY = "Segoe UI"

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


def f(size: int = 10, weight: str = "normal") -> Tuple[str, int, str]:
    return (FONT_FAMILY, size, weight)


def enable_dpi(root: tk.Tk) -> None:
    try:
        from ctypes import windll

        windll.shcore.SetProcessDpiAwareness(1)
    except Exception:
        pass
    try:
        root.tk.call("tk", "scaling", 1.25)
    except Exception:
        pass


def center_window(win: tk.Toplevel | tk.Tk, width: int, height: int) -> None:
    win.update_idletasks()
    sw = win.winfo_screenwidth()
    sh = win.winfo_screenheight()
    x = (sw - width) // 2
    y = (sh - height) // 2
    win.geometry(f"{width}x{height}+{x}+{y}")


def apply_theme(root: tk.Tk | tk.Toplevel) -> ttk.Style:
    root.configure(bg=BG)
    style = ttk.Style(root)
    try:
        style.theme_use("clam")
    except Exception:
        pass

    style.configure(".", background=BG, foreground=FG, fieldbackground=BG_INPUT, bordercolor=BORDER)
    style.configure("TFrame", background=BG)
    style.configure("Card.TFrame", background=BG_CARD, relief="flat")
    style.configure("TLabel", background=BG, foreground=FG, font=f(10))
    style.configure("Dim.TLabel", background=BG, foreground=FG_DIM, font=f(9))
    style.configure("Title.TLabel", background=BG, foreground=FG, font=f(16, "bold"))
    style.configure("CardTitle.TLabel", background=BG_CARD, foreground=FG, font=f(12, "bold"))
    style.configure("Card.TLabel", background=BG_CARD, foreground=FG_DIM, font=f(9))
    style.configure("TButton", background=BG_INPUT, foreground=FG, font=f(10), padding=6)
    style.map("TButton", background=[("active", SELECT), ("pressed", BORDER)])
    style.configure("Accent.TButton", background=ACCENT, foreground=BG, font=f(10, "bold"))
    style.map("Accent.TButton", background=[("active", "#74a8e8")])
    style.configure("TEntry", fieldbackground=BG_INPUT, foreground=FG, insertcolor=FG)
    style.configure("TCombobox", fieldbackground=BG_INPUT, foreground=FG, selectbackground=SELECT)
    style.configure("TSpinbox", fieldbackground=BG_INPUT, foreground=FG)
    style.configure("TCheckbutton", background=BG, foreground=FG)
    style.configure("Card.TCheckbutton", background=BG_CARD, foreground=FG)
    style.configure("TRadiobutton", background=BG, foreground=FG)
    style.configure("TNotebook", background=BG, tabmargins=[2, 5, 2, 0])
    style.configure("TNotebook.Tab", background=BG_CARD, foreground=FG_DIM, padding=[12, 6])
    style.map("TNotebook.Tab", background=[("selected", BG_INPUT)], foreground=[("selected", ACCENT)])
    style.configure("Horizontal.TProgressbar", troughcolor=BG_INPUT, background=ACCENT)
    style.configure("Vertical.TScrollbar", background=BG_INPUT, troughcolor=BG, arrowcolor=FG)
    style.configure("Treeview", background=BG_INPUT, foreground=FG, fieldbackground=BG_INPUT, rowheight=24)
    style.map("Treeview", background=[("selected", SELECT)])
    style.configure("Treeview.Heading", background=BG_CARD, foreground=FG, font=f(10, "bold"))
    style.configure("TLabelframe", background=BG, foreground=FG)
    style.configure("TLabelframe.Label", background=BG, foreground=ACCENT, font=f(10, "bold"))
    style.configure("Card.TLabelframe", background=BG_CARD, foreground=FG)
    style.configure("Card.TLabelframe.Label", background=BG_CARD, foreground=ACCENT)
    return style


def setup_matplotlib() -> None:
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    plt.rcParams.update(
        {
            "figure.facecolor": BG_CARD,
            "axes.facecolor": BG_INPUT,
            "axes.edgecolor": BORDER,
            "axes.labelcolor": FG,
            "text.color": FG,
            "xtick.color": FG_DIM,
            "ytick.color": FG_DIM,
            "grid.color": BORDER,
            "legend.facecolor": BG_CARD,
            "legend.edgecolor": BORDER,
        }
    )


def card(parent: tk.Widget, **kw) -> ttk.Frame:
    return ttk.Frame(parent, style="Card.TFrame", **kw)
