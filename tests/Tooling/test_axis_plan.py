import copy
from pathlib import Path
import sys
import unittest
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tests/Tooling/managed_compat'))
from axis_fixture import plan, validate_plan, verify_sources

class Tests(unittest.TestCase):
    def invalid(self, field, value):
        rows=plan(1); rows[2][field]=value
        with self.assertRaises(ValueError): validate_plan(rows)

    def test_deterministic_seed_0(self):
        rows=plan(0)
        self.assertEqual(rows,plan(0))
        self.assertEqual(rows,validate_plan(rows))

    def test_deterministic_seed_1(self):
        rows=plan(1)
        self.assertEqual(rows,plan(1))
        self.assertEqual(rows,validate_plan(rows))

    def test_deterministic_seed_17(self):
        rows=plan(17)
        self.assertEqual(rows,plan(17))
        self.assertEqual(rows,validate_plan(rows))

    def test_deterministic_seed_4294967296(self):
        rows=plan(4294967296)
        self.assertEqual(rows,plan(4294967296))
        self.assertEqual(rows,validate_plan(rows))

    def test_deterministic_seed_18446744073709551615(self):
        rows=plan(18446744073709551615)
        self.assertEqual(rows,plan(18446744073709551615))
        self.assertEqual(rows,validate_plan(rows))

    def test_reject_seed_0(self):
        with self.assertRaises(ValueError): plan(**{'seed': -1})

    def test_reject_seed_1(self):
        with self.assertRaises(ValueError): plan(**{'seed': 18446744073709551616})

    def test_reject_seed_2(self):
        with self.assertRaises(ValueError): plan(**{'seed': True})

    def test_reject_seed_3(self):
        with self.assertRaises(ValueError): plan(**{'seed': 1.5})

    def test_reject_seed_4(self):
        with self.assertRaises(ValueError): plan(**{'seed': '1'})

    def test_reject_seed_5(self):
        with self.assertRaises(ValueError): plan(**{'seed': None})

    def test_reject_count_0(self):
        with self.assertRaises(ValueError): plan(**{'seed': 1, 'count': 0})

    def test_reject_count_1(self):
        with self.assertRaises(ValueError): plan(**{'seed': 1, 'count': -1})

    def test_reject_count_2(self):
        with self.assertRaises(ValueError): plan(**{'seed': 1, 'count': 4097})

    def test_reject_count_3(self):
        with self.assertRaises(ValueError): plan(**{'seed': 1, 'count': True})

    def test_reject_count_4(self):
        with self.assertRaises(ValueError): plan(**{'seed': 1, 'count': 1.5})

    def test_reject_stall_0(self):
        with self.assertRaises(ValueError): plan(**{'seed': 1, 'stall': -1})

    def test_reject_stall_1(self):
        with self.assertRaises(ValueError): plan(**{'seed': 1, 'stall': 33})

    def test_reject_stall_2(self):
        with self.assertRaises(ValueError): plan(**{'seed': 1, 'stall': True})

    def test_reject_stall_3(self):
        with self.assertRaises(ValueError): plan(**{'seed': 1, 'stall': 1.5})

    def test_reject_data_0(self):
        self.invalid('Data',-1)

    def test_reject_data_1(self):
        self.invalid('Data',65536)

    def test_reject_data_2(self):
        self.invalid('Data',True)

    def test_reject_keep_0(self):
        self.invalid('Keep',-1)

    def test_reject_keep_1(self):
        self.invalid('Keep',4)

    def test_reject_keep_2(self):
        self.invalid('Keep',True)

    def test_reject_id_0(self):
        self.invalid('Id',-1)

    def test_reject_id_1(self):
        self.invalid('Id',16)

    def test_reject_id_2(self):
        self.invalid('Id',True)

    def test_reject_dest_0(self):
        self.invalid('Dest',-1)

    def test_reject_dest_1(self):
        self.invalid('Dest',16)

    def test_reject_dest_2(self):
        self.invalid('Dest',True)

    def test_reject_user_0(self):
        self.invalid('User',-1)

    def test_reject_user_1(self):
        self.invalid('User',4)

    def test_reject_user_2(self):
        self.invalid('User',True)

    def test_boolean_reset(self):
        self.invalid('Reset',1)

    def test_boolean_ready(self):
        self.invalid('Ready',1)

    def test_boolean_offer(self):
        self.invalid('Offer',1)

    def test_boolean_last(self):
        self.invalid('Last',1)

    def test_source_digest(self):
        self.assertEqual("MIT",verify_sources(ROOT)["license"])

    def test_different_seeds(self):
        self.assertNotEqual(plan(1),plan(17))

    def test_minimum_plan(self):
        self.assertEqual(14,len(validate_plan(plan(1,count=1))))

    def test_maximum_plan(self):
        self.assertEqual(4109,len(validate_plan(plan(1,count=4096))))

    def test_midstream_reset(self):
        rows=plan(1,reset_at=12)
        self.assertTrue(rows[13]['Reset'])
        validate_plan(rows)
