import pathlib
import sys
import tempfile
import unittest
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2] / 'tools'))
from code_coverage_report import validate

class CodeCoverageReportTests(unittest.TestCase):
    def check(self, xml, expected=('SharpTestBench',)):
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder) / 'coverage.xml'
            path.write_text(xml, encoding='utf-8')
            return validate(path, expected)
