"""Independent Python frame oracle cross-checks production C# replay."""
import copy
from pathlib import Path
import sys
import unittest
from frame_reference import validate
sys.path.insert(0,str(Path(__file__).resolve().parent/'managed_compat'))
from managed_rpc import call


def sample():
    config=dict(Depth=4,Pipeline=0,OutputFifo=False)
    first=dict(Data=41,Keep=3,Last=False,Id=1,Dest=2,User=0)
    last=dict(Data=83,Keep=1,Last=True,Id=1,Dest=2,User=3)
    plan=dict(Frames=[[first,last]],Ready=[False]+[True]*99)
    trace=[[i,int(i==0),int(i in (1,2)),int(i in (3,4)),int(i>0),0,0,0,0,0,0] for i in range(100)]
    for cycle,word in [(3,first),(4,last)]:trace[cycle][5:]=[word['Data'],word['Keep'],int(word['Last']),word['Id'],word['Dest'],word['User']]
    metrics=dict(trace=trace,accepted=2,consumed=2,flushed=0,stalls=0,resets=1,cycles=100)
    return config,plan,metrics


class Tests(unittest.TestCase):
    def reject_both(self,config,plan,metrics):
        with self.assertRaises(ValueError):validate(config,plan,metrics)
        with self.assertRaises(ValueError):call('frame_fixture','metrics',[config,metrics,plan])

    def test_hand_derived_complete_frame_is_accepted_by_both_replays(self):
        config,plan,metrics=sample()
        self.assertEqual(1,validate(config,plan,metrics)['incompleteHidden'])
        self.assertEqual(1,call('frame_fixture','metrics',[config,metrics,plan]))

    def test_last_handshake_cycle_is_still_too_early_for_output(self):
        config,plan,metrics=sample()
        metrics['trace'][2][3]=1;metrics['trace'][2][5:]=metrics['trace'][3][5:]
        metrics['trace'][3][5:]=metrics['trace'][4][5:];metrics['trace'][4][3]=0;metrics['trace'][4][5:]=[0]*6
        self.reject_both(config,plan,metrics)

    def test_each_payload_sideband_and_last_corruption_is_rejected(self):
        for field in range(5,11):
            config,plan,metrics=sample();metrics['trace'][3][field]^=1
            self.reject_both(config,plan,metrics)

    def test_false_counters_cannot_qualify_matching_trace(self):
        for field in ['accepted','consumed','flushed','stalls','resets','cycles']:
            config,plan,metrics=sample();metrics[field]+=1;self.reject_both(config,plan,metrics)

    def test_unbounded_frame_and_missing_last_are_rejected(self):
        config,plan,metrics=sample();plan['Frames'][0][-1]['Last']=False;self.reject_both(config,plan,metrics)
        config,plan,metrics=sample();plan['Frames'][0]=[plan['Frames'][0][0]]*3+[plan['Frames'][0][-1]];self.reject_both(config,plan,metrics)

    def test_float_boolean_and_extra_field_encodings_are_rejected(self):
        for value in [True,41.0]:
            config,plan,metrics=sample();metrics['trace'][3][5]=value;self.reject_both(config,plan,metrics)
        config,plan,metrics=sample();plan['unexpected']=True;self.reject_both(config,plan,metrics)

    def test_generator_is_deterministic_with_drain_for_seed_extremes(self):
        for seed in [0,17,2**64-1]:
            config,_,_=sample();plan=call('frame_fixture','plan',[config,seed,16])
            self.assertEqual(plan,call('frame_fixture','plan',[config,seed,16]))
            self.assertTrue(all(plan['Ready'][-84:]));self.assertFalse(plan['Ready'][0])
            self.assertTrue(all(frame[-1]['Last'] for frame in plan['Frames']))
