#!/usr/bin/env python3
"""Headless smoke tests for yolov8_gui core logic."""

from __future__ import annotations

import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import numpy as np

# Project root
ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))


class TestImports(unittest.TestCase):
    def test_core_imports(self):
        from yolov8_gui.core import (
            AppContext,
            AugConfig,
            CompareReport,
            DeviceInfo,
            Predictor,
            default_params,
            probe,
        )

        self.assertTrue(callable(probe))
        self.assertTrue(callable(default_params))


class TestDevice(unittest.TestCase):
    def test_probe(self):
        from yolov8_gui.core.device import probe

        info = probe()
        self.assertIsInstance(info.python_version, str)
        self.assertIsInstance(info.cpu_count, int)
        self.assertIn(info.device_arg(), [0, "cpu"])


class TestAugment(unittest.TestCase):
    def test_preview_and_run(self):
        from yolov8_gui.core.augment import AugConfig, preview, run

        img = np.random.randint(0, 255, (64, 64, 3), dtype=np.uint8)
        cfg = AugConfig(hflip=True, brightness=True)
        samples = preview(img, cfg, n=2)
        self.assertEqual(len(samples), 2)

        with tempfile.TemporaryDirectory() as td:
            td_path = Path(td)
            img_path = td_path / "test.jpg"
            import cv2

            cv2.imwrite(str(img_path), img)
            lbl = td_path / "test.txt"
            lbl.write_text("0 0.5 0.5 0.3 0.3\n")
            n = run(td_path, td_path, cfg, multiplier=1, out_images_dir=td_path / "out", out_labels_dir=td_path / "out_lbl")
            self.assertGreaterEqual(n, 1)


class TestDatasetQC(unittest.TestCase):
    def test_check_tiny_sample(self):
        from yolov8_gui.core.dataset_qc import check

        with tempfile.TemporaryDirectory() as td:
            td_path = Path(td)
            import cv2

            p = td_path / "img.jpg"
            cv2.imwrite(str(p), np.full((32, 32, 3), 128, dtype=np.uint8))
            (td_path / "img.txt").write_text("0 0.5 0.5 0.2 0.2\n")
            report = check(td_path)
            self.assertEqual(report.total_images, 1)


class TestTrainEngine(unittest.TestCase):
    def test_default_and_validate(self):
        from yolov8_gui.core.device import probe
        from yolov8_gui.core.train_engine import default_params, validate_params

        params, rationale = default_params()
        self.assertIn("epochs", params)
        self.assertIn("epochs", rationale)
        advice = validate_params(params, None, probe())
        self.assertIsInstance(advice, list)


class TestCompareEngine(unittest.TestCase):
    def test_dummy_compare(self):
        from yolov8_gui.core.compare import CompareReport, _iou, _match_boxes
        from yolov8_gui.core.yolo_engine import Det

        a = Det(0, "cat", 0.9, (10, 10, 50, 50))
        b = Det(0, "cat", 0.85, (12, 12, 52, 52))
        self.assertGreater(_iou(a, b), 0.5)
        pairs, only_a, only_b = _match_boxes([a], [b])
        self.assertEqual(len(pairs), 1)


class TestIOUtils(unittest.TestCase):
    def test_imread_write_unicode(self):
        from yolov8_gui.core.io_utils import imread_unicode, imwrite_unicode

        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "测试.jpg"
            img = np.zeros((16, 16, 3), dtype=np.uint8)
            self.assertTrue(imwrite_unicode(p, img))
            loaded = imread_unicode(p)
            self.assertIsNotNone(loaded)


class TestContext(unittest.TestCase):
    def test_workspace_state(self):
        from yolov8_gui.core.context import AppContext

        with tempfile.TemporaryDirectory() as td:
            ctx = AppContext(td)
            ctx.last_dataset_yaml = "/tmp/data.yaml"
            ctx2 = AppContext(td)
            self.assertEqual(ctx2.last_dataset_yaml, "/tmp/data.yaml")


class TestAnnotationReview(unittest.TestCase):
    """模拟创建临时图片+txt：加载、删低置信度框、保存，校验内容。"""

    def test_load_delete_low_conf_save(self):
        from yolov8_gui.core.annotation_review import (
            delete_below_threshold,
            draw_preview,
            load_annotations,
            remap_class_names_to_ids,
            save_annotations,
        )
        from yolov8_gui.core.version import __version__
        import cv2

        self.assertEqual(__version__, "1.0.5")

        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            images = root / "images"
            labels = root / "labels"
            images.mkdir()
            labels.mkdir()

            img_path = images / "sample.jpg"
            cv2.imwrite(str(img_path), np.full((64, 64, 3), 120, dtype=np.uint8))

            # 含置信度的扩展 YOLO 行：高 conf + 低 conf
            (labels / "sample.txt").write_text(
                "0 0.50 0.50 0.40 0.40 0.90\n"
                "1 0.20 0.20 0.10 0.10 0.15\n"
                "2 0.80 0.80 0.12 0.12 0.10\n",
                encoding="utf-8",
            )

            names = ["cat", "dog", "bird"]
            anns = load_annotations(img_path, labels, names)
            self.assertEqual(len(anns), 3)
            self.assertEqual(anns[0].cls_name, "cat")
            self.assertAlmostEqual(anns[1].conf, 0.15)

            kept = delete_below_threshold(anns, 0.25)
            self.assertEqual(len(kept), 1)
            self.assertEqual(kept[0].cls_id, 0)

            save_annotations(img_path, labels, kept, with_conf=False)
            text = (labels / "sample.txt").read_text(encoding="utf-8").strip()
            parts = text.split()
            self.assertEqual(len(parts), 5)
            self.assertEqual(parts[0], "0")
            self.assertAlmostEqual(float(parts[1]), 0.5, places=4)

            # 再读回（无 conf 列时默认 1.0）
            reloaded = load_annotations(img_path, labels, names)
            self.assertEqual(len(reloaded), 1)
            self.assertAlmostEqual(reloaded[0].conf, 1.0)

            preview = draw_preview(img_path, kept, conf_threshold=0.25, class_names=names)
            self.assertIsNotNone(preview)
            self.assertEqual(preview.shape[:2], (64, 64))

            mapping = remap_class_names_to_ids(names)
            self.assertEqual(mapping["dog"], 1)


def run_compileall() -> int:
    import compileall

    return 0 if compileall.compile_dir(str(ROOT / "yolov8_gui"), quiet=1) else 1


if __name__ == "__main__":
    # Skip GUI tests; mock display if needed
    if not os.environ.get("DISPLAY"):
        os.environ.setdefault("MPLBACKEND", "Agg")

    rc = run_compileall()
    if rc:
        print("compileall: FAIL")
        sys.exit(1)
    print("compileall: OK")

    loader = unittest.TestLoader()
    suite = loader.loadTestsFromModule(sys.modules[__name__])
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    sys.exit(0 if result.wasSuccessful() else 1)
