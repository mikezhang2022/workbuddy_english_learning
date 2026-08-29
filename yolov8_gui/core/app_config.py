"""界面配置持久化（字体大小 / UI 缩放）。"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Dict, Optional

DEFAULT_FONT_SIZE = 12
DEFAULT_UI_SCALE = 1.0

# 程序目录下的配置文件（与包同级旁：yolov8_gui/yolov8_gui_config.json）
_PACKAGE_DIR = Path(__file__).resolve().parent.parent
CONFIG_FILENAME = "yolov8_gui_config.json"
WORKSPACE_CONFIG_NAME = "config.json"


def default_config() -> Dict[str, Any]:
    return {
        "font_size": DEFAULT_FONT_SIZE,
        "ui_scale": DEFAULT_UI_SCALE,
    }


def config_path(workspace: Optional[Path | str] = None) -> Path:
    """优先工作区 config.json；否则程序目录 yolov8_gui_config.json。"""
    if workspace is not None:
        ws = Path(workspace)
        ws_cfg = ws / WORKSPACE_CONFIG_NAME
        if ws_cfg.exists():
            return ws_cfg
    return _PACKAGE_DIR / CONFIG_FILENAME


def load_config(workspace: Optional[Path | str] = None) -> Dict[str, Any]:
    cfg = default_config()
    paths = []
    if workspace is not None:
        paths.append(Path(workspace) / WORKSPACE_CONFIG_NAME)
    paths.append(_PACKAGE_DIR / CONFIG_FILENAME)

    for path in paths:
        if not path.exists():
            continue
        try:
            with open(path, encoding="utf-8") as f:
                data = json.load(f)
            if isinstance(data, dict):
                if "font_size" in data:
                    cfg["font_size"] = int(data["font_size"])
                if "ui_scale" in data:
                    cfg["ui_scale"] = float(data["ui_scale"])
                break
        except Exception:
            continue

    cfg["font_size"] = max(8, min(24, int(cfg["font_size"])))
    cfg["ui_scale"] = max(0.8, min(1.5, round(float(cfg["ui_scale"]) * 20) / 20))
    return cfg


def save_config(
    font_size: int,
    ui_scale: float,
    workspace: Optional[Path | str] = None,
) -> Path:
    """同时写入程序目录配置；若提供工作区则也写入 workspace/config.json。"""
    data = {
        "font_size": max(8, min(24, int(font_size))),
        "ui_scale": max(0.8, min(1.5, float(ui_scale))),
    }
    pkg_path = _PACKAGE_DIR / CONFIG_FILENAME
    with open(pkg_path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)

    if workspace is not None:
        ws = Path(workspace)
        ws.mkdir(parents=True, exist_ok=True)
        ws_path = ws / WORKSPACE_CONFIG_NAME
        with open(ws_path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        return ws_path
    return pkg_path
