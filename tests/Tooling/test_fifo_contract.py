"""Independent oracle failures must also be rejected by the production C# replay."""
import copy
from pathlib import Path
import sys
import unittest
from fifo_reference import validate
sys.path.insert(0, str(Path(__file__).resolve().parent / 'managed_compat'))
from managed_rpc import call


def sample(seed=17, sidebands=True, count=1):
    config = dict(Depth=2, Pipeline=0, OutputFifo=False, Sidebands=sidebands)
    plan = call('fifo_fixture','plan',[config,seed,count,0,None])
    source = plan[1]
    word = [source['Data'],source['Keep'] if sidebands else 3,int(source['Last']) if sidebands else 1,
            source['Id'] if sidebands else 0,source['Dest'] if sidebands else 0,source['User'] if sidebands else 0]
    trace = [[i,int(row['Reset']),int(i==1),int(i==2),int(row['Ready']),*(word if i==2 else [0]*6)] for i,row in enumerate(plan)]
    metrics = dict(trace=trace,accepted=1,consumed=1,flushed=0,stalls=0,resets=1,cycles=len(plan))
    return config, plan, metrics


class Tests(unittest.TestCase):
    def reject_both(self, config, plan, metrics):
        with self.assertRaises(ValueError): validate(plan,metrics,config['Sidebands'])
        with self.assertRaises(ValueError): call('fifo_fixture','metrics',[config,metrics,plan])

    def test_generated_plans_agree_with_independent_replay(self):
        for seed in [0,17,2**64-1]:
            for sidebands in [False,True]:
                with self.subTest(seed=seed,sidebands=sidebands):
                    config,plan,metrics=sample(seed,sidebands)
                    self.assertTrue(validate(plan,metrics,sidebands)['valid'])
                    self.assertEqual(metrics,call('fifo_fixture','metrics',[config,metrics,plan]))

    def test_matching_wrong_payload_fails_both_models(self):
        config,plan,metrics=sample();metrics['trace'][2][5]^=1
        self.reject_both(config,plan,metrics)

    def test_different_requested_ready_control_fails_both_models(self):
        config,plan,metrics=sample();plan[1]['Ready']=False
        self.reject_both(config,plan,metrics)

    def test_consistent_rows_cannot_support_forged_counters(self):
        config,plan,metrics=sample();metrics['accepted']+=1
        self.reject_both(config,plan,metrics)

    def test_boolean_and_float_trace_encodings_fail_both_models(self):
        for value in [True,41.0]:
            config,plan,metrics=sample();metrics['trace'][2][5]=value
            self.reject_both(config,plan,metrics)

    def test_phantom_acceptance_fails_at_the_source_offer_boundary(self):
        config,plan,metrics=sample(count=2);plan[1]['Offer']=False
        with self.assertRaisesRegex(ValueError,'offer'):validate(plan,metrics)
        with self.assertRaisesRegex(ValueError,'offer'):call('fifo_fixture','metrics',[config,metrics,plan])

    def test_string_sideband_flags_are_not_booleans(self):
        config,plan,metrics=sample();config['Sidebands']='false'
        with self.assertRaises(ValueError):validate(plan,metrics,'false')
        with self.assertRaises(ValueError):call('fifo_fixture','metrics',[config,metrics,plan])
