import copy
from pathlib import Path
import sys
import unittest
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools'))
from axis_fixture import metrics

class Tests(unittest.TestCase):
    def sample(self):
        return dict(trace=[[0,1,0,0,0,0,0,0,0,0,0],
                           [1,0,1,1,1,123,3,1,4,5,2],
                           [2,0,0,0,1,0,0,0,0,0,0]],
                    accepted=1,consumed=1,flushed=0,stalls=0,resets=1,cycles=3)
    def bad(self,edit):
        data=self.sample(); edit(data)
        with self.assertRaises(ValueError): metrics(data)

    def test_valid_evidence(self):
        data=self.sample()
        self.assertEqual(data,metrics(data))

    def test_missing_trace(self):
        self.bad(lambda data:data.pop('trace'))

    def test_missing_accepted(self):
        self.bad(lambda data:data.pop('accepted'))

    def test_missing_consumed(self):
        self.bad(lambda data:data.pop('consumed'))

    def test_missing_flushed(self):
        self.bad(lambda data:data.pop('flushed'))

    def test_missing_stalls(self):
        self.bad(lambda data:data.pop('stalls'))

    def test_missing_resets(self):
        self.bad(lambda data:data.pop('resets'))

    def test_missing_cycles(self):
        self.bad(lambda data:data.pop('cycles'))

    def test_type_accepted_neg1(self):
        self.bad(lambda data:data.update(accepted=-1))

    def test_type_accepted_True(self):
        self.bad(lambda data:data.update(accepted=True))

    def test_type_accepted_1_5(self):
        self.bad(lambda data:data.update(accepted=1.5))

    def test_type_consumed_neg1(self):
        self.bad(lambda data:data.update(consumed=-1))

    def test_type_consumed_True(self):
        self.bad(lambda data:data.update(consumed=True))

    def test_type_consumed_1_5(self):
        self.bad(lambda data:data.update(consumed=1.5))

    def test_type_flushed_neg1(self):
        self.bad(lambda data:data.update(flushed=-1))

    def test_type_flushed_True(self):
        self.bad(lambda data:data.update(flushed=True))

    def test_type_flushed_1_5(self):
        self.bad(lambda data:data.update(flushed=1.5))

    def test_type_stalls_neg1(self):
        self.bad(lambda data:data.update(stalls=-1))

    def test_type_stalls_True(self):
        self.bad(lambda data:data.update(stalls=True))

    def test_type_stalls_1_5(self):
        self.bad(lambda data:data.update(stalls=1.5))

    def test_type_resets_neg1(self):
        self.bad(lambda data:data.update(resets=-1))

    def test_type_resets_True(self):
        self.bad(lambda data:data.update(resets=True))

    def test_type_resets_1_5(self):
        self.bad(lambda data:data.update(resets=1.5))

    def test_type_cycles_neg1(self):
        self.bad(lambda data:data.update(cycles=-1))

    def test_type_cycles_True(self):
        self.bad(lambda data:data.update(cycles=True))

    def test_type_cycles_1_5(self):
        self.bad(lambda data:data.update(cycles=1.5))

    def test_column_0(self):
        self.bad(lambda data:data["trace"][1].__setitem__(0,4110))

    def test_column_1(self):
        self.bad(lambda data:data["trace"][1].__setitem__(1,2))

    def test_column_2(self):
        self.bad(lambda data:data["trace"][1].__setitem__(2,2))

    def test_column_3(self):
        self.bad(lambda data:data["trace"][1].__setitem__(3,2))

    def test_column_4(self):
        self.bad(lambda data:data["trace"][1].__setitem__(4,2))

    def test_column_5(self):
        self.bad(lambda data:data["trace"][1].__setitem__(5,65536))

    def test_column_6(self):
        self.bad(lambda data:data["trace"][1].__setitem__(6,4))

    def test_column_7(self):
        self.bad(lambda data:data["trace"][1].__setitem__(7,2))

    def test_column_8(self):
        self.bad(lambda data:data["trace"][1].__setitem__(8,16))

    def test_column_9(self):
        self.bad(lambda data:data["trace"][1].__setitem__(9,16))

    def test_column_10(self):
        self.bad(lambda data:data["trace"][1].__setitem__(10,4))

    def test_counter_accepted(self):
        self.bad(lambda data:data.update(accepted=99))

    def test_counter_consumed(self):
        self.bad(lambda data:data.update(consumed=99))

    def test_counter_flushed(self):
        self.bad(lambda data:data.update(flushed=99))

    def test_counter_stalls(self):
        self.bad(lambda data:data.update(stalls=99))

    def test_counter_resets(self):
        self.bad(lambda data:data.update(resets=99))

    def test_counter_cycles(self):
        self.bad(lambda data:data.update(cycles=99))

    def test_reset_transfers(self):
        self.bad(lambda data:data["trace"][0].__setitem__(2,1))

    def test_empty_metrics(self):
        with self.assertRaises(ValueError): metrics({})
