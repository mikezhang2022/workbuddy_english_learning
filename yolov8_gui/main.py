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

from yolov8_gui.core.context import AppContext
from yolov8_gui.core.device import probe, probe_async
from yolov8_gui.core.theme import ACCENT, ACCENT2, BG, BG_CARD, BORDER, FG, FG_DIM, SELECT, WARN, apply_theme, center_window, enable_dpi, f
from yolov8_gui.tools.compare_tool import CompareTool
from yolov8_gui.tools.image_tool import ImageTool
from yolov8_gui.tools.train_tool import TrainTool


class Launcher:
    """主启动窗口：环境检测与工具卡片。"""

    def __init__(self, direct_tool: str | None = None) -> None:
        self.root = tk.Tk()
        self.root.title("YOLOv8 可视化工作台")
        enable_dpi(self.root)
        apply_theme(self.root)
        center_window(self.root, 960, 680)

        ws = os.environ.get("YOLOV8_WORKSPACE", str(Path.home() / "yolov8_workspace"))
        self.ctx = AppContext(ws)
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
        header = ttk.Frame(self.root, padding=16)
        header.pack(fill=tk.X)
        ttk.Label(header, text="YOLOv8 可视化工作台", style="Title.TLabel").pack(anchor=tk.W)
        ttk.Label(
            header,
            text="导入 · 训练 · 对比 — 统一的 YOLOv8 工作流工作区",
            style="Dim.TLabel",
        ).pack(anchor=tk.W)

        ws_frame = ttk.LabelFrame(self.root, text="工作区", padding=10)
        ws_frame.pack(fill=tk.X, padx=16, pady=8)
        self.ws_var = tk.StringVar(value=str(self.ctx.workspace))
        ttk.Entry(ws_frame, textvariable=self.ws_var, width=70).pack(side=tk.LEFT, padx=4)
        ttk.Button(ws_frame, text="浏览", command=self._browse_workspace).pack(side=tk.LEFT)
        ttk.Button(ws_frame, text="应用", command=self._apply_workspace).pack(side=tk.LEFT, padx=4)

        cards = ttk.Frame(self.root, padding=8)
        cards.pack(fill=tk.BOTH, expand=True, padx=16)

        tools = [
            ("image", "图片/视频工具", "导入、自动标注、质检、数据增强、导出数据集", ACCENT),
            ("train", "模型训练工具", "自动/手动超参、实时曲线、GPU 训练", ACCENT2),
            ("compare", "模型对比工具", "双模型并排验证与差异分析", WARN),
        ]
        for i, (key, title, desc, color) in enumerate(tools):
            card = tk.Frame(cards, bg=BG_CARD, highlightbackground=BORDER, highlightthickness=1)
            card.grid(row=0, column=i, padx=8, pady=8, sticky="nsew")
            cards.columnconfigure(i, weight=1)

            tk.Label(card, text=title, bg=BG_CARD, fg=color, font=f(13, "bold")).pack(anchor=tk.W, padx=12, pady=(12, 4))
            tk.Label(card, text=desc, bg=BG_CARD, fg=FG_DIM, font=f(9), wraplength=240, justify=tk.LEFT).pack(
                anchor=tk.W, padx=12, pady=4
            )
            btn = tk.Button(
                card,
                text="打开",
                bg=BG_CARD,
                fg=ACCENT,
                activebackground=SELECT,
                activeforeground=FG,
                relief=tk.FLAT,
                font=f(10, "bold"),
                command=lambda k=key: self._open(k),
            )
            btn.pack(anchor=tk.W, padx=12, pady=(4, 12))

        env_frame = ttk.LabelFrame(self.root, text="环境 / 硬件", padding=10)
        env_frame.pack(fill=tk.BOTH, expand=True, padx=16, pady=8)
        self.env_text = tk.Text(env_frame, height=10, bg=BG_CARD, fg=FG, wrap=tk.WORD, font=f(9),
                                insertbackground=FG, highlightbackground=BORDER, relief=tk.FLAT, bd=1)
        self.env_text.pack(fill=tk.BOTH, expand=True)
        self.env_text.insert(tk.END, "正在检测环境…\n")

        footer = ttk.Label(
            self.root,
            text="提示：python main.py --tool train  |  通过工作区共享状态（data.yaml、权重）",
            style="Dim.TLabel",
        )
        footer.pack(fill=tk.X, padx=16, pady=8)

    def _browse_workspace(self) -> None:
        d = filedialog.askdirectory()
        if d:
            self.ws_var.set(d)

    def _apply_workspace(self) -> None:
        self.ctx.set_workspace(self.ws_var.get())
        self.env_text.insert(tk.END, f"\n工作区已设为：{self.ctx.workspace}\n")

    def _on_probe_done(self, info) -> None:
        self.ctx.device_info = info

        def ui():
            self.env_text.delete("1.0", tk.END)
            for line in info.summary_lines():
                self.env_text.insert(tk.END, line + "\n")
            if not info.cuda_available:
                self.env_text.insert(
                    tk.END,
                    "\n⚠ 未检测到 CUDA — 训练/推理将使用 CPU（速度较慢）。\n",
                )

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
