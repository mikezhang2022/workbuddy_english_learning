"""Background training runner with log streaming."""

from __future__ import annotations

import io
import os
import sys
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
    ) -> None:
        self.params = params
        self.dataset_yaml = dataset_yaml
        self.project = project
        self.name = name
        self.device = device
        self.on_log = on_log or (lambda _s: None)
        self.on_history = on_history or (lambda _h: None)
        self._stop = threading.Event()
        self._thread: Optional[threading.Thread] = None
        self.result: Optional[TrainResult] = None
        self.history = TrainHistory()

    def start(self) -> None:
        self._stop.clear()
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        self.on_log("[Runner] Stop requested...\n")

    def is_running(self) -> bool:
        return self._thread is not None and self._thread.is_alive()

    def _run(self) -> None:
        result = TrainResult(success=False)
        try:
            from ultralytics import YOLO

            args = build_train_args(self.params, self.dataset_yaml, self.project, self.name, self.device)
            model_name = args.pop("model")
            self.on_log(f"Loading model {model_name} on device {self.device}\n")
            model = YOLO(model_name)

            capture = _LogCapture(lambda s: self.on_log(s))

            with redirect_stdout(capture), redirect_stderr(capture):
                train_results = model.train(**args)

            if self._stop.is_set():
                result.message = "Training stopped by user"
                self.result = result
                return

            save_dir = Path(getattr(train_results, "save_dir", self.project) or self.project)
            best = save_dir / "weights" / "best.pt"
            if not best.exists():
                best = save_dir / "weights" / "last.pt"

            result.success = True
            result.best_weights = str(best) if best.exists() else None
            result.save_dir = str(save_dir)

            # metrics
            if hasattr(train_results, "results_dict"):
                result.metrics = dict(train_results.results_dict)
            elif hasattr(model, "metrics"):
                try:
                    result.metrics = dict(model.metrics.results_dict)  # type: ignore
                except Exception:
                    pass

            # curve images
            for fname in ("results.png", "confusion_matrix.png", "F1_curve.png", "PR_curve.png"):
                p = save_dir / fname
                if p.exists():
                    result.curves_paths.append(str(p))

            # parse results.csv for history
            csv_path = save_dir / "results.csv"
            if csv_path.exists():
                self._parse_csv(csv_path)
                result.history = self.history

            result.message = "Training completed successfully"
        except Exception as exc:
            result.message = f"Training failed: {exc}"
            self.on_log(result.message + "\n")
        self.result = result

    def _parse_csv(self, csv_path: Path) -> None:
        import csv

        with open(csv_path, newline="", encoding="utf-8") as f:
            reader = csv.DictReader(f)
            for row in reader:
                try:
                    ep = int(float(row.get("epoch", len(self.history.epoch) + 1)))
                    self.history.epoch.append(ep)
                    for key, attr in [
                        ("train/box_loss", "train_loss"),
                        ("val/box_loss", "val_loss"),
                        ("metrics/mAP50(B)", "map50"),
                        ("metrics/mAP50-95(B)", "map50_95"),
                    ]:
                        if key in row and row[key]:
                            val = float(row[key])
                            getattr(self.history, attr).append(val)
                except (ValueError, KeyError):
                    continue
        self.on_history(self.history)
