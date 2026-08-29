"""线程安全的 Tkinter UI 投递辅助。

后台线程禁止直接操作 Tk 控件；通过本模块把回调投递到主线程执行。
"""

from __future__ import annotations

import logging
import queue
import tkinter as tk
from typing import Any, Callable, Optional

_log = logging.getLogger(__name__)


def safe_ui(root: tk.Misc, fn: Callable[[], Any]) -> None:
    """将 callable 投递到 Tk 主线程执行（after 仅作调度，回调体在主线程跑）。"""
    if root is None:
        return

    def _wrap() -> None:
        try:
            fn()
        except tk.TclError:
            # 窗口已销毁等
            pass
        except Exception:
            _log.exception("safe_ui 回调异常")

    try:
        root.after(0, _wrap)
    except tk.TclError:
        pass


class ProbeQueue:
    """探测结果队列 + 主线程轮询（标准 Tk 线程安全模式）。"""

    def __init__(self, root: tk.Misc, on_result: Callable[[Any], None], interval_ms: int = 120) -> None:
        self.root = root
        self.on_result = on_result
        self.interval_ms = interval_ms
        self.queue: queue.Queue = queue.Queue()
        self._poll_id: Optional[str] = None
        self._stopped = False

    def put(self, item: Any) -> None:
        self.queue.put(item)

    def start(self) -> None:
        self._stopped = False
        self._schedule()

    def stop(self) -> None:
        self._stopped = True
        if self._poll_id is not None:
            try:
                self.root.after_cancel(self._poll_id)
            except Exception:
                pass
            self._poll_id = None

    def _schedule(self) -> None:
        if self._stopped:
            return
        try:
            self._poll_id = self.root.after(self.interval_ms, self._poll)
        except tk.TclError:
            self._poll_id = None

    def _poll(self) -> None:
        self._poll_id = None
        if self._stopped:
            return
        try:
            while True:
                item = self.queue.get_nowait()
                try:
                    self.on_result(item)
                except Exception:
                    _log.exception("ProbeQueue 消费异常")
                # 探测通常只有一个结果，拿到后停止轮询
                self.stop()
                return
        except queue.Empty:
            pass
        self._schedule()
