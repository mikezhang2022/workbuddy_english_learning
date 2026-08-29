"""Hardware / environment probing for YOLOv8 Visual Studio."""

from __future__ import annotations

import platform
import sys
import threading
from dataclasses import dataclass, asdict
from typing import Callable, Optional


@dataclass
class DeviceInfo:
    python_version: str
    torch_version: str
    cuda_available: bool
    cuda_version: Optional[str]
    gpu_name: Optional[str]
    gpu_mem_total_gb: Optional[float]
    gpu_mem_free_gb: Optional[float]
    cpu_count: int
    ram_total_gb: Optional[float]
    ultralytics_version: str
    error: Optional[str] = None

    def device_arg(self) -> int | str:
        """Return ultralytics device argument."""
        return 0 if self.cuda_available else "cpu"

    def summary_lines(self) -> list[str]:
        lines = [
            f"Python：{self.python_version}",
            f"PyTorch：{self.torch_version}",
            f"CUDA：{'是' if self.cuda_available else '否'}"
            + (f"（{self.cuda_version}）" if self.cuda_version else ""),
        ]
        if self.gpu_name:
            lines.append(f"GPU：{self.gpu_name}")
        if self.gpu_mem_total_gb is not None:
            free = self.gpu_mem_free_gb if self.gpu_mem_free_gb is not None else 0
            lines.append(f"显存：可用 {free:.1f} / 总计 {self.gpu_mem_total_gb:.1f} GB")
        lines.append(f"CPU 核心数：{self.cpu_count}")
        if self.ram_total_gb is not None:
            lines.append(f"内存：{self.ram_total_gb:.1f} GB")
        lines.append(f"Ultralytics：{self.ultralytics_version}")
        if self.error:
            lines.append(f"提示：{self.error}")
        return lines


def _ram_total_gb() -> Optional[float]:
    try:
        import psutil

        return psutil.virtual_memory().total / (1024**3)
    except Exception:
        try:
            with open("/proc/meminfo") as f:
                for line in f:
                    if line.startswith("MemTotal:"):
                        kb = int(line.split()[1])
                        return kb / (1024**2)
        except Exception:
            pass
    return None


def probe() -> DeviceInfo:
    """Synchronously probe runtime environment."""
    python_version = platform.python_version()
    cpu_count = os_cpu_count()
    ram_total_gb = _ram_total_gb()
    torch_version = "未安装"
    cuda_available = False
    cuda_version: Optional[str] = None
    gpu_name: Optional[str] = None
    gpu_mem_total_gb: Optional[float] = None
    gpu_mem_free_gb: Optional[float] = None
    ultralytics_version = "未安装"
    error: Optional[str] = None

    try:
        import torch

        torch_version = torch.__version__
        cuda_available = torch.cuda.is_available()
        if cuda_available:
            cuda_version = torch.version.cuda
            try:
                gpu_name = torch.cuda.get_device_name(0)
                props = torch.cuda.get_device_properties(0)
                gpu_mem_total_gb = props.total_memory / (1024**3)
                free, _total = torch.cuda.mem_get_info(0)
                gpu_mem_free_gb = free / (1024**3)
            except Exception as exc:
                error = f"GPU 检测不完整：{exc}"
    except Exception as exc:
        error = f"PyTorch 不可用：{exc}"

    try:
        import ultralytics

        ultralytics_version = ultralytics.__version__
    except Exception as exc:
        if error:
            error += f"；Ultralytics：{exc}"
        else:
            error = f"Ultralytics 不可用：{exc}"

    return DeviceInfo(
        python_version=python_version,
        torch_version=torch_version,
        cuda_available=cuda_available,
        cuda_version=cuda_version,
        gpu_name=gpu_name,
        gpu_mem_total_gb=gpu_mem_total_gb,
        gpu_mem_free_gb=gpu_mem_free_gb,
        cpu_count=cpu_count,
        ram_total_gb=ram_total_gb,
        ultralytics_version=ultralytics_version,
        error=error,
    )


def os_cpu_count() -> int:
    import os

    return os.cpu_count() or 1


def probe_async(cb: Callable[[DeviceInfo], None]) -> threading.Thread:
    """Run probe in background thread and invoke callback on main thread via cb."""

    def _worker():
        info = probe()
        cb(info)

    t = threading.Thread(target=_worker, daemon=True)
    t.start()
    return t


def device_info_dict(info: DeviceInfo) -> dict:
    return asdict(info)
