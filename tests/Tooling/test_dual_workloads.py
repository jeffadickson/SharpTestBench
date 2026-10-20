import json
from pathlib import Path
import sys
import unittest
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tests/Tooling/managed_compat'))
import dual_runner_ci as ci

class Tests(unittest.TestCase):
    def dsp(self):
        return {'trace':[[1,1,0,0]],'cycles':2,'accepted':1,'consumed':0,'stalls':0}

    def cpu(self):
        return {'trace':[[1,2,3]],'cycles':2,'commits':1,'memoryStalls':0,'pauses':0,'memory':[0,1]}

    def failed(self):
        return [{'Name':'a','Status':'failed','Seed':1,'Error':'DSP_MISMATCH injected'}]


    def test_missing_trace(self):
        data=self.dsp()
        del data['trace']
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_missing_cycles(self):
        data=self.dsp()
        del data['cycles']
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_missing_accepted(self):
        data=self.dsp()
        del data['accepted']
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_missing_consumed(self):
        data=self.dsp()
        del data['consumed']
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_missing_stalls(self):
        data=self.dsp()
        del data['stalls']
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_trace_empty(self):
        data=self.dsp()
        data["trace"]=[]
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_trace_null(self):
        data=self.dsp()
        data["trace"]=None
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_trace_string(self):
        data=self.dsp()
        data["trace"]='trace'
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_trace_empty_row(self):
        data=self.dsp()
        data["trace"]=[[]]
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_trace_float(self):
        data=self.dsp()
        data["trace"]=[[1.0]]
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_trace_boolean(self):
        data=self.dsp()
        data["trace"]=[[True]]
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_counter_negative(self):
        data=self.dsp()
        data['stalls']=-1
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_counter_boolean(self):
        data=self.dsp()
        data['cycles']=True
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_counter_zero(self):
        data=self.dsp()
        data['cycles']=0
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_counter_float(self):
        data=self.dsp()
        data['accepted']=1.0
        with self.assertRaises(ValueError):
            ci.metrics_contract("dsp",data)

    def test_ignore_wall_time(self):
        a=self.dsp()
        b=self.dsp()
        a['seconds']=1
        b['seconds']=2
        self.assertEqual(ci.metrics_contract('dsp',a),ci.metrics_contract('dsp',b))

    def test_trace_difference(self):
        a=self.dsp()
        b=self.dsp()
        b['trace'][0][0]=2
        self.assertNotEqual(ci.metrics_contract('dsp',a),ci.metrics_contract('dsp',b))

    def test_cpu_contract(self):
        self.assertEqual(self.cpu(),ci.metrics_contract('cpu',self.cpu()))

    def test_cpu_bad_memory(self):
        data=self.cpu()
        data['memory']=[-1]
        with self.assertRaises(ValueError):
            ci.metrics_contract('cpu',data)

    def test_fault_detected(self):
        self.assertTrue(ci.fault_detected(self.failed(),1,['a'],'DSP_MISMATCH'))

    def test_fault_exit_zero(self):
        self.assertFalse(ci.fault_detected(self.failed(),0,['a'],'DSP_MISMATCH'))

    def test_fault_wrong_error(self):
        self.assertFalse(ci.fault_detected(self.failed(),1,['a'],'CPU_MISMATCH'))

    def test_fault_wrong_case(self):
        self.assertFalse(ci.fault_detected(self.failed(),1,['b'],'DSP_MISMATCH'))

    def test_fault_duplicate(self):
        self.assertFalse(ci.fault_detected(self.failed()*2,1,['a','b'],'DSP_MISMATCH'))

    def test_selection_isolation(self):
        env={'CS_TEST_FILTER':'skip','CS_TEST_NAMES':'[]','CS_LIST_TESTS':'1','CPU_FAULT':'3','DSP_PLAN':'old','PATH':'keep','DOTNET_ROOT':'sdk'}
        clean=ci.clean_environment(env)
        self.assertEqual({'PATH':'keep','DOTNET_ROOT':'sdk'},clean)
        self.assertIn('CS_TEST_FILTER',env)
