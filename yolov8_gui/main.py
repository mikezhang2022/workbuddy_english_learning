#!/usr/bin/env python3
"""YOLOv8 可视化工作台 — 启动器与入口。"""

from __future__ import annotations

import argparse
import os
import sys
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, ttk

# Ensure package root on path when run as script
_ROOT = Path(__file__).resolve().parent
if str(_ROOT.parent) not in sys.path:
    sys.path.insert(0, str(_ROOT.parent))

from yolov8_gui.core.app_config import load_config
from yolov8_gui.core.context import AppContext
from yolov8_gui.core.device import probe_async
from yolov8_gui.core.settings_dialog import SettingsDialog
from yolov8_gui.core.theme import (
    ACCENT,
    ACCENT2,
    BG_CARD,
    BORDER,
    FG,
    FG_DIM,
    SELECT,
    WARN,
    center_window,
    configure_theme,
    f,
    scaled,
)
from yolov8_gui.core.version import __version__
from yolov8_gui.tools.compare_tool import CompareTool
from yolov8_gui.tools.image_tool import ImageTool
from yolov8_gui.tools.train_tool import TrainTool


class Launcher:
    """主启动窗口：环境检测与工具卡片。"""

    BASE_WIDTH = 960
    BASE_HEIGHT = 680

    def __init__(self, direct_tool: str | None = None) -> None:
        ws = os.environ.get("YOLOV8_WORKSPACE", str(Path.home() / "yolov8_workspace"))
        self.ctx = AppContext(ws)
        self.cfg = load_config(self.ctx.workspace)

        self.root = tk.Tk()
        self.root.title(f"YOLOv8 可视化工作台 v{__version__}")
        configure_theme(self.root, font_size=self.cfg["font_size"], ui_scale=self.cfg["ui_scale"])
        center_window(self.root, self.BASE_WIDTH, self.BASE_HEIGHT)

        self._register_tools()
        self._build_ui()
        probe_async(self._on_probe_done)

        if direct_tool:
            self.root.after(300, lambda: self._open(direct_tool))

        self.root.mainloop()

    def _register_tools(self) -> None:
        self.ctx.register_tool_opener("image", lambda **kw: ImageTool(self.ctx, **kw))
        self.ctx.register_tool_opener("train", lambda **kw: TrainTool(self.ctx, **kw))
        self.ctx.register_tool_opener("compare", lambda **kw: CompareTool(self.ctx, **kw))

    def _build_ui(self) -> None:
        # 先清空再重建，便于设置保存后刷新
        for child in self.root.winfo_children():
            child.destroy()

        header = ttk.Frame(self.root, padding=scaled(16))
        header.pack(fill=tk.X)

        title_row = ttk.Frame(header)
        title_row.pack(fill=tk.X)
        ttk.Label(title_row, text="YOLOv8 可视化工作台", style="Title.TLabel").pack(side=tk.LEFT, anchor=tk.W)
        ttk.Button(title_row, text="⚙ 设置", command=self._open_settings).pack(side=tk.RIGHT)

        ttk.Label(
            header,
            text=f"导入 · 训练 · 对比 — 统一的 YOLOv8 工作流工作区  |  v{__version__}",
            style="Dim.TLabel",
        ).pack(anchor=tk.W)

        ws_frame = ttk.LabelFrame(self.root, text="工作区", padding=scaled(10))
        ws_frame.pack(fill=tk.X, padx=scaled(16), pady=scaled(8))
        self.ws_var = tk.StringVar(value=str(self.ctx.workspace))
        ttk.Entry(ws_frame, textvariable=self.ws_var, width=70).pack(side=tk.LEFT, padx=4)
        ttk.Button(ws_frame, text="浏览", command=self._browse_workspace).pack(side=tk.LEFT)
        ttk.Button(ws_frame, text="应用", command=self._apply_workspace).pack(side=tk.LEFT, padx=4)

        cards = ttk.Frame(self.root, padding=scaled(8))
        cards.pack(fill=tk.BOTH, expand=True, padx=scaled(16))

        tools = [
            ("image", "图片/视频工具", "导入、自动标注、质检、数据增强、导出数据集", ACCENT),
            ("train", "模型训练工具", "自动/手动超参、实时曲线、GPU 训练", ACCENT2),
            ("compare", "模型对比工具", "双模型并排验证与差异分析", WARN),
        ]
        for i, (key, title, desc, color) in enumerate(tools):
            card = tk.Frame(cards, bg=BG_CARD, highlightbackground=BORDER, highlightthickness=1)
            card.grid(row=0, column=i, padx=scaled(8), pady=scaled(8), sticky="nsew")
            cards.columnconfigure(i, weight=1)

            tk.Label(card, text=title, bg=BG_CARD, fg=color, font=f(14, "bold")).pack(
                anchor=tk.W, padx=scaled(12), pady=(scaled(12), 4)
            )
            tk.Label(
                card,
                text=desc,
                bg=BG_CARD,
                fg=FG_DIM,
                font=f(11),
                wraplength=scaled(240),
                justify=tk.LEFT,
            ).pack(anchor=tk.W, padx=scaled(12), pady=4)
            btn = tk.Button(
                card,
                text="打开",
                bg=BG_CARD,
                fg=ACCENT,
                activebackground=SELECT,
                activeforeground=FG,
                relief=tk.FLAT,
                font=f(12, "bold"),
                command=lambda k=key: self._open(k),
            )
            btn.pack(anchor=tk.W, padx=scaled(12), pady=(4, scaled(12)))

        env_frame = ttk.LabelFrame(self.root, text="环境 / 硬件", padding=scaled(10))
        env_frame.pack(fill=tk.BOTH, expand=True, padx=scaled(16), pady=scaled(8))
        self.env_text = tk.Text(
            env_frame,
            height=10,
            bg=BG_CARD,
            fg=FG,
            wrap=tk.WORD,
            font=f(12),
            insertbackground=FG,
            highlightbackground=BORDER,
            relief=tk.FLAT,
            bd=1,
        )
        self.env_text.pack(fill=tk.BOTH, expand=True)
        self.env_text.insert(tk.END, f"软件版本：v{__version__}\n正在检测环境…\n")

        footer = ttk.Label(
            self.root,
            text="提示：python main.py --tool train  |  通过工作区共享状态（data.yaml、权重）  |  右上角「设置」可调节字体",
            style="Dim.TLabel",
        )
        footer.pack(fill=tk.X, padx=scaled(16), pady=scaled(8))

        # 菜单栏：工具 → 设置
        menubar = tk.Menu(self.root)
        tools_menu = tk.Menu(menubar, tearoff=0)
        tools_menu.add_command(label="设置…", command=self._open_settings)
        menubar.add_cascade(label="工具", menu=tools_menu)
        self.root.config(menu=menubar)

    def _open_settings(self) -> None:
        SettingsDialog(
            self.root,
            workspace=str(self.ctx.workspace),
            on_apply=self._on_settings_applied,
        )

    def _on_settings_applied(self, font_size: int, ui_scale: float) -> None:
        self.cfg = {"font_size": font_size, "ui_scale": ui_scale}
        configure_theme(self.root, font_size=font_size, ui_scale=ui_scale)
        center_window(self.root, self.BASE_WIDTH, self.BASE_HEIGHT)
        # 重建主界面以刷新 tk.Label / Text 等非 ttk 控件字体与间距
        probe_info = getattr(self.ctx, "device_info", None)
        self._build_ui()
        if probe_info is not None and getattr(probe_info, "python_version", None):
            self._fill_env(probe_info)

    def _browse_workspace(self) -> None:
        d = filedialog.askdirectory()
        if d:
            self.ws_var.set(d)

    def _apply_workspace(self) -> None:
        self.ctx.set_workspace(self.ws_var.get())
        self.env_text.insert(tk.END, f"\n工作区已设为：{self.ctx.workspace}\n")

    def _fill_env(self, info) -> None:
        self.env_text.delete("1.0", tk.END)
        self.env_text.insert(tk.END, f"软件版本：v{__version__}\n")
        for line in info.summary_lines():
            self.env_text.insert(tk.END, line + "\n")
        if not info.cuda_available:
            self.env_text.insert(
                tk.END,
                "\n⚠ 未检测到 CUDA — 训练/推理将使用 CPU（速度较慢）。\n",
            )

    def _on_probe_done(self, info) -> None:
        self.ctx.device_info = info

        def ui():
            self._fill_env(info)

        self.root.after(0, ui)

    def _open(self, key: str) -> None:
        kwargs = {}
        if key == "train" and self.ctx.last_dataset_yaml:
            kwargs["dataset_yaml"] = self.ctx.last_dataset_yaml
        self.ctx.open_tool(key, **kwargs)


def main() -> None:
    parser = argparse.ArgumentParser(description="YOLOv8 可视化工作台")
    parser.add_argument(
        "--tool",
        choices=["image", "train", "compare"],
        help="直接打开指定工具",
    )
    args = parser.parse_args()
    Launcher(direct_tool=args.tool)


if __name__ == "__main__":
    main()
