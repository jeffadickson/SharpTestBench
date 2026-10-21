"""Bounded strict data-only inputs for the four independent HDL oracle CLIs."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from fifo_reference import read_json, MAXIMUM_JSON_BYTES


class Tests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.path=Path(self.temp.name)/'input.json'

    def read(self,raw,maximum=MAXIMUM_JSON_BYTES):
        self.path.write_bytes(raw);return read_json(self.path,maximum)

    def test_exact_byte_budget_and_bom(self):
        self.assertEqual({},self.read(b'{}',2))
        self.assertEqual({},self.read(b'\xef\xbb\xbf{}',5))
        with self.assertRaisesRegex(ValueError,'byte budget'):self.read(b'{} ',2)
        for maximum in [True,0,-1,MAXIMUM_JSON_BYTES+1]:
            with self.subTest(maximum=maximum),self.assertRaises(ValueError):self.read(b'{}',maximum)

    def test_duplicate_keys_include_escaped_equivalents(self):
        for raw in [b'{"a":1,"a":2}',b'{"a":1,"\\u0061":2}',b'{"nested":{"a":1,"a":2}}']:
            with self.subTest(raw=raw),self.assertRaisesRegex(ValueError,'duplicate'):self.read(raw)

    def test_nonfinite_numbers_reject_all_spellings_and_overflow(self):
        for raw in [b'NaN',b'Infinity',b'-Infinity',b'1e999',b'{"x":-1e999}']:
            with self.subTest(raw=raw),self.assertRaisesRegex(ValueError,'nonfinite'):self.read(raw)
        self.assertEqual([1.25,None,True],self.read(b'[1.25,null,true]'))

    def test_container_depth_boundary(self):
        self.read(b'['*64+b'0'+b']'*64)
        with self.assertRaisesRegex(ValueError,'nesting'):self.read(b'['*65+b'0'+b']'*65)
        with self.assertRaises(ValueError):self.read(b'['*2000+b'0'+b']'*2000)

    def test_utf8_and_unicode_scalar_boundaries(self):
        self.assertEqual('😀',self.read(b'"\\ud83d\\ude00"'))
        self.assertEqual('Ω',self.read('"Ω"'.encode()))
        for raw in [b'"\xff"',b'"\\ud800"',b'{"\\udfff":0}']:
            with self.subTest(raw=raw),self.assertRaises(ValueError):self.read(raw)

    def test_actual_oracle_clis_reject_bounded_malformed_inputs(self):
        root=Path(__file__).resolve().parent
        config=Path(self.temp.name)/'config.json';config.write_text('{}')
        for script in ['fifo_reference.py','frame_reference.py','frame_drop_reference.py','arbiter_reference.py']:
            for raw,diagnostic in [(b' '*(MAXIMUM_JSON_BYTES+1),'byte budget'),(b'{"a":1,"a":2}','duplicate'),(b'"\\ud800"','Unicode scalar')]:
                with self.subTest(script=script,diagnostic=diagnostic):
                    self.path.write_bytes(raw)
                    args=[sys.executable,str(root/script),'--plan',str(self.path),'--metrics',str(config)]
                    if script!='fifo_reference.py':args+=['--configuration',str(self.path)]
                    result=subprocess.run(args,capture_output=True,text=True,timeout=15)
                    self.assertNotEqual(0,result.returncode);self.assertEqual('',result.stdout)
                    self.assertIn(diagnostic,result.stderr)
