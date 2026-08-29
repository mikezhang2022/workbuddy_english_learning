"""Core package public API."""

from .augment import AugConfig, estimate_new_samples, preview, run as augment_run
from .compare import CompareReport, CompareStats, compare, export_report_html
from .context import AppContext
from .dataset_qc import DatasetReport, Issue, apply_fixes, check as dataset_check
from .device import DeviceInfo, probe, probe_async
from .io_utils import MediaItem, extract_frames, imread_unicode, imwrite_unicode, list_media
from .theme import (
    COLORS,
    apply_theme,
    center_window,
    configure_theme,
    enable_dpi,
    f,
    get_font_size,
    get_ui_scale,
    setup_matplotlib,
)
from .app_config import load_config, save_config
from .train_engine import AdviceItem, build_train_args, default_params, validate_params
from .train_runner import TrainHistory, TrainResult, TrainRunner
from .yolo_engine import (
    DEFAULT_MODEL,
    OFFICIAL_MODELS,
    Det,
    Dets,
    Predictor,
    annotate_video,
    draw_dets,
    load_model,
    model_display_name,
)

__all__ = [
    "AppContext",
    "AugConfig",
    "AdviceItem",
    "COLORS",
    "CompareReport",
    "CompareStats",
    "DatasetReport",
    "DEFAULT_MODEL",
    "Det",
    "Dets",
    "DeviceInfo",
    "Issue",
    "MediaItem",
    "OFFICIAL_MODELS",
    "Predictor",
    "TrainHistory",
    "TrainResult",
    "TrainRunner",
    "annotate_video",
    "apply_fixes",
    "apply_theme",
    "augment_run",
    "build_train_args",
    "center_window",
    "compare",
    "configure_theme",
    "dataset_check",
    "default_params",
    "draw_dets",
    "enable_dpi",
    "estimate_new_samples",
    "export_report_html",
    "extract_frames",
    "f",
    "get_font_size",
    "get_ui_scale",
    "imread_unicode",
    "imwrite_unicode",
    "list_media",
    "load_config",
    "load_model",
    "model_display_name",
    "preview",
    "probe",
    "probe_async",
    "save_config",
    "setup_matplotlib",
    "validate_params",
]
