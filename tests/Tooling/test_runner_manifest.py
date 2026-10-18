import json
from pathlib import Path
import sys
import unittest
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import dual_runner_parity as parity

import subprocess

class Tests(unittest.TestCase):
    def setUp(self):
        self.data = {'schemaVersion':1,'simulator':'icarus','top':'counter','sources':['counter.sv'],
                     'assembly':'a.dll','nativeHost':'host.so','icarusBootstrap':'b.vpi','gpiLibrary':'gpi.so',
                     'dotnetRoot':'dotnet','outputDirectory':'build/native','gpiImpl':'impl',
                     'expectedTests':1,'expectedNames':['Entry.Test']}

    def valid(self):
        executable=ROOT/'tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll'
        result=subprocess.run(['dotnet',str(executable),'manifest'],input=json.dumps(self.data),capture_output=True,text=True,check=True)
        return json.loads(result.stdout)['valid']


    def test_schema(self):
        self.data['schemaVersion']=2
        self.assertFalse(self.valid())

    def test_simulator(self):
        self.data['simulator']='verilator'
        self.assertFalse(self.valid())

    def test_no_tests(self):
        self.data['expectedTests']=0
        self.assertFalse(self.valid())

    def test_negative_tests(self):
        self.data['expectedTests']=-1
        self.assertFalse(self.valid())

    def test_empty_names(self):
        self.data['expectedNames']=[]
        self.assertFalse(self.valid())

    def test_null_name(self):
        self.data['expectedNames']=[None]
        self.assertFalse(self.valid())

    def test_empty_name(self):
        self.data['expectedNames']=['']
        self.assertFalse(self.valid())

    def test_blank_name(self):
        self.data['expectedNames']=[' ']
        self.assertFalse(self.valid())

    def test_padded_name(self):
        self.data['expectedNames']=[' a']
        self.assertFalse(self.valid())

    def test_control_name(self):
        self.data['expectedNames']=['a\n']
        self.assertFalse(self.valid())

    def test_name_count(self):
        self.data['expectedNames']=['a', 'b']
        self.assertFalse(self.valid())

    def test_no_sources(self):
        self.data['sources']=[]
        self.assertFalse(self.valid())

    def test_null_sources(self):
        self.data['sources']=None
        self.assertFalse(self.valid())

    def test_null_source(self):
        self.data['sources']=[None]
        self.assertFalse(self.valid())

    def test_empty_source(self):
        self.data['sources']=['']
        self.assertFalse(self.valid())

    def test_invalid_top(self):
        self.data['top']='a.b'
        self.assertFalse(self.valid())

    def test_newline_top(self):
        self.data['top']='top\n'
        self.assertFalse(self.valid())

    def test_null_top(self):
        self.data['top']=None
        self.assertFalse(self.valid())

    def test_null_assembly(self):
        self.data['assembly']=None
        self.assertFalse(self.valid())

    def test_empty_host(self):
        self.data['nativeHost']=''
        self.assertFalse(self.valid())

    def test_blank_bootstrap(self):
        self.data['icarusBootstrap']=' '
        self.assertFalse(self.valid())

    def test_control_library(self):
        self.data['gpiLibrary']='a\x00'
        self.assertFalse(self.valid())

    def test_empty_gpi_impl(self):
        self.data['gpiImpl']=''
        self.assertFalse(self.valid())

    def test_invalid_symbol(self):
        self.data['gpiEntryPoint']='bad,symbol'
        self.assertFalse(self.valid())

    def test_valid_manifest(self):
        self.assertTrue(self.valid())
