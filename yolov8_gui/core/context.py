"""Shared application context and workspace state."""

from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any, Callable, Dict, Optional

from .device import DeviceInfo, probe


class AppContext:
    """Workspace-scoped shared state for the three tools."""

    STATE_FILE = "workspace_state.json"

    def __init__(self, workspace: Optional[str] = None) -> None:
        self.workspace = Path(workspace or os.path.expanduser("~/yolov8_workspace")).resolve()
        self.workspace.mkdir(parents=True, exist_ok=True)
        self.device_info: DeviceInfo = probe()
        self._roots: Dict[str, tk_root_type()] = {}
        self._tool_openers: Dict[str, Callable[..., Any]] = {}
        self._state = self._load_state()

    def _load_state(self) -> dict:
        path = self.workspace / self.STATE_FILE
        if path.exists():
            try:
                with open(path, encoding="utf-8") as f:
                    return json.load(f)
            except Exception:
                pass
        return {}

    def save_state(self) -> None:
        path = self.workspace / self.STATE_FILE
        with open(path, "w", encoding="utf-8") as f:
            json.dump(self._state, f, indent=2, ensure_ascii=False)

    def get(self, key: str, default: Any = None) -> Any:
        return self._state.get(key, default)

    def set(self, key: str, value: Any) -> None:
        self._state[key] = value
        self.save_state()

    @property
    def last_dataset_yaml(self) -> Optional[str]:
        v = self.get("last_dataset_yaml")
        return str(v) if v else None

    @last_dataset_yaml.setter
    def last_dataset_yaml(self, path: str) -> None:
        self.set("last_dataset_yaml", str(path))

    @property
    def last_weights(self) -> Optional[str]:
        v = self.get("last_weights")
        return str(v) if v else None

    @last_weights.setter
    def last_weights(self, path: str) -> None:
        self.set("last_weights", str(path))

    @property
    def last_model_a(self) -> Optional[str]:
        v = self.get("last_model_a")
        return str(v) if v else None

    @last_model_a.setter
    def last_model_a(self, path: str) -> None:
        self.set("last_model_a", str(path))

    @property
    def last_model_b(self) -> Optional[str]:
        v = self.get("last_model_b")
        return str(v) if v else None

    @last_model_b.setter
    def last_model_b(self, path: str) -> None:
        self.set("last_model_b", str(path))

    def set_workspace(self, path: str) -> None:
        self.workspace = Path(path).resolve()
        self.workspace.mkdir(parents=True, exist_ok=True)
        self._state = self._load_state()

    def subdir(self, name: str) -> Path:
        p = self.workspace / name
        p.mkdir(parents=True, exist_ok=True)
        return p

    def register_root(self, key: str, root: Any) -> None:
        self._roots[key] = root

    def register_tool_opener(self, key: str, opener: Callable[..., Any]) -> None:
        self._tool_openers[key] = opener

    def open_tool(self, key: str, **kwargs: Any) -> Any:
        if key not in self._tool_openers:
            raise KeyError(f"Unknown tool: {key}")
        return self._tool_openers[key](**kwargs)

    def refresh_device(self) -> DeviceInfo:
        self.device_info = probe()
        return self.device_info


def tk_root_type():
    try:
        import tkinter as tk

        return tk.Tk | tk.Toplevel
    except Exception:
        return object
