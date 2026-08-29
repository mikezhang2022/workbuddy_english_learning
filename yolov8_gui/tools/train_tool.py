"""工具 2 — 模型训练。"""

from __future__ import annotations

import os
import threading
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk
from typing import Optional

from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure

from ..core.context import AppContext
from ..core.theme import (
    ACCENT,
    ACCENT2,
    BG_CARD,
    BORDER,
    ERROR,
    FG,
    SELECT,
    WARN,
    center_window,
    configure_theme,
    f,
    get_font_size,
    get_ui_scale,
    setup_matplotlib,
)
from ..core.train_engine import AdviceItem, default_params, validate_params
from ..core.train_runner import TrainHistory, TrainRunner

# 参数键 → 中文标签
PARAM_LABELS = {
    "epochs": "轮数",
    "lr0": "学习率",
    "batch": "批次大小",
    "imgsz": "输入尺寸",
    "optimizer": "优化器",
    "momentum": "动量",
    "weight_decay": "权重衰减",
    "warmup_epochs": "预热轮数",
    "patience": "早停耐心值",
    "model": "模型",
}


class TrainTool:
    """YOLOv8 训练：自动/手动模式与实时曲线。"""

    PARAM_KEYS = [
        "epochs", "lr0", "batch", "imgsz", "optimizer", "momentum",
        "weight_decay", "warmup_epochs", "patience", "model",
    ]

    def __init__(self, ctx: AppContext, dataset_yaml: str = "") -> None:
        self.ctx = ctx
        self.dataset_yaml = dataset_yaml or ctx.last_dataset_yaml or ""
        self.runner: Optional[TrainRunner] = None
        self.mode = tk.StringVar(value="auto")
        self.param_vars: dict = {}
        self.rationale: dict = {}

        setup_matplotlib()
        self.win = tk.Toplevel()
        self.win.title("工具 2 — 模型训练工具")
        configure_theme(self.win, font_size=get_font_size(), ui_scale=get_ui_scale())
        center_window(self.win, 1100, 850)

        self._build_ui()
        self._load_defaults()
        self.win.protocol("WM_DELETE_WINDOW", self.win.destroy)

    def _build_ui(self) -> None:
        top = ttk.Frame(self.win, padding=8)
        top.pack(fill=tk.X)

        ttk.Label(top, text="数据集 (data.yaml)：").pack(side=tk.LEFT)
        self.dataset_var = tk.StringVar(value=self.dataset_yaml)
        ttk.Entry(top, textvariable=self.dataset_var, width=50).pack(side=tk.LEFT, padx=4)
        ttk.Button(top, text="浏览", command=self._browse_dataset).pack(side=tk.LEFT)
        ttk.Button(top, text="发送到图片/视频工具 →", command=self._send_to_image).pack(side=tk.LEFT, padx=8)

        mode_frame = ttk.Frame(self.win, padding=4)
        mode_frame.pack(fill=tk.X)
        ttk.Radiobutton(mode_frame, text="自动（推荐）", variable=self.mode, value="auto", command=self._on_mode).pack(side=tk.LEFT, padx=8)
        ttk.Radiobutton(mode_frame, text="手动", variable=self.mode, value="manual", command=self._on_mode).pack(side=tk.LEFT)

        paned = ttk.PanedWindow(self.win, orient=tk.HORIZONTAL)
        paned.pack(fill=tk.BOTH, expand=True, padx=8, pady=4)

        params_frame = ttk.LabelFrame(paned, text="参数", padding=8)
        paned.add(params_frame, weight=1)

        self.params_grid = ttk.Frame(params_frame)
        self.params_grid.pack(fill=tk.BOTH, expand=True)
        for key in self.PARAM_KEYS:
            row = ttk.Frame(self.params_grid)
            row.pack(fill=tk.X, pady=2)
            label = PARAM_LABELS.get(key, key)
            ttk.Label(row, text=label, width=14).pack(side=tk.LEFT)
            var = tk.StringVar()
            self.param_vars[key] = var
            entry = ttk.Entry(row, textvariable=var, width=12)
            entry.pack(side=tk.LEFT)
            entry.bind("<KeyRelease>", lambda _e: self._refresh_advice())
            ttk.Label(row, text="", width=40, style="Dim.TLabel").pack(side=tk.LEFT, padx=4)

        self.rationale_label = ttk.Label(params_frame, text="", wraplength=350, style="Dim.TLabel")
        self.rationale_label.pack(fill=tk.X, pady=8)

        right = ttk.Frame(paned)
        paned.add(right, weight=2)

        advice_frame = ttk.LabelFrame(right, text="训练建议", padding=8)
        advice_frame.pack(fill=tk.X)
        self.advice_list = tk.Listbox(
            advice_frame, height=5, bg=BG_CARD, fg=FG, selectbackground=SELECT,
            selectforeground=FG, highlightbackground=BORDER, relief=tk.FLAT,
        )
        self.advice_list.pack(fill=tk.X)
        self._advice_items: list = []
        ttk.Button(advice_frame, text="应用所选修复", command=self._apply_fix).pack(pady=4)

        log_frame = ttk.LabelFrame(right, text="训练日志", padding=4)
        log_frame.pack(fill=tk.BOTH, expand=True, pady=4)
        self.log_text = tk.Text(
            log_frame, height=10, bg=BG_CARD, fg=FG, wrap=tk.WORD,
            insertbackground=FG, highlightbackground=BORDER, relief=tk.FLAT,
        )
        self.log_text.pack(fill=tk.BOTH, expand=True, side=tk.LEFT)
        sb = ttk.Scrollbar(log_frame, command=self.log_text.yview)
        sb.pack(side=tk.RIGHT, fill=tk.Y)
        self.log_text.config(yscrollcommand=sb.set)

        chart_frame = ttk.LabelFrame(right, text="实时指标", padding=4)
        chart_frame.pack(fill=tk.BOTH, expand=True)
        self.fig = Figure(figsize=(5, 3), dpi=100)
        self.ax = self.fig.add_subplot(111)
        self.canvas = FigureCanvasTkAgg(self.fig, chart_frame)
        self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True)

        btn_row = ttk.Frame(self.win, padding=8)
        btn_row.pack(fill=tk.X)
        ttk.Button(btn_row, text="开始训练", command=self._start, style="Accent.TButton").pack(side=tk.LEFT, padx=4)
        ttk.Button(btn_row, text="停止", command=self._stop).pack(side=tk.LEFT, padx=4)
        self.result_label = ttk.Label(btn_row, text="", style="Dim.TLabel")
        self.result_label.pack(side=tk.LEFT, padx=16)

        device_txt = f"设备：{self.ctx.device_info.device_arg()}"
        if self.ctx.device_info.cuda_available:
            device_txt += f"（{self.ctx.device_info.gpu_name}）"
        else:
            device_txt += "（CPU — 无 CUDA）"
        ttk.Label(self.win, text=device_txt, style="Dim.TLabel").pack(fill=tk.X, padx=8)

    def _browse_dataset(self) -> None:
        p = filedialog.askopenfilename(filetypes=[("YAML", "*.yaml *.yml"), ("全部", "*.*")])
        if p:
            self.dataset_var.set(p)
            self._load_defaults()
            self._refresh_advice()

    def _load_defaults(self) -> None:
        ds = self.dataset_var.get()
        params, rationale = default_params(ds if ds else None)
        self.rationale = rationale
        for k, v in params.items():
            if k in self.param_vars:
                self.param_vars[k].set(str(v))
        self._update_rationale_text()
        self._refresh_advice()

    def _on_mode(self) -> None:
        if self.mode.get() == "auto":
            self._load_defaults()
            for key, var in self.param_vars.items():
                for w in self.params_grid.winfo_children():
                    pass
        self._update_rationale_text()

    def _get_params(self) -> dict:
        params = {}
        for k, var in self.param_vars.items():
            val = var.get()
            try:
                if k in ("epochs", "batch", "imgsz", "patience"):
                    params[k] = int(float(val))
                elif k in ("lr0", "momentum", "weight_decay", "warmup_epochs"):
                    params[k] = float(val)
                else:
                    params[k] = val
            except ValueError:
                params[k] = val
        return params

    def _update_rationale_text(self) -> None:
        if self.mode.get() != "auto":
            self.rationale_label.config(text="手动模式 — 请按需调整参数。")
            return
        lines = [f"{k}: {v}" for k, v in list(self.rationale.items())[:5]]
        self.rationale_label.config(text="\n".join(lines))

    def _refresh_advice(self) -> None:
        params = self._get_params()
        items = validate_params(params, self.dataset_var.get(), self.ctx.device_info)
        self._advice_items = items
        self.advice_list.delete(0, tk.END)
        for it in items:
            prefix = {"error": "⛔", "warn": "⚠", "info": "ℹ"}.get(it.severity, "")
            self.advice_list.insert(tk.END, f"{prefix} {it.message} → [{it.fix_label}]")

    def _apply_fix(self) -> None:
        sel = self.advice_list.curselection()
        if not sel:
            return
        item: AdviceItem = self._advice_items[sel[0]]
        if item.fix_key == "augment" or item.fix_key == "val_split":
            self._send_to_image()
            return
        if item.fix_key in self.param_vars:
            self.param_vars[item.fix_key].set(str(item.fix_value))
            self._refresh_advice()

    def _send_to_image(self) -> None:
        try:
            self.ctx.open_tool("image")
        except Exception as exc:
            messagebox.showerror("错误", str(exc))

    def _append_log(self, text: str) -> None:
        def ui():
            self.log_text.insert(tk.END, text)
            self.log_text.see(tk.END)

        self.win.after(0, ui)

    def _update_chart(self, history: TrainHistory) -> None:
        def ui():
            self.ax.clear()
            if history.epoch:
                self.ax.plot(history.epoch, history.train_loss, label="训练损失", color=ACCENT)
                if history.val_loss:
                    self.ax.plot(history.epoch[: len(history.val_loss)], history.val_loss, label="验证损失", color=ERROR)
                if history.map50:
                    self.ax.plot(history.epoch[: len(history.map50)], history.map50, label="mAP50", color=ACCENT2)
            self.ax.legend(loc="upper right", fontsize=8)
            self.ax.set_xlabel("轮数")
            self.canvas.draw()

        self.win.after(0, ui)

    def _start(self) -> None:
        ds = self.dataset_var.get()
        if not ds or not os.path.isfile(ds):
            messagebox.showerror("训练", "请选择有效的 data.yaml")
            return
        if self.runner and self.runner.is_running():
            messagebox.showwarning("训练", "训练已在进行中")
            return

        params = self._get_params()
        project = str(self.ctx.subdir("runs"))
        name = "train"

        self.runner = TrainRunner(
            params=params,
            dataset_yaml=ds,
            project=project,
            name=name,
            device=self.ctx.device_info.device_arg(),
            on_log=self._append_log,
            on_history=self._update_chart,
        )
        self.log_text.delete("1.0", tk.END)
        self._append_log("开始训练…\n")
        self.runner.start()
        self._poll_runner()

    def _poll_runner(self) -> None:
        if self.runner and self.runner.is_running():
            self.win.after(500, self._poll_runner)
        elif self.runner and self.runner.result:
            r = self.runner.result
            if r.success:
                msg = f"完成！最佳权重：{r.best_weights}"
                self.result_label.config(text=msg)
                if r.best_weights:
                    self.ctx.last_weights = r.best_weights
                if r.history.epoch:
                    self._update_chart(r.history)
            else:
                self.result_label.config(text=r.message)

    def _stop(self) -> None:
        if self.runner:
            self.runner.stop()
