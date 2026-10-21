"""Independent replay of complete good/bad frames and completion pulses."""
from pathlib import Path
import sys
import unittest
from frame_drop_reference import validate
sys.path.insert(0,str(Path(__file__).resolve().parent/'managed_compat'))
from managed_rpc import call


def sample():
    config=dict(Depth=4,Pipeline=0,OutputFifo=False)
    def word(data,last,user):return dict(Data=data,Keep=3,Last=last,Id=1,Dest=2,User=user)
    good=[word(41,False,1),word(83,True,2)]
    bad=[word(99,False,0),word(100,True,1)]
    plan=dict(Frames=[good,bad],Ready=[False]+[True]*99)
    trace=[[i,int(i==0),int(i in (1,2,3,4)),int(i in (3,4)),int(i>0),0,0,0,0,0,0,int(i==5),int(i==3)] for i in range(100)]
    for cycle,item in [(3,good[0]),(4,good[1])]:trace[cycle][5:11]=[item['Data'],item['Keep'],int(item['Last']),item['Id'],item['Dest'],item['User']]
    metrics=dict(trace=trace,accepted=4,consumed=2,discarded=2,badFrames=1,goodFrames=1,stalls=0,resets=1,cycles=100)
    return config,plan,metrics


class Tests(unittest.TestCase):
    def reject_both(self,config,plan,metrics):
        with self.assertRaises(ValueError):validate(config,plan,metrics)
        with self.assertRaises(ValueError):call('frame_drop_fixture','metrics',[config,metrics,plan])

    def test_hand_derived_mixed_good_and_bad_frames_agree(self):
        config,plan,metrics=sample();a=validate(config,plan,metrics)
        b=call('frame_drop_fixture','metrics',[config,metrics,plan])
        self.assertEqual(1,a['badFrames']);self.assertEqual(2,a['discarded'])
        self.assertEqual(a['incompleteHidden'],b['incompleteHidden'])

    def test_bad_frame_payload_cannot_leak_after_drop_pulse(self):
        config,plan,metrics=sample();row=metrics['trace'][5];row[3]=1;row[5:11]=[99,3,0,1,2,0]
        self.reject_both(config,plan,metrics)

    def test_early_missing_and_stretched_status_pulses_reject(self):
        for cycle,field,value in [(4,11,1),(5,11,0),(6,11,1),(3,12,0),(4,12,1)]:
            config,plan,metrics=sample();metrics['trace'][cycle][field]=value;self.reject_both(config,plan,metrics)

    def test_every_good_output_sideband_remains_checked(self):
        for field in range(5,11):
            config,plan,metrics=sample();metrics['trace'][3][field]^=1;self.reject_both(config,plan,metrics)

    def test_drop_and_frame_counters_cannot_be_forged(self):
        for key in ['accepted','consumed','discarded','badFrames','goodFrames','stalls','resets','cycles']:
            config,plan,metrics=sample();metrics[key]+=1;self.reject_both(config,plan,metrics)

    def test_boolean_float_and_extra_field_encodings_reject(self):
        for value in [True,1.0]:
            config,plan,metrics=sample();metrics['trace'][5][11]=value;self.reject_both(config,plan,metrics)
        config,plan,metrics=sample();metrics['unrequested']=0;self.reject_both(config,plan,metrics)

    def test_drop_generator_has_both_classes_at_seed_extremes(self):
        for seed in [0,17,2**64-1]:
            config,_,_=sample();plan=call('frame_drop_fixture','plan',[config,seed,16])
            self.assertEqual(8,sum(frame[-1]['User']&1 for frame in plan['Frames']))
            self.assertTrue(all(plan['Ready'][-84:]));self.assertFalse(plan['Ready'][0])
