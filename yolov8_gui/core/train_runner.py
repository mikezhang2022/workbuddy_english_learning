"""Background training runner with log streaming."""

from __future__ import annotations

import io
import threading
import time
from contextlib import redirect_stderr, redirect_stdout
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Dict, List, Optional

from .train_engine import build_train_args


@dataclass
class TrainHistory:
    epoch: List[int] = field(default_factory=list)
    train_loss: List[float] = field(default_factory=list)
    val_loss: List[float] = field(default_factory=list)
    map50: List[float] = field(default_factory=list)
    map50_95: List[float] = field(default_factory=list)


@dataclass
class TrainResult:
    success: bool
    best_weights: Optional[str] = None
    save_dir: Optional[str] = None
    metrics: Dict = field(default_factory=dict)
    history: TrainHistory = field(default_factory=TrainHistory)
    curves_paths: List[str] = field(default_factory=list)
    message: str = ""


class _LogCapture(io.StringIO):
    def __init__(self, callback: Callable[[str], None]) -> None:
        super().__init__()
        self._cb = callback

    def write(self, s: str) -> int:
        if s.strip():
            self._cb(s)
        return super().write(s)

    def flush(self) -> None:
        # 确保逐行实时刷到 UI（redirect 可能调用 flush）
        return None


class TrainRunner:
    """Run YOLO training in a background thread."""

    def __init__(
        self,
        params: dict,
        dataset_yaml: str,
        project: str,
        name: str,
        device: int | str,
        on_log: Optional[Callable[[str], None]] = None,
        on_history: Optional[Callable[[TrainHistory], None]] = None,
        on_progress: Optional[Callable[[int, int, str], None]] = None,
    ) -> None:
        self.params = params
        self.dataset_yaml = dataset_yaml
        self.project = project
        self.name = name
        self.device = device
        self.on_log = on_log or (lambda _s: None)
        self.on_history = on_history or (lambda _h: None)
        self.on_progress = on_progress or (lambda _e, _t, _m: None)
        self._stop = threading.Event()
        self._thread: Optional[threading.Thread] = None
        self.result: Optional[TrainResult] = None
        self.history = TrainHistory()
        self._last_csv_epochs = 0
        self._total_epochs = int(params.get("epochs", 0) or 0)

    def start(self) -> None:
        self._stop.clear()
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        self.on_log("[训练器] 已请求停止…\n")

    def is_running(self) -> bool:
        return self._thread is not None and self._thread.is_alive()

    def _resolve_device(self) -> int | str:
        """CUDA 可用且 device 为 0 时强制传整数 0，禁止字符串。"""
        if self.device == 0 or self.device == "0":
            return 0
        return self.device

    def _run(self) -> None:
        result = TrainResult(success=False)
        try:
            from ultralytics import YOLO

            device = self._resolve_device()
            args = build_train_args(
                self.params, self.dataset_yaml, self.project, self.name, device
            )
            model_name = args.pop("model")
            # 再次确保 device 为 int 0（防止上游写成字符串）
            if device == 0:
                args["device"] = 0

            self._total_epochs = int(args.get("epochs", self._total_epochs) or 0)
            self.on_log(f"【训练配置】模型={model_name}  实际 device={device!r}\n")
            if device == "cpu" or self.device == "cpu":
                self.on_log(
                    "⚠⚠⚠ 警告：当前使用 CPU 训练。"
                    "若本机有 NVIDIA 显卡，说明 PyTorch 是 CPU 版本，"
                    "需重装 GPU 版 torch（cu121）。\n"
                )

            self.on_log(f"正在加载模型 {model_name}，设备 {device}\n")
            model = YOLO(model_name)

            save_dir_guess = Path(self.project) / self.name
            capture = _LogCapture(lambda s: self.on_log(s))
            train_holder: list = []
            train_error: list = []

            def do_train() -> None:
                try:
                    with redirect_stdout(capture), redirect_stderr(capture):
                        train_results = model.train(**args)
                    train_holder.append(train_results)
                except Exception as exc:
                    train_error.append(exc)

            train_thread = threading.Thread(target=do_train, daemon=True)
            train_thread.start()

            # 训练期间每 2 秒轮询 results.csv，新 epoch 立即回调
            while train_thread.is_alive():
                self._poll_results_csv(save_dir_guess)
                # 用 join 超时等待，同时保证至少约 2s 一轮
                t0 = time.time()
                train_thread.join(timeout=2.0)
                elapsed = time.time() - t0
                if train_thread.is_alive() and elapsed < 1.5:
                    time.sleep(2.0 - elapsed)

            # 训练线程已结束，再读一次
            self._poll_results_csv(save_dir_guess)

            if train_error:
                raise train_error[0]

            if self._stop.is_set():
                result.message = "训练已被用户停止"
                self.result = result
                return

            train_results = train_holder[0] if train_holder else None
            save_dir = Path(
                getattr(train_results, "save_dir", save_dir_guess) if train_results else save_dir_guess
            )
            best = save_dir / "weights" / "best.pt"
            if not best.exists():
                best = save_dir / "weights" / "last.pt"

            result.success = True
            result.best_weights = str(best) if best.exists() else None
            result.save_dir = str(save_dir)

            if train_results is not None and hasattr(train_results, "results_dict"):
                result.metrics = dict(train_results.results_dict)
            elif hasattr(model, "metrics"):
                try:
                    result.metrics = dict(model.metrics.results_dict)  # type: ignore
                except Exception:
                    pass

            for fname in ("results.png", "confusion_matrix.png", "F1_curve.png", "PR_curve.png"):
                p = save_dir / fname
                if p.exists():
                    result.curves_paths.append(str(p))

            csv_path = save_dir / "results.csv"
            if csv_path.exists():
                self._parse_csv(csv_path, incremental=False)
                result.history = self.history

            result.message = "训练成功完成"
        except Exception as exc:
            result.message = f"训练失败：{exc}"
            self.on_log(result.message + "\n")
        self.result = result

    def _poll_results_csv(self, save_dir: Path) -> None:
        csv_path = save_dir / "results.csv"
        if not csv_path.exists():
            # Ultralytics 有时把结果写在 runs 子目录外的实际 save_dir；尝试常见路径
            return
        self._parse_csv(csv_path, incremental=True)

    def _parse_csv(self, csv_path: Path, incremental: bool = False) -> None:
        import csv

        try:
            with open(csv_path, newline="", encoding="utf-8") as f:
                reader = csv.DictReader(f)
                rows = list(reader)
        except Exception:
            return

        if not rows:
            return

        if incremental and len(rows) <= self._last_csv_epochs:
            return

        # 重建 history（CSV 是权威来源）
        new_history = TrainHistory()
        for row in rows:
            try:
                ep = int(float(row.get("epoch", len(new_history.epoch) + 1)))
                new_history.epoch.append(ep)
                for key, attr in [
                    ("train/box_loss", "train_loss"),
                    ("val/box_loss", "val_loss"),
                    ("metrics/mAP50(B)", "map50"),
                    ("metrics/mAP50-95(B)", "map50_95"),
                ]:
                    if key in row and row[key]:
                        val = float(row[key])
                        getattr(new_history, attr).append(val)
            except (ValueError, KeyError):
                continue

        prev = self._last_csv_epochs
        self.history = new_history
        self._last_csv_epochs = len(new_history.epoch)

        if self._last_csv_epochs > prev or not incremental:
            self.on_history(self.history)
            if new_history.epoch:
                cur = new_history.epoch[-1]
                total = self._total_epochs or cur
                metrics_bits = []
                if new_history.train_loss:
                    metrics_bits.append(f"loss={new_history.train_loss[-1]:.4f}")
                if new_history.map50:
                    metrics_bits.append(f"mAP50={new_history.map50[-1]:.4f}")
                if new_history.val_loss:
                    metrics_bits.append(f"val_loss={new_history.val_loss[-1]:.4f}")
                metrics_line = "  ".join(metrics_bits)
                self.on_progress(cur, total, metrics_line)
