import copy
import unittest
from arbiter_reference import reference,validate


def config(**changes):
    value=dict(Ports=3,RoundRobin=True,Blocking=0,LsbPriority=True);value.update(changes);return value


def plan(*pairs):
    return [dict(Reset=True,Request=0,Acknowledge=0)]+[dict(Reset=False,Request=r,Acknowledge=a) for r,a in pairs]+[dict(Reset=True,Request=0,Acknowledge=0)]


class Tests(unittest.TestCase):
    def test_round_robin_rotates_non_power_of_two_port_sets(self):
        c=config();p=plan(*([(7,7)]*5));m=reference(c,p)
        self.assertEqual([0,1,2,4,1,2,0],[r[4] for r in m['trace']]);self.assertTrue(validate(c,p,m)['valid'])

    def test_msb_priority_and_encoded_grant_are_explicit(self):
        c=config(RoundRobin=False,LsbPriority=False);p=plan((5,0),(2,0));m=reference(c,p)
        self.assertEqual([0,4,2,0],[r[4] for r in m['trace']]);self.assertEqual([0,2,1,0],[r[6] for r in m['trace']])

    def test_acknowledgement_block_holds_even_after_request_withdrawal(self):
        c=config(Ports=4,Blocking=2);p=plan((8,0),(1,0),(1,8));m=reference(c,p)
        self.assertEqual([0,8,8,1,0],[r[4] for r in m['trace']])

    def test_request_block_ignores_acknowledgement_until_request_drops(self):
        c=config(Ports=4,Blocking=1);p=plan((8,0),(9,8),(1,0));m=reference(c,p)
        self.assertEqual([0,8,8,1,0],[r[4] for r in m['trace']])

    def test_idle_cycle_does_not_reset_round_robin_cursor(self):
        c=config();p=plan((7,0),(0,0),(7,0));m=reference(c,p)
        self.assertEqual([0,1,0,2,0],[r[4] for r in m['trace']])

    def test_same_wrong_grant_and_forged_counters_cannot_qualify(self):
        c=config();p=plan((7,0));m=reference(c,p)
        for change in ['grant','counter']:
            bad=copy.deepcopy(m)
            if change=='grant':bad['trace'][1][4]=2
            else:bad['valid']+=1
            with self.assertRaises(ValueError):validate(c,p,bad)

    def test_boolean_numeric_observation_is_not_a_trace_integer(self):
        c=config();p=plan((7,0));m=reference(c,p);m['trace'][1][5]=True
        with self.assertRaises(ValueError):validate(c,p,m)

    def test_reset_restarts_rotation_after_a_held_grant(self):
        c=config(Blocking=2);p=plan((7,0),(7,0));p.insert(2,dict(Reset=True,Request=0,Acknowledge=0));m=reference(c,p)
        self.assertEqual([0,1,0,1,0],[r[4] for r in m['trace']])
