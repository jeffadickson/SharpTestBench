from pathlib import Path
import sys
import unittest
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tests/Tooling/managed_compat'))
from lfsr_fixture import config,plan,validate_plan,metrics,verify_sources

class Tests(unittest.TestCase):
    def sample(self):
        p=plan(config(),1,1)
        return p,dict(vectors=len(p['Vectors']),trace=[[i,r['State'],r['Data'],r['ExpectedState'],r['ExpectedData']] for i,r in enumerate(p['Vectors'])])
    def bad_plan(self,edit):
        p,_=self.sample();edit(p)
        with self.assertRaises(ValueError):validate_plan(p)
    def bad_metrics(self,edit):
        p,m=self.sample();edit(m)
        with self.assertRaises(ValueError):metrics(m,p)

    def test_missing_plan_config(self):
        self.bad_plan(lambda p:p.pop('Config'))

    def test_missing_plan_seed(self):
        self.bad_plan(lambda p:p.pop('Seed'))

    def test_missing_plan_vectors(self):
        self.bad_plan(lambda p:p.pop('Vectors'))

    def test_missing_config_width(self):
        self.bad_plan(lambda p:p["Config"].pop('Width'))

    def test_missing_config_polynomial(self):
        self.bad_plan(lambda p:p["Config"].pop('Polynomial'))

    def test_missing_config_datawidth(self):
        self.bad_plan(lambda p:p["Config"].pop('DataWidth'))

    def test_missing_config_topology(self):
        self.bad_plan(lambda p:p["Config"].pop('Topology'))

    def test_missing_config_reverse(self):
        self.bad_plan(lambda p:p["Config"].pop('Reverse'))

    def test_missing_config_feedforward(self):
        self.bad_plan(lambda p:p["Config"].pop('FeedForward'))

    def test_missing_vector_state(self):
        self.bad_plan(lambda p:p["Vectors"][0].pop('State'))

    def test_bad_vector_state_neg1(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(State=-1))

    def test_bad_vector_state_True(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(State=True))

    def test_bad_vector_state_18446744073709551616(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(State=18446744073709551616))

    def test_missing_vector_data(self):
        self.bad_plan(lambda p:p["Vectors"][0].pop('Data'))

    def test_bad_vector_data_neg1(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(Data=-1))

    def test_bad_vector_data_True(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(Data=True))

    def test_bad_vector_data_18446744073709551616(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(Data=18446744073709551616))

    def test_missing_vector_expectedstate(self):
        self.bad_plan(lambda p:p["Vectors"][0].pop('ExpectedState'))

    def test_bad_vector_expectedstate_neg1(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(ExpectedState=-1))

    def test_bad_vector_expectedstate_True(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(ExpectedState=True))

    def test_bad_vector_expectedstate_18446744073709551616(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(ExpectedState=18446744073709551616))

    def test_missing_vector_expecteddata(self):
        self.bad_plan(lambda p:p["Vectors"][0].pop('ExpectedData'))

    def test_bad_vector_expecteddata_neg1(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(ExpectedData=-1))

    def test_bad_vector_expecteddata_True(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(ExpectedData=True))

    def test_bad_vector_expecteddata_18446744073709551616(self):
        self.bad_plan(lambda p:p["Vectors"][0].update(ExpectedData=18446744073709551616))

    def test_bad_vectors_0(self):
        self.bad_plan(lambda p:p.update(Vectors=[]))

    def test_bad_vectors_1(self):
        self.bad_plan(lambda p:p.update(Vectors={}))

    def test_bad_vectors_2(self):
        self.bad_plan(lambda p:p.update(Vectors=None))

    def test_bad_vectors_3(self):
        self.bad_plan(lambda p:p.update(Vectors=True))

    def test_wrong_trace_column_0(self):
        self.bad_metrics(lambda m:m["trace"][0].__setitem__(0,1))

    def test_wrong_trace_column_1(self):
        self.bad_metrics(lambda m:m["trace"][0].__setitem__(1,1))

    def test_wrong_trace_column_2(self):
        self.bad_metrics(lambda m:m["trace"][0].__setitem__(2,1))

    def test_wrong_trace_column_3(self):
        self.bad_metrics(lambda m:m["trace"][0].__setitem__(3,1))

    def test_wrong_trace_column_4(self):
        self.bad_metrics(lambda m:m["trace"][0].__setitem__(4,1))

    def test_missing_metric_trace(self):
        self.bad_metrics(lambda m:m.pop('trace'))

    def test_missing_metric_vectors(self):
        self.bad_metrics(lambda m:m.pop('vectors'))

    def test_bad_metric_count_0(self):
        self.bad_metrics(lambda m:m.update(vectors=-1))

    def test_bad_metric_count_1(self):
        self.bad_metrics(lambda m:m.update(vectors=True))

    def test_bad_metric_count_2(self):
        self.bad_metrics(lambda m:m.update(vectors=1.5))

    def test_bad_metric_count_3(self):
        self.bad_metrics(lambda m:m.update(vectors=None))

    def test_valid_source(self):
        self.assertEqual("MIT",verify_sources(ROOT)["license"])

    def test_valid_trace(self):
        p,m=self.sample()
        self.assertEqual(m,metrics(m,p))

    def test_truncated_trace(self):
        self.bad_metrics(lambda m:m["trace"].pop())

    def test_extra_trace(self):
        self.bad_metrics(lambda m:m["trace"].append(m["trace"][-1]))
