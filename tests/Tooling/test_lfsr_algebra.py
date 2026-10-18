from pathlib import Path
import sys
import unittest
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools'))
from lfsr_fixture import CASES, STYLES, fixture, config, transition, reverse_bits, plan

class Tests(unittest.TestCase):
    pass

    def test_zero_crc32(self):
        _,c=fixture('crc32',"LOOP")
        self.assertEqual((0,0),transition(c,0,0))

    def test_linearity_crc32(self):
        _,c=fixture('crc32',"LOOP")
        a=transition(c,1,0);b=transition(c,0,1)
        self.assertEqual(tuple(x^y for x,y in zip(a,b)),transition(c,1,1))

    def test_style_crc32(self):
        a,c=fixture('crc32',"LOOP");b,d=fixture('crc32',"REDUCTION")
        self.assertNotEqual(a,b)
        self.assertEqual(c,d)

    def test_reversal_crc32(self):
        _,c=fixture('crc32',"LOOP")
        normal=dict(c,Reverse=False);rev=dict(c,Reverse=True)
        a=transition(normal,reverse_bits(1,c["Width"]),reverse_bits(1,c["DataWidth"]))
        b=transition(rev,1,1)
        self.assertEqual((reverse_bits(a[0],c["Width"]),reverse_bits(a[1],c["DataWidth"])),b)

    def test_zero_crc16(self):
        _,c=fixture('crc16',"LOOP")
        self.assertEqual((0,0),transition(c,0,0))

    def test_linearity_crc16(self):
        _,c=fixture('crc16',"LOOP")
        a=transition(c,1,0);b=transition(c,0,1)
        self.assertEqual(tuple(x^y for x,y in zip(a,b)),transition(c,1,1))

    def test_style_crc16(self):
        a,c=fixture('crc16',"LOOP");b,d=fixture('crc16',"REDUCTION")
        self.assertNotEqual(a,b)
        self.assertEqual(c,d)

    def test_reversal_crc16(self):
        _,c=fixture('crc16',"LOOP")
        normal=dict(c,Reverse=False);rev=dict(c,Reverse=True)
        a=transition(normal,reverse_bits(1,c["Width"]),reverse_bits(1,c["DataWidth"]))
        b=transition(rev,1,1)
        self.assertEqual((reverse_bits(a[0],c["Width"]),reverse_bits(a[1],c["DataWidth"])),b)

    def test_zero_crc32c(self):
        _,c=fixture('crc32c',"LOOP")
        self.assertEqual((0,0),transition(c,0,0))

    def test_linearity_crc32c(self):
        _,c=fixture('crc32c',"LOOP")
        a=transition(c,1,0);b=transition(c,0,1)
        self.assertEqual(tuple(x^y for x,y in zip(a,b)),transition(c,1,1))

    def test_style_crc32c(self):
        a,c=fixture('crc32c',"LOOP");b,d=fixture('crc32c',"REDUCTION")
        self.assertNotEqual(a,b)
        self.assertEqual(c,d)

    def test_reversal_crc32c(self):
        _,c=fixture('crc32c',"LOOP")
        normal=dict(c,Reverse=False);rev=dict(c,Reverse=True)
        a=transition(normal,reverse_bits(1,c["Width"]),reverse_bits(1,c["DataWidth"]))
        b=transition(rev,1,1)
        self.assertEqual((reverse_bits(a[0],c["Width"]),reverse_bits(a[1],c["DataWidth"])),b)

    def test_zero_prbs7(self):
        _,c=fixture('prbs7',"LOOP")
        self.assertEqual((0,0),transition(c,0,0))

    def test_linearity_prbs7(self):
        _,c=fixture('prbs7',"LOOP")
        a=transition(c,1,0);b=transition(c,0,1)
        self.assertEqual(tuple(x^y for x,y in zip(a,b)),transition(c,1,1))

    def test_style_prbs7(self):
        a,c=fixture('prbs7',"LOOP");b,d=fixture('prbs7',"REDUCTION")
        self.assertNotEqual(a,b)
        self.assertEqual(c,d)

    def test_reversal_prbs7(self):
        _,c=fixture('prbs7',"LOOP")
        normal=dict(c,Reverse=False);rev=dict(c,Reverse=True)
        a=transition(normal,reverse_bits(1,c["Width"]),reverse_bits(1,c["DataWidth"]))
        b=transition(rev,1,1)
        self.assertEqual((reverse_bits(a[0],c["Width"]),reverse_bits(a[1],c["DataWidth"])),b)

    def test_zero_scrambler(self):
        _,c=fixture('scrambler',"LOOP")
        self.assertEqual((0,0),transition(c,0,0))

    def test_linearity_scrambler(self):
        _,c=fixture('scrambler',"LOOP")
        a=transition(c,1,0);b=transition(c,0,1)
        self.assertEqual(tuple(x^y for x,y in zip(a,b)),transition(c,1,1))

    def test_style_scrambler(self):
        a,c=fixture('scrambler',"LOOP");b,d=fixture('scrambler',"REDUCTION")
        self.assertNotEqual(a,b)
        self.assertEqual(c,d)

    def test_reversal_scrambler(self):
        _,c=fixture('scrambler',"LOOP")
        normal=dict(c,Reverse=False);rev=dict(c,Reverse=True)
        a=transition(normal,reverse_bits(1,c["Width"]),reverse_bits(1,c["DataWidth"]))
        b=transition(rev,1,1)
        self.assertEqual((reverse_bits(a[0],c["Width"]),reverse_bits(a[1],c["DataWidth"])),b)

    def test_zero_descrambler(self):
        _,c=fixture('descrambler',"LOOP")
        self.assertEqual((0,0),transition(c,0,0))

    def test_linearity_descrambler(self):
        _,c=fixture('descrambler',"LOOP")
        a=transition(c,1,0);b=transition(c,0,1)
        self.assertEqual(tuple(x^y for x,y in zip(a,b)),transition(c,1,1))

    def test_style_descrambler(self):
        a,c=fixture('descrambler',"LOOP");b,d=fixture('descrambler',"REDUCTION")
        self.assertNotEqual(a,b)
        self.assertEqual(c,d)

    def test_reversal_descrambler(self):
        _,c=fixture('descrambler',"LOOP")
        normal=dict(c,Reverse=False);rev=dict(c,Reverse=True)
        a=transition(normal,reverse_bits(1,c["Width"]),reverse_bits(1,c["DataWidth"]))
        b=transition(rev,1,1)
        self.assertEqual((reverse_bits(a[0],c["Width"]),reverse_bits(a[1],c["DataWidth"])),b)

    def test_zero_galois_ff(self):
        _,c=fixture('galois_ff',"LOOP")
        self.assertEqual((0,0),transition(c,0,0))

    def test_linearity_galois_ff(self):
        _,c=fixture('galois_ff',"LOOP")
        a=transition(c,1,0);b=transition(c,0,1)
        self.assertEqual(tuple(x^y for x,y in zip(a,b)),transition(c,1,1))

    def test_style_galois_ff(self):
        a,c=fixture('galois_ff',"LOOP");b,d=fixture('galois_ff',"REDUCTION")
        self.assertNotEqual(a,b)
        self.assertEqual(c,d)

    def test_reversal_galois_ff(self):
        _,c=fixture('galois_ff',"LOOP")
        normal=dict(c,Reverse=False);rev=dict(c,Reverse=True)
        a=transition(normal,reverse_bits(1,c["Width"]),reverse_bits(1,c["DataWidth"]))
        b=transition(rev,1,1)
        self.assertEqual((reverse_bits(a[0],c["Width"]),reverse_bits(a[1],c["DataWidth"])),b)

    def test_zero_minimal(self):
        _,c=fixture('minimal',"LOOP")
        self.assertEqual((0,0),transition(c,0,0))

    def test_linearity_minimal(self):
        _,c=fixture('minimal',"LOOP")
        a=transition(c,1,0);b=transition(c,0,1)
        self.assertEqual(tuple(x^y for x,y in zip(a,b)),transition(c,1,1))

    def test_style_minimal(self):
        a,c=fixture('minimal',"LOOP");b,d=fixture('minimal',"REDUCTION")
        self.assertNotEqual(a,b)
        self.assertEqual(c,d)

    def test_reversal_minimal(self):
        _,c=fixture('minimal',"LOOP")
        normal=dict(c,Reverse=False);rev=dict(c,Reverse=True)
        a=transition(normal,reverse_bits(1,c["Width"]),reverse_bits(1,c["DataWidth"]))
        b=transition(rev,1,1)
        self.assertEqual((reverse_bits(a[0],c["Width"]),reverse_bits(a[1],c["DataWidth"])),b)

    def test_plan_seed_0(self):
        self.assertEqual(plan(config(),0),plan(config(),0))

    def test_plan_seed_1(self):
        self.assertEqual(plan(config(),1),plan(config(),1))

    def test_plan_seed_17(self):
        self.assertEqual(plan(config(),17),plan(config(),17))

    def test_plan_seed_4294967296(self):
        self.assertEqual(plan(config(),4294967296),plan(config(),4294967296))

    def test_plan_seed_18446744073709551615(self):
        self.assertEqual(plan(config(),18446744073709551615),plan(config(),18446744073709551615))

    def test_basis_crc32(self):
        _,c=fixture('crc32',"LOOP")
        p=plan(c,1,1)
        self.assertEqual(5+c["Width"]+c["DataWidth"],len(p["Vectors"]))

    def test_basis_crc16(self):
        _,c=fixture('crc16',"LOOP")
        p=plan(c,1,1)
        self.assertEqual(5+c["Width"]+c["DataWidth"],len(p["Vectors"]))

    def test_basis_crc32c(self):
        _,c=fixture('crc32c',"LOOP")
        p=plan(c,1,1)
        self.assertEqual(5+c["Width"]+c["DataWidth"],len(p["Vectors"]))

    def test_basis_prbs7(self):
        _,c=fixture('prbs7',"LOOP")
        p=plan(c,1,1)
        self.assertEqual(5+c["Width"]+c["DataWidth"],len(p["Vectors"]))

    def test_basis_scrambler(self):
        _,c=fixture('scrambler',"LOOP")
        p=plan(c,1,1)
        self.assertEqual(5+c["Width"]+c["DataWidth"],len(p["Vectors"]))

    def test_basis_descrambler(self):
        _,c=fixture('descrambler',"LOOP")
        p=plan(c,1,1)
        self.assertEqual(5+c["Width"]+c["DataWidth"],len(p["Vectors"]))

    def test_feed_forward_difference(self):
        a=config(7,0x41,8,"FIBONACCI",False,False)
        b=dict(a,FeedForward=True)
        self.assertNotEqual(transition(a,1,0),transition(b,1,0))
