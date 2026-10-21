import copy
import itertools
import unittest
from arbiter_reference import reference,validate
from test_fifo_contract import call


class Tests(unittest.TestCase):
    def sample(self):
        c=dict(Ports=3,RoundRobin=True,Blocking=2,LsbPriority=False)
        p=call('arbiter_fixture','plan',[c,83,17]);return c,p,reference(c,p)

    def reject_both(self,c,p,m):
        with self.assertRaises(ValueError):validate(c,p,m)
        with self.assertRaises(ValueError):call('arbiter_fixture','metrics',[c,m,p])

    def test_all_48_policy_configurations_agree_with_independent_reference(self):
        for ports,rr,block,lsb in itertools.product([2,3,4,8],[False,True],[0,1,2],[False,True]):
            c=dict(Ports=ports,RoundRobin=rr,Blocking=block,LsbPriority=lsb)
            with self.subTest(configuration=c):
                p=call('arbiter_fixture','plan',[c,2**64-1,17]);m=reference(c,p)
                self.assertTrue(validate(c,p,m)['valid']);self.assertEqual(m,call('arbiter_fixture','metrics',[c,m,p]))

    def test_matching_wrong_grant_cannot_bypass_either_policy_model(self):
        c,p,m=self.sample();m['trace'][1][4]^=1;self.reject_both(c,p,m)

    def test_encoded_grant_must_match_the_selected_port(self):
        c,p,m=self.sample();m['trace'][1][6]=0;self.reject_both(c,p,m)

    def test_requested_controls_are_bound_not_just_runner_agreement(self):
        c,p,m=self.sample();p[1]['Acknowledge']^=1;self.reject_both(c,p,m)

    def test_forged_counters_and_boolean_observations_fail_both_models(self):
        for mode in ['counter','boolean']:
            c,p,m=self.sample()
            if mode=='counter':m['transitions']+=1
            else:m['trace'][1][5]=True
            self.reject_both(c,p,m)

    def test_missing_boundary_reset_is_not_a_complete_workload(self):
        for boundary in [0,-1]:
            c,p,m=self.sample();p[boundary]['Reset']=False;self.reject_both(c,p,m)

    def test_invalid_policy_types_and_bounds_reject_before_generation(self):
        c,_,_=self.sample()
        for key,value in [('Ports',5),('Ports',True),('Blocking',3),('Blocking',False),('RoundRobin','true'),('LsbPriority',1)]:
            bad=dict(c);bad[key]=value
            with self.assertRaises(ValueError):reference(bad,[])
            with self.assertRaises(ValueError):call('arbiter_fixture','plan',[bad,83,17])

    def test_missing_or_extra_metric_fields_fail_both_models(self):
        c,p,m=self.sample()
        for bad in [{k:v for k,v in m.items() if k!='idle'},dict(m,unexpected=0)]:self.reject_both(c,p,bad)

    def test_ambient_arbitration_controls_are_removed_from_ordinary_children(self):
        poisoned={key:'poison' for key in ['ARBITER_PLAN','ARBITER_CONFIG','ARBITER_REPORT','ARBITER_FAULT']}
        self.assertEqual({'PATH':'keep'},call('dual_runner_ci','clean_environment',[dict(poisoned,PATH='keep')]))
