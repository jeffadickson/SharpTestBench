"""Hand-authored transfer histories check FIFO ownership across boundary events."""
import copy
import unittest
from test_fifo_contract import sample, call
from fifo_reference import validate


WORD_A = [41, 2, 0, 3, 4, 2]
WORD_B = [99, 1, 1, 5, 6, 1]
WORD_C = [7, 3, 0, 8, 9, 0]


def history(sidebands=True):
    config, plan, metrics = sample(sidebands=sidebands)
    # Reserve ten active cycles and the required idle/draining tail.
    idle = dict(Reset=False, Ready=True, Offer=False, Data=0, Keep=0, Last=False, Id=0, Dest=0, User=0)
    plan = [dict(idle, Reset=True, Ready=False)] + [dict(idle) for _ in range(88)]
    metrics = dict(trace=[[i, int(p['Reset']), 0, 0, int(p['Ready']), *([0]*6)] for i,p in enumerate(plan)],
                   accepted=0, consumed=0, flushed=0, stalls=0, resets=1, cycles=len(plan))
    return config, plan, metrics


def event(plan, metrics, cycle, *, offer=None, take=False, output=None, ready=True, reset=False):
    stimulus = plan[cycle]
    stimulus.update(Ready=ready, Reset=reset, Offer=offer is not None)
    if offer is not None:
        stimulus.update(Data=offer[0], Keep=offer[1], Last=bool(offer[2]), Id=offer[3], Dest=offer[4], User=offer[5])
    metrics['trace'][cycle] = [cycle,int(reset),int(take),int(output is not None),int(ready),*(output or [0]*6)]


class Tests(unittest.TestCase):
    def accept_both(self, config, plan, metrics):
        self.assertTrue(validate(plan,metrics,config['Sidebands'])['valid'])
        self.assertEqual(metrics,call('fifo_fixture','metrics',[config,metrics,plan]))

    def reject_both(self, config, plan, metrics):
        with self.assertRaises(ValueError): validate(plan,metrics,config['Sidebands'])
        with self.assertRaises(ValueError): call('fifo_fixture','metrics',[config,metrics,plan])

    def test_saturation_witness_requires_full_backlog_and_blocked_source(self):
        c,p,m=history()
        event(p,m,1,offer=WORD_A,take=True,ready=False)
        event(p,m,2,offer=WORD_B,take=True,ready=False)
        event(p,m,3,offer=WORD_C,output=WORD_A,ready=False)
        event(p,m,4,output=WORD_A)
        event(p,m,5,take=True,output=WORD_B)
        event(p,m,6,output=WORD_C)
        m.update(accepted=3,consumed=3,stalls=1)
        self.accept_both(c,p,m)
        self.assertEqual(dict(maximumBacklog=2,blockedSourceCycles=2,blockedAtDepthCycles=1,simultaneousTransfers=1),
                         call('fifo_fixture','saturation_witness',[c,m,p]))

    def test_unsaturated_passing_stream_does_not_claim_capacity_coverage(self):
        c,p,m=sample()
        self.accept_both(c,p,m)
        with self.assertRaisesRegex(ValueError,'not witnessed'):
            call('fifo_fixture','saturation_witness',[c,m,p])

        # Startup-only blocking cannot be combined with a later full queue.
        c,p,m=history()
        event(p,m,1,offer=WORD_A,ready=False)
        event(p,m,2,take=True,ready=False)
        event(p,m,3,offer=WORD_B,take=True,ready=False)
        event(p,m,4,output=WORD_A)
        event(p,m,5,output=WORD_B)
        m.update(accepted=2,consumed=2)
        self.accept_both(c,p,m)
        with self.assertRaisesRegex(ValueError,'not witnessed'):
            call('fifo_fixture','saturation_witness',[c,m,p])

    def test_reset_discards_unaccepted_pending_source_without_flushed_count(self):
        c,p,m=history()
        event(p,m,1,offer=WORD_A,ready=False)
        event(p,m,2,reset=True)
        event(p,m,3,offer=WORD_B,take=True)
        event(p,m,4,output=WORD_B)
        m.update(accepted=1,consumed=1,resets=2)
        self.accept_both(c,p,m)
        forged=copy.deepcopy(m);forged['trace'][4][5:]=WORD_A
        self.reject_both(c,p,forged)

    def test_reset_releases_held_output_and_accounts_only_accepted_backlog(self):
        c,p,m=history()
        event(p,m,1,offer=WORD_A,take=True,ready=False)
        event(p,m,2,output=WORD_A,ready=False)
        event(p,m,3,reset=True)
        event(p,m,4,offer=WORD_B,take=True)
        event(p,m,5,output=WORD_B)
        m.update(accepted=2,consumed=1,flushed=1,stalls=1,resets=2)
        self.accept_both(c,p,m)
        forged=copy.deepcopy(m);forged['flushed']=0
        self.reject_both(c,p,forged)

    def test_simultaneous_input_and_output_preserves_old_queue_head(self):
        c,p,m=history()
        event(p,m,1,offer=WORD_A,take=True)
        event(p,m,2,offer=WORD_B,take=True,output=WORD_A)
        event(p,m,3,output=WORD_B)
        m.update(accepted=2,consumed=2)
        self.accept_both(c,p,m)
        forged=copy.deepcopy(m);forged['trace'][2][5:]=WORD_B
        self.reject_both(c,p,forged)

    def test_balanced_two_beat_reordering_fails_even_when_both_runners_agree(self):
        c,p,m=history()
        event(p,m,1,offer=WORD_A,take=True,ready=False)
        event(p,m,2,offer=WORD_B,take=True,ready=False)
        event(p,m,3,output=WORD_A)
        event(p,m,4,output=WORD_B)
        m.update(accepted=2,consumed=2)
        self.accept_both(c,p,m)
        m['trace'][3][5:],m['trace'][4][5:]=m['trace'][4][5:],m['trace'][3][5:]
        self.reject_both(c,p,m)

    def test_disabled_sidebands_stay_projected_through_output_stall(self):
        c,p,m=history(False);projected=[WORD_A[0],3,1,0,0,0]
        event(p,m,1,offer=WORD_A,take=True,ready=False)
        event(p,m,2,output=projected,ready=False)
        event(p,m,3,output=projected)
        m.update(accepted=1,consumed=1,stalls=1)
        self.accept_both(c,p,m)
        m['trace'][3][6]=WORD_A[1]
        self.reject_both(c,p,m)

    def test_new_offer_cannot_replace_a_pending_backpressured_source(self):
        c,p,m=history()
        event(p,m,1,offer=WORD_A,ready=False)
        event(p,m,2,offer=WORD_B,take=True)
        event(p,m,3,output=WORD_A)
        m.update(accepted=1,consumed=1)
        self.accept_both(c,p,m)
        m['trace'][3][5:]=WORD_B
        self.reject_both(c,p,m)
