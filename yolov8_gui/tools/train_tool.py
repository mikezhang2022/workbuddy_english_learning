"""Tool 2 — Model training."""

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
from ..core.theme import ACCENT, ERROR, WARN, apply_theme, center_window, f, setup_matplotlib
from ..core.train_engine import AdviceItem, default_params, validate_params
from ..core.train_runner import TrainHistory, TrainRunner


class TrainTool:
    """YOLOv8 training with auto/manual modes and live charts."""

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
        self.win.title("Tool 2 — Model Training")
        apply_theme(self.win)
        center_window(self.win, 1100, 850)

        self._build_ui()
        self._load_defaults()
        self.win.protocol("WM_DELETE_WINDOW", self.win.destroy)

    def _build_ui(self) -> None:
        top = ttk.Frame(self.win, padding=8)
        top.pack(fill=tk.X)

        ttk.Label(top, text="Dataset (data.yaml):").pack(side=tk.LEFT)
        self.dataset_var = tk.StringVar(value=self.dataset_yaml)
        ttk.Entry(top, textvariable=self.dataset_var, width=50).pack(side=tk.LEFT, padx=4)
        ttk.Button(top, text="Browse", command=self._browse_dataset).pack(side=tk.LEFT)
        ttk.Button(top, text="Send to Tool 1 →", command=self._send_to_image).pack(side=tk.LEFT, padx=8)

        mode_frame = ttk.Frame(self.win, padding=4)
        mode_frame.pack(fill=tk.X)
        ttk.Radiobutton(mode_frame, text="AUTO (recommended)", variable=self.mode, value="auto", command=self._on_mode).pack(side=tk.LEFT, padx=8)
        ttk.Radiobutton(mode_frame, text="MANUAL", variable=self.mode, value="manual", command=self._on_mode).pack(side=tk.LEFT)

        paned = ttk.PanedWindow(self.win, orient=tk.HORIZONTAL)
        paned.pack(fill=tk.BOTH, expand=True, padx=8, pady=4)

        params_frame = ttk.LabelFrame(paned, text="Parameters", padding=8)
        paned.add(params_frame, weight=1)

        self.params_grid = ttk.Frame(params_frame)
        self.params_grid.pack(fill=tk.BOTH, expand=True)
        for key in self.PARAM_KEYS:
            row = ttk.Frame(self.params_grid)
            row.pack(fill=tk.X, pady=2)
            ttk.Label(row, text=key, width=14).pack(side=tk.LEFT)
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

        advice_frame = ttk.LabelFrame(right, text="Training Advice", padding=8)
        advice_frame.pack(fill=tk.X)
        self.advice_list = tk.Listbox(advice_frame, height=5, bg="#313244", fg="#cdd6f4")
        self.advice_list.pack(fill=tk.X)
        self._advice_items: list = []
        ttk.Button(advice_frame, text="Apply Selected Fix", command=self._apply_fix).pack(pady=4)

        log_frame = ttk.LabelFrame(right, text="Training Log", padding=4)
        log_frame.pack(fill=tk.BOTH, expand=True, pady=4)
        self.log_text = tk.Text(log_frame, height=10, bg="#313244", fg="#cdd6f4", wrap=tk.WORD)
        self.log_text.pack(fill=tk.BOTH, expand=True, side=tk.LEFT)
        sb = ttk.Scrollbar(log_frame, command=self.log_text.yview)
        sb.pack(side=tk.RIGHT, fill=tk.Y)
        self.log_text.config(yscrollcommand=sb.set)

        chart_frame = ttk.LabelFrame(right, text="Live Metrics", padding=4)
        chart_frame.pack(fill=tk.BOTH, expand=True)
        self.fig = Figure(figsize=(5, 3), dpi=100)
        self.ax = self.fig.add_subplot(111)
        self.canvas = FigureCanvasTkAgg(self.fig, chart_frame)
        self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True)

        btn_row = ttk.Frame(self.win, padding=8)
        btn_row.pack(fill=tk.X)
        ttk.Button(btn_row, text="Start Training", command=self._start, style="Accent.TButton").pack(side=tk.LEFT, padx=4)
        ttk.Button(btn_row, text="Stop", command=self._stop).pack(side=tk.LEFT, padx=4)
        self.result_label = ttk.Label(btn_row, text="", style="Dim.TLabel")
        self.result_label.pack(side=tk.LEFT, padx=16)

        device_txt = f"Device: {self.ctx.device_info.device_arg()}"
        if self.ctx.device_info.cuda_available:
            device_txt += f" ({self.ctx.device_info.gpu_name})"
        else:
            device_txt += " (CPU — no CUDA)"
        ttk.Label(self.win, text=device_txt, style="Dim.TLabel").pack(fill=tk.X, padx=8)

    def _browse_dataset(self) -> None:
        p = filedialog.askopenfilename(filetypes=[("YAML", "*.yaml *.yml"), ("All", "*.*")])
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
            self.rationale_label.config(text="Manual mode — adjust parameters as needed.")
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
            messagebox.showerror("Error", str(exc))

    def _append_log(self, text: str) -> None:
        def ui():
            self.log_text.insert(tk.END, text)
            self.log_text.see(tk.END)

        self.win.after(0, ui)

    def _update_chart(self, history: TrainHistory) -> None:
        def ui():
            self.ax.clear()
            if history.epoch:
                self.ax.plot(history.epoch, history.train_loss, label="train loss", color="#89b4fa")
                if history.val_loss:
                    self.ax.plot(history.epoch[: len(history.val_loss)], history.val_loss, label="val loss", color="#f38ba8")
                if history.map50:
                    self.ax.plot(history.epoch[: len(history.map50)], history.map50, label="mAP50", color="#a6e3a1")
            self.ax.legend(loc="upper right", fontsize=8)
            self.ax.set_xlabel("Epoch")
            self.canvas.draw()

        self.win.after(0, ui)

    def _start(self) -> None:
        ds = self.dataset_var.get()
        if not ds or not os.path.isfile(ds):
            messagebox.showerror("Training", "Select a valid data.yaml")
            return
        if self.runner and self.runner.is_running():
            messagebox.showwarning("Training", "Already running")
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
        self._append_log("Starting training...\n")
        self.runner.start()
        self._poll_runner()

    def _poll_runner(self) -> None:
        if self.runner and self.runner.is_running():
            self.win.after(500, self._poll_runner)
        elif self.runner and self.runner.result:
            r = self.runner.result
            if r.success:
                msg = f"Done! Best: {r.best_weights}"
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
