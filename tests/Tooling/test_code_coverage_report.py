import pathlib
import sys
import tempfile
import unittest
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2] / 'tests/Tooling/managed_compat'))
from code_coverage_report import validate

VALID = '<coverage lines-valid="10" lines-covered="5" branches-valid="4" branches-covered="1" line-rate="0.5" branch-rate="0.25"><packages><package name="SharpTestBench"><classes><class name="Example" filename="Example.cs" /></classes></package></packages></coverage>'

class CodeCoverageReportTests(unittest.TestCase):
    def check(self, xml, expected=('SharpTestBench',)):
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder) / 'coverage.xml'
            path.write_text(xml, encoding='utf-8')
            return validate(path, expected)

    def test_missing_lines_valid(self):
        import re
        xml = re.sub(r' lines-valid="[^"]*"', '', VALID)
        with self.assertRaises((KeyError, ValueError)): self.check(xml)

    def test_missing_lines_covered(self):
        import re
        xml = re.sub(r' lines-covered="[^"]*"', '', VALID)
        with self.assertRaises((KeyError, ValueError)): self.check(xml)

    def test_missing_branches_valid(self):
        import re
        xml = re.sub(r' branches-valid="[^"]*"', '', VALID)
        with self.assertRaises((KeyError, ValueError)): self.check(xml)

    def test_missing_branches_covered(self):
        import re
        xml = re.sub(r' branches-covered="[^"]*"', '', VALID)
        with self.assertRaises((KeyError, ValueError)): self.check(xml)

    def test_missing_line_rate(self):
        import re
        xml = re.sub(r' line-rate="[^"]*"', '', VALID)
        with self.assertRaises((KeyError, ValueError)): self.check(xml)

    def test_missing_branch_rate(self):
        import re
        xml = re.sub(r' branch-rate="[^"]*"', '', VALID)
        with self.assertRaises((KeyError, ValueError)): self.check(xml)

    def test_lines_valid_0(self):
        import re
        xml = re.sub(r'lines-valid="[^"]*"', 'lines-valid="0"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_lines_valid_1(self):
        import re
        xml = re.sub(r'lines-valid="[^"]*"', 'lines-valid="-1"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_lines_valid_2(self):
        import re
        xml = re.sub(r'lines-valid="[^"]*"', 'lines-valid="text"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_lines_valid_3(self):
        import re
        xml = re.sub(r'lines-valid="[^"]*"', 'lines-valid="1.5"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branches_valid_0(self):
        import re
        xml = re.sub(r'branches-valid="[^"]*"', 'branches-valid="0"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branches_valid_1(self):
        import re
        xml = re.sub(r'branches-valid="[^"]*"', 'branches-valid="-1"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branches_valid_2(self):
        import re
        xml = re.sub(r'branches-valid="[^"]*"', 'branches-valid="text"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branches_valid_3(self):
        import re
        xml = re.sub(r'branches-valid="[^"]*"', 'branches-valid="1.5"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_lines_covered_0(self):
        import re
        xml = re.sub(r'lines-covered="[^"]*"', 'lines-covered="-1"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_lines_covered_1(self):
        import re
        xml = re.sub(r'lines-covered="[^"]*"', 'lines-covered="11"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_lines_covered_2(self):
        import re
        xml = re.sub(r'lines-covered="[^"]*"', 'lines-covered="text"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_lines_covered_3(self):
        import re
        xml = re.sub(r'lines-covered="[^"]*"', 'lines-covered="0"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branches_covered_0(self):
        import re
        xml = re.sub(r'branches-covered="[^"]*"', 'branches-covered="-1"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branches_covered_1(self):
        import re
        xml = re.sub(r'branches-covered="[^"]*"', 'branches-covered="5"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branches_covered_2(self):
        import re
        xml = re.sub(r'branches-covered="[^"]*"', 'branches-covered="text"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branches_covered_3(self):
        import re
        xml = re.sub(r'branches-covered="[^"]*"', 'branches-covered="1.5"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_line_rate_0(self):
        import re
        xml = re.sub(r'line-rate="[^"]*"', 'line-rate="NaN"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_line_rate_1(self):
        import re
        xml = re.sub(r'line-rate="[^"]*"', 'line-rate="Infinity"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_line_rate_2(self):
        import re
        xml = re.sub(r'line-rate="[^"]*"', 'line-rate="-0.1"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_line_rate_3(self):
        import re
        xml = re.sub(r'line-rate="[^"]*"', 'line-rate="1.1"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_line_rate_4(self):
        import re
        xml = re.sub(r'line-rate="[^"]*"', 'line-rate="0.7"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branch_rate_0(self):
        import re
        xml = re.sub(r'branch-rate="[^"]*"', 'branch-rate="NaN"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branch_rate_1(self):
        import re
        xml = re.sub(r'branch-rate="[^"]*"', 'branch-rate="-Infinity"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branch_rate_2(self):
        import re
        xml = re.sub(r'branch-rate="[^"]*"', 'branch-rate="-0.1"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branch_rate_3(self):
        import re
        xml = re.sub(r'branch-rate="[^"]*"', 'branch-rate="1.1"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_branch_rate_4(self):
        import re
        xml = re.sub(r'branch-rate="[^"]*"', 'branch-rate="0.7"', VALID)
        with self.assertRaises(ValueError): self.check(xml)

    def test_valid_counts(self):
        result = self.check(VALID)
        self.assertEqual(result, self.check('<!DOCTYPE coverage SYSTEM "http://cobertura.sourceforge.net/xml/coverage-04.dtd">' + VALID))
        self.assertEqual(50, result['lines']['percent'])
        self.assertEqual(25, result['branches']['percent'])
        rounded = VALID.replace('branches-valid="4"', 'branches-valid="1297"').replace('branches-covered="1"', 'branches-covered="345"').replace('branch-rate="0.25"', 'branch-rate="0.2659"')
        self.assertEqual(345, self.check(rounded)['branches']['covered'])

    def test_wrong_root(self):
        with self.assertRaises(ValueError): self.check(VALID.replace('coverage', 'report'))

    def test_wrong_assembly(self):
        with self.assertRaises(ValueError): self.check(VALID.replace('SharpTestBench', 'Other'))

    def test_duplicate_package(self):
        xml = VALID.replace('</packages>', '<package name="SharpTestBench"><classes><class /></classes></package></packages>')
        with self.assertRaises(ValueError): self.check(xml)

    def test_empty_classes(self):
        with self.assertRaises(ValueError): self.check(VALID.replace('<class name="Example" filename="Example.cs" />', ''))

    def test_no_packages(self):
        xml = VALID[:VALID.index('<packages>')] + '</coverage>'
        with self.assertRaises(ValueError): self.check(xml)

    def test_dtd(self):
        with self.assertRaises(ValueError): self.check('<!DOCTYPE coverage []>' + VALID)

    def test_entity(self):
        with self.assertRaises(ValueError): self.check('<!ENTITY foo "bar">' + VALID)

    def test_empty_input(self):
        import xml.etree.ElementTree as ET
        with self.assertRaises(ET.ParseError): self.check('')

    def test_extra_package(self):
        xml = VALID.replace('</packages>', '<package name="Other"><classes><class /></classes></package></packages>')
        with self.assertRaises(ValueError): self.check(xml)

    def test_zero_branch_hits(self):
        result = self.check(VALID.replace('branches-covered="1"', 'branches-covered="0"').replace('branch-rate="0.25"', 'branch-rate="0"'))
        self.assertEqual(0, result['branches']['covered'])

    def test_full_coverage(self):
        xml = VALID.replace('lines-covered="5"', 'lines-covered="10"').replace('line-rate="0.5"', 'line-rate="1"')
        self.assertEqual(100, self.check(xml)['lines']['percent'])

    def test_malformed_xml(self):
        import xml.etree.ElementTree as ET
        with self.assertRaises(ET.ParseError): self.check(VALID[:-1])

    def test_oversized(self):
        from unittest.mock import patch
        with patch('code_coverage_report.MAX_BYTES', 10):
            with self.assertRaises(ValueError): self.check(VALID)

    def test_utf16_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder) / 'coverage.xml'
            path.write_bytes(VALID.encode('utf-16'))
            with self.assertRaises(UnicodeError): validate(path, ('SharpTestBench',))
