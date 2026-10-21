"""Failures during optional simulator qualification must retire old success."""
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

import qualify_priority_faults as qualification


class PriorityQualificationSetupTests(unittest.TestCase):
    def check_setup(self, golden, manifest, expected):
        with tempfile.TemporaryDirectory(prefix='stb-priority-setup-') as temporary:
            root = Path(temporary)
            report = root / 'build' / 'setup-failure.json'
            report.parent.mkdir()
            report.write_text('{"matched":true}', encoding='utf-8')
            if golden is not None:
                path = root / 'tests/SharpTestBench.Tools.Tests/priority-seed-vectors.json'
                path.parent.mkdir(parents=True)
                path.write_text(golden, encoding='utf-8')
            if manifest is not None:
                path = root / 'build/priority-83/manifest.json'
                path.parent.mkdir(parents=True)
                path.write_text(manifest, encoding='utf-8')
            with patch.object(qualification, 'ROOT', root), patch.object(sys, 'argv', ['qualify', '--name', 'setup-failure']):
                with self.assertRaises(expected):
                    qualification.main()
            current = json.loads(report.read_text(encoding='utf-8'))
            self.assertFalse(current['matched'])
            self.assertEqual(current['schemaVersion'], 1)
            self.assertEqual(current['cases'], [])
            self.assertTrue(current['error'])
            self.assertTrue(Path(current['workspace']).is_dir())

    def test_missing_golden_retires_success(self):
        self.check_setup(None, None, FileNotFoundError)

    def test_malformed_golden_retires_success(self):
        self.check_setup('{', None, json.JSONDecodeError)

    def test_missing_seed_retires_success(self):
        self.check_setup('{}', None, KeyError)

    def test_missing_manifest_retires_success(self):
        self.check_setup('{"83":[]}', None, FileNotFoundError)

    def test_malformed_manifest_retires_success(self):
        self.check_setup('{"83":[]}', '{', json.JSONDecodeError)