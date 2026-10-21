import unittest
from fifo_reference import validate


def row(**changes):
    value = dict(Reset=False, Ready=True, Offer=False, Data=41, Keep=2, Last=False, Id=3, Dest=4, User=2)
    value.update(changes)
    return value


def sample():
    plan = [row(Reset=True, Ready=False), row(Offer=True), row()]
    metrics = dict(trace=[[0,1,0,0,0,0,0,0,0,0,0], [1,0,1,0,1,0,0,0,0,0,0], [2,0,0,1,1,41,2,0,3,4,2]],
                   accepted=1, consumed=1, flushed=0, stalls=0, resets=1, cycles=3)
    return plan, metrics


class Tests(unittest.TestCase):
    def test_accepted_fifo_beat_round_trip(self):
        plan, metrics = sample()
        self.assertTrue(validate(plan, metrics)['valid'])

    def test_matching_wrong_payload_is_not_an_oracle(self):
        plan, metrics = sample()
        metrics['trace'][2][5] ^= 1
        with self.assertRaises(ValueError):
            validate(plan, metrics)

    def test_disabled_sidebands_have_explicit_projection(self):
        plan, metrics = sample()
        metrics['trace'][2][6:] = [3,1,0,0,0]
        self.assertTrue(validate(plan, metrics, False)['valid'])
        with self.assertRaises(ValueError):
            validate(plan, metrics, True)

    def test_source_pending_survives_a_backpressured_offer(self):
        plan, metrics = sample()
        plan.insert(2, row(Offer=True, Data=99))
        metrics['trace'].insert(1, [1,0,0,0,1,0,0,0,0,0,0])
        for index, trace in enumerate(metrics['trace']):
            trace[0] = index
        metrics['cycles'] = 4
        self.assertTrue(validate(plan, metrics)['valid'])

    def test_trace_controls_cannot_use_a_different_plan(self):
        plan, metrics = sample()
        plan[1]['Ready'] = False
        with self.assertRaises(ValueError):
            validate(plan, metrics)

    def test_balanced_counters_cannot_hide_missing_source_offer(self):
        plan, metrics = sample()
        plan[1]['Offer'] = False
        with self.assertRaises(ValueError):
            validate(plan, metrics)

    def test_boolean_counters_do_not_count_as_integers(self):
        plan, metrics = sample()
        metrics['accepted'] = True
        with self.assertRaises(ValueError):
            validate(plan, metrics)

    def test_counter_claims_must_match_observed_transfers(self):
        plan, metrics = sample()
        metrics['flushed'] = 1
        with self.assertRaises(ValueError):
            validate(plan, metrics)
