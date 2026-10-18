import json
from pathlib import Path
import subprocess
import unittest
ROOT=Path(__file__).resolve().parents[2]

class Tests(unittest.TestCase):
    def plan(self, **options):
        data={'schemaVersion':1,'simulator':'icarus','top':'top','sources':['top.sv'],'assembly':'a.dll','nativeHost':'host.so',
              'icarusBootstrap':'boot.vpi','gpiLibrary':'gpi.so','gpiImpl':'impl','dotnetRoot':'sdk','outputDirectory':'out','expectedTests':1}
        data.update(options)
        result=subprocess.run(['dotnet',str(ROOT/'tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll'),'plan'],input=json.dumps(data),text=True,capture_output=True,check=True)
        return json.loads(result.stdout)

    def test_default_standard(self):
        self.assertEqual('-g2012',self.plan()['arguments'][0])

    def test_standard_1995(self):
        self.assertEqual('-g1995',self.plan(hdlStandard='1995')['arguments'][0])

    def test_standard_2001(self):
        self.assertEqual('-g2001',self.plan(hdlStandard='2001')['arguments'][0])

    def test_standard_2005(self):
        self.assertEqual('-g2005',self.plan(hdlStandard='2005')['arguments'][0])

    def test_standard_2005_sv(self):
        self.assertEqual('-g2005-sv',self.plan(hdlStandard='2005-sv')['arguments'][0])

    def test_standard_2012(self):
        self.assertEqual('-g2012',self.plan(hdlStandard='2012')['arguments'][0])

    def test_unknown_standard(self):
        self.assertFalse(self.plan(**{'hdlStandard': 'future'})["valid"])

    def test_null_standard(self):
        self.assertFalse(self.plan(**{'hdlStandard': None})["valid"])

    def test_blank_include(self):
        self.assertFalse(self.plan(**{'includeDirectories': ['']})["valid"])

    def test_null_include(self):
        self.assertFalse(self.plan(**{'includeDirectories': [None]})["valid"])

    def test_invalid_define(self):
        self.assertFalse(self.plan(**{'defines': {'a-b': '1'}})["valid"])

    def test_control_define(self):
        self.assertFalse(self.plan(**{'defines': {'A': 'a\n'}})["valid"])

    def test_blank_parameter(self):
        self.assertFalse(self.plan(**{'parameters': {'WIDTH': ''}})["valid"])

    def test_null_parameter(self):
        self.assertFalse(self.plan(**{'parameters': {'WIDTH': None}})["valid"])

    def test_invalid_parameter(self):
        self.assertFalse(self.plan(**{'parameters': {'a.b': '1'}})["valid"])

    def test_duplicate_sources(self):
        self.assertFalse(self.plan(**{'sources': ['top.sv', './top.sv']})["valid"])

    def test_empty_selection(self):
        self.assertFalse(self.plan(**{'testNames': []})["valid"])

    def test_unattested_selection(self):
        self.assertFalse(self.plan(**{'testNames': ['Entry.a']})["valid"])

    def test_mismatched_selection(self):
        self.assertFalse(self.plan(**{'testNames': ['Entry.a'], 'expectedNames': ['Entry.b']})["valid"])

    def test_duplicate_selection(self):
        self.assertFalse(self.plan(**{'testNames': ['Entry.a', 'Entry.a'], 'expectedNames': ['Entry.a']})["valid"])

    def test_ordered_includes(self):
        args=self.plan(includeDirectories=['second dir','first dir'])['arguments']
        self.assertLess(args.index(str((Path.cwd()/'second dir').resolve())),args.index(str((Path.cwd()/'first dir').resolve())))

    def test_sorted_defines(self):
        args=self.plan(defines={'Z':None,'A':'1'})['arguments']
        self.assertLess(args.index('A=1'),args.index('Z'))

    def test_parameter_top(self):
        self.assertIn('top.WIDTH=8',self.plan(parameters={'WIDTH':'8'})['arguments'])

    def test_selection_environment(self):
        self.assertEqual(['Entry.a'],json.loads(self.plan(testNames=['Entry.a'],expectedNames=['Entry.a'])['environment']['CS_TEST_NAMES']))

    def test_case_sensitive_definitions(self):
        args=self.plan(defines={'A':'1','a':'2'})['arguments']
        self.assertIn('A=1',args)
        self.assertIn('a=2',args)
