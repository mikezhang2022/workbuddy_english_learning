"""Hardware / environment probing for YOLOv8 Visual Studio."""

from __future__ import annotations

import platform
import subprocess
import threading
from dataclasses import dataclass, asdict
from typing import Callable, Optional, Tuple


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
    gpu_via_smi: bool = False
    cudnn_version: Optional[str] = None

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
        if self.cudnn_version:
            lines.append(f"cuDNN：{self.cudnn_version}")
        if self.cuda_available:
            if self.gpu_name:
                lines.append(f"GPU：{self.gpu_name}")
            if self.gpu_mem_total_gb is not None:
                free = self.gpu_mem_free_gb if self.gpu_mem_free_gb is not None else 0
                lines.append(f"显存：可用 {free:.1f} / 总计 {self.gpu_mem_total_gb:.1f} GB")
        elif self.gpu_via_smi:
            if self.gpu_name:
                lines.append(f"GPU（nvidia-smi）：{self.gpu_name}")
            if self.gpu_mem_total_gb is not None:
                free = self.gpu_mem_free_gb if self.gpu_mem_free_gb is not None else 0
                lines.append(f"显存（nvidia-smi）：可用 {free:.1f} / 总计 {self.gpu_mem_total_gb:.1f} GB")
            lines.append("CUDA（PyTorch）：暂不可用，详见提示")
        else:
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


def _probe_nvidia_smi() -> Tuple[Optional[str], Optional[float], Optional[float]]:
    """Parse first GPU from nvidia-smi: (name, total_gb, free_gb)."""
    try:
        result = subprocess.run(
            [
                "nvidia-smi",
                "--query-gpu=name,memory.total,memory.free",
                "--format=csv,noheader,nounits",
            ],
            capture_output=True,
            text=True,
            check=False,
            timeout=10,
        )
        if result.returncode != 0 or not (result.stdout or "").strip():
            return None, None, None
        line = result.stdout.strip().splitlines()[0]
        parts = [p.strip() for p in line.split(",")]
        if len(parts) < 3:
            return None, None, None
        name = parts[0]
        total_gb = float(parts[1]) / 1024.0
        free_gb = float(parts[2]) / 1024.0
        return name, total_gb, free_gb
    except Exception:
        return None, None, None


def probe() -> DeviceInfo:
    """Synchronously probe runtime environment."""
    python_version = platform.python_version()
    cpu_count = os_cpu_count()
    ram_total_gb = _ram_total_gb()
    torch_version = "未安装"
    cuda_available = False
    cuda_version: Optional[str] = None
    cudnn_version: Optional[str] = None
    gpu_name: Optional[str] = None
    gpu_mem_total_gb: Optional[float] = None
    gpu_mem_free_gb: Optional[float] = None
    ultralytics_version = "未安装"
    error: Optional[str] = None
    gpu_via_smi = False

    try:
        import torch

        torch_version = torch.__version__
        cuda_available = torch.cuda.is_available()
        try:
            if torch.backends.cudnn.is_available():
                cudnn_version = str(torch.backends.cudnn.version())
        except Exception:
            cudnn_version = None
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

    if not cuda_available:
        smi_name, smi_total, smi_free = _probe_nvidia_smi()
        if smi_name:
            gpu_name = smi_name
            gpu_mem_total_gb = smi_total
            gpu_mem_free_gb = smi_free
            gpu_via_smi = True
            smi_hint = (
                f"检测到 NVIDIA GPU（{gpu_name}），但 PyTorch 当前无法使用 CUDA。"
                "常见原因：PyTorch 与本地 NVIDIA 驱动/CUDA 版本不匹配；未安装对应 cuDNN；"
                "多显卡（笔记本集显/独显）导致 torch 默认未选中独显。"
                "建议重新安装匹配版本，例如："
                "pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121"
            )
            if error:
                error = f"{error}；{smi_hint}"
            else:
                error = smi_hint
        else:
            no_gpu = "未检测到 NVIDIA GPU（torch.cuda 不可用，且 nvidia-smi 未返回 GPU 信息）。"
            if error:
                error = f"{error}；{no_gpu}"
            else:
                error = no_gpu

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
        gpu_via_smi=gpu_via_smi,
        cudnn_version=cudnn_version,
    )


def os_cpu_count() -> int:
    import os

    return os.cpu_count() or 1


def probe_async(cb: Callable[[DeviceInfo], None]) -> threading.Thread:
    """Run probe in background thread; callback receives result (do not touch Tk in cb)."""

    def _worker():
        try:
            info = probe()
        except Exception as exc:
            info = DeviceInfo(
                python_version="",
                torch_version="未知",
                cuda_available=False,
                cuda_version=None,
                gpu_name=None,
                gpu_mem_total_gb=None,
                gpu_mem_free_gb=None,
                cpu_count=os_cpu_count(),
                ram_total_gb=None,
                ultralytics_version="未知",
                error=str(exc),
            )
        cb(info)

    t = threading.Thread(target=_worker, daemon=True)
    t.start()
    return t


def device_info_dict(info: DeviceInfo) -> dict:
    return asdict(info)
