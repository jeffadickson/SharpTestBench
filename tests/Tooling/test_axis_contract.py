import copy
from pathlib import Path
import sys
import unittest
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools'))
from axis_fixture import plan, validate_plan, configuration, project

class Tests(unittest.TestCase):
    def bad(self, edit):
        rows=plan(17); edit(rows)
        with self.assertRaises(ValueError): validate_plan(rows)

    def test_mode_0(self):
        self.assertEqual("axis_mode0",configuration(0))

    def test_mode_1(self):
        self.assertEqual("axis_mode1",configuration(1))

    def test_mode_2(self):
        self.assertEqual("axis_mode2",configuration(2))

    def test_disabled_skid(self):
        self.assertEqual("axis_disabled",configuration(2,False))

    def test_bad_mode_0(self):
        with self.assertRaises(ValueError): configuration(-1)

    def test_bad_mode_1(self):
        with self.assertRaises(ValueError): configuration(3)

    def test_bad_mode_2(self):
        with self.assertRaises(ValueError): configuration(True)

    def test_bad_mode_3(self):
        with self.assertRaises(ValueError): configuration(1.5)

    def test_bad_mode_4(self):
        with self.assertRaises(ValueError): configuration('2')

    def test_bad_mode_5(self):
        with self.assertRaises(ValueError): configuration(None)

    def test_bad_sidebands_0(self):
        with self.assertRaises(ValueError): configuration(2,0)

    def test_bad_sidebands_1(self):
        with self.assertRaises(ValueError): configuration(2,1)

    def test_bad_sidebands_2(self):
        with self.assertRaises(ValueError): configuration(2,None)

    def test_bad_sidebands_3(self):
        with self.assertRaises(ValueError): configuration(2,'true')

    def test_disabled_bad_mode_0(self):
        with self.assertRaises(ValueError): configuration(0,False)

    def test_disabled_bad_mode_1(self):
        with self.assertRaises(ValueError): configuration(1,False)

    def test_missing_data(self):
        self.bad(lambda rows: rows[1].pop('Data'))

    def test_missing_keep(self):
        self.bad(lambda rows: rows[1].pop('Keep'))

    def test_missing_last(self):
        self.bad(lambda rows: rows[1].pop('Last'))

    def test_missing_id(self):
        self.bad(lambda rows: rows[1].pop('Id'))

    def test_missing_dest(self):
        self.bad(lambda rows: rows[1].pop('Dest'))

    def test_missing_user(self):
        self.bad(lambda rows: rows[1].pop('User'))

    def test_unknown_field(self):
        self.bad(lambda rows: rows[1].update(Unexpected=1))

    def test_initial_reset(self):
        self.bad(lambda rows: rows[0].update(Reset=False))

    def test_bad_drain_reset(self):
        self.bad(lambda rows: rows[-1].update(Reset=True))

    def test_bad_drain_ready(self):
        self.bad(lambda rows: rows[-1].update(Ready=False))

    def test_bad_drain_offer(self):
        self.bad(lambda rows: rows[-1].update(Offer=True))

    def test_bad_rows_0(self):
        with self.assertRaises(ValueError): validate_plan([])

    def test_bad_rows_1(self):
        with self.assertRaises(ValueError): validate_plan([{}])

    def test_bad_rows_2(self):
        with self.assertRaises(ValueError): validate_plan({})

    def test_bad_rows_3(self):
        with self.assertRaises(ValueError): validate_plan(None)

    def test_bad_rows_4(self):
        with self.assertRaises(ValueError): validate_plan('rows')

    def test_bad_reset_0(self):
        with self.assertRaises(ValueError): plan(1,reset_at=0)

    def test_bad_reset_1(self):
        with self.assertRaises(ValueError): plan(1,reset_at=64)

    def test_bad_reset_2(self):
        with self.assertRaises(ValueError): plan(1,reset_at=-1)

    def test_bad_reset_3(self):
        with self.assertRaises(ValueError): plan(1,reset_at=True)

    def test_bad_reset_4(self):
        with self.assertRaises(ValueError): plan(1,reset_at=1.5)

    def test_project_data(self):
        row=plan(1)[1]
        row['Data']=65535
        self.assertEqual(65535,project(row)[0])

    def test_project_keep(self):
        row=plan(1)[1]
        row['Keep']=2
        self.assertEqual(2,project(row)[1])

    def test_project_last(self):
        row=plan(1)[1]
        row['Last']=True
        self.assertEqual(1,project(row)[2])

    def test_project_id(self):
        row=plan(1)[1]
        row['Id']=15
        self.assertEqual(15,project(row)[3])

    def test_project_dest(self):
        row=plan(1)[1]
        row['Dest']=13
        self.assertEqual(13,project(row)[4])

    def test_project_user(self):
        row=plan(1)[1]
        row['User']=3
        self.assertEqual(3,project(row)[5])

    def test_disabled_projection(self):
        row=plan(1)[1]
        self.assertEqual([3,1,0,0,0],project(row,False)[1:])
