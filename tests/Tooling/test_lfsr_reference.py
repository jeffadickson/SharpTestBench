import binascii
from pathlib import Path
import sys
import unittest
import zlib
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tests/Tooling/managed_compat'))
from lfsr_fixture import config, validate_config, transition, reverse_bits, plan

class Tests(unittest.TestCase):
    def crc(self,settings,state,payload):
        for byte in payload: state,_=transition(settings,state,byte)
        return state

    def test_crc32_stdlib_0(self):
        payload=b''
        self.assertEqual(zlib.crc32(payload),self.crc(config(),0xffffffff,payload)^0xffffffff)

    def test_crc32_stdlib_1(self):
        payload=b'123456789'
        self.assertEqual(zlib.crc32(payload),self.crc(config(),0xffffffff,payload)^0xffffffff)

    def test_crc32_stdlib_2(self):
        payload=b'\x00'
        self.assertEqual(zlib.crc32(payload),self.crc(config(),0xffffffff,payload)^0xffffffff)

    def test_crc32_stdlib_3(self):
        payload=b'\xff'
        self.assertEqual(zlib.crc32(payload),self.crc(config(),0xffffffff,payload)^0xffffffff)

    def test_crc32_stdlib_4(self):
        payload=b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f\x80\x81\x82\x83\x84\x85\x86\x87\x88\x89\x8a\x8b\x8c\x8d\x8e\x8f\x90\x91\x92\x93\x94\x95\x96\x97\x98\x99\x9a\x9b\x9c\x9d\x9e\x9f\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'
        self.assertEqual(zlib.crc32(payload),self.crc(config(),0xffffffff,payload)^0xffffffff)

    def test_crc32_stdlib_5(self):
        payload=b'SharpTestBench'
        self.assertEqual(zlib.crc32(payload),self.crc(config(),0xffffffff,payload)^0xffffffff)

    def test_crc16_stdlib_0(self):
        payload=b''
        self.assertEqual(binascii.crc_hqx(payload,0xffff),self.crc(config(16,0x1021,8,reverse=False),0xffff,payload))

    def test_crc16_stdlib_1(self):
        payload=b'123456789'
        self.assertEqual(binascii.crc_hqx(payload,0xffff),self.crc(config(16,0x1021,8,reverse=False),0xffff,payload))

    def test_crc16_stdlib_2(self):
        payload=b'\x00'
        self.assertEqual(binascii.crc_hqx(payload,0xffff),self.crc(config(16,0x1021,8,reverse=False),0xffff,payload))

    def test_crc16_stdlib_3(self):
        payload=b'\xff'
        self.assertEqual(binascii.crc_hqx(payload,0xffff),self.crc(config(16,0x1021,8,reverse=False),0xffff,payload))

    def test_crc16_stdlib_4(self):
        payload=b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f\x80\x81\x82\x83\x84\x85\x86\x87\x88\x89\x8a\x8b\x8c\x8d\x8e\x8f\x90\x91\x92\x93\x94\x95\x96\x97\x98\x99\x9a\x9b\x9c\x9d\x9e\x9f\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'
        self.assertEqual(binascii.crc_hqx(payload,0xffff),self.crc(config(16,0x1021,8,reverse=False),0xffff,payload))

    def test_crc16_stdlib_5(self):
        payload=b'Verilog'
        self.assertEqual(binascii.crc_hqx(payload,0xffff),self.crc(config(16,0x1021,8,reverse=False),0xffff,payload))

    def test_bad_width_0(self):
        with self.assertRaises(ValueError): config(**{'width':0})

    def test_bad_width_1(self):
        with self.assertRaises(ValueError): config(**{'width':65})

    def test_bad_width_2(self):
        with self.assertRaises(ValueError): config(**{'width':True})

    def test_bad_width_3(self):
        with self.assertRaises(ValueError): config(**{'width':1.5})

    def test_bad_data_width_0(self):
        with self.assertRaises(ValueError): config(**{'data_width':0})

    def test_bad_data_width_1(self):
        with self.assertRaises(ValueError): config(**{'data_width':65})

    def test_bad_data_width_2(self):
        with self.assertRaises(ValueError): config(**{'data_width':True})

    def test_bad_data_width_3(self):
        with self.assertRaises(ValueError): config(**{'data_width':1.5})

    def test_bad_polynomial_0(self):
        with self.assertRaises(ValueError): config(**{'polynomial':0})

    def test_bad_polynomial_1(self):
        with self.assertRaises(ValueError): config(**{'polynomial':-1})

    def test_bad_polynomial_2(self):
        with self.assertRaises(ValueError): config(**{'polynomial':2})

    def test_bad_polynomial_3(self):
        with self.assertRaises(ValueError): config(**{'polynomial':4294967296})

    def test_bad_polynomial_4(self):
        with self.assertRaises(ValueError): config(**{'polynomial':True})

    def test_bad_topology_0(self):
        with self.assertRaises(ValueError): config(topology='galois')

    def test_bad_topology_1(self):
        with self.assertRaises(ValueError): config(topology='UNKNOWN')

    def test_bad_topology_2(self):
        with self.assertRaises(ValueError): config(topology=None)

    def test_bad_topology_3(self):
        with self.assertRaises(ValueError): config(topology=1)

    def test_bad_reverse_0(self):
        with self.assertRaises(ValueError): config(**{'reverse':0})

    def test_bad_reverse_1(self):
        with self.assertRaises(ValueError): config(**{'reverse':1})

    def test_bad_reverse_2(self):
        with self.assertRaises(ValueError): config(**{'reverse':None})

    def test_bad_feed_forward_0(self):
        with self.assertRaises(ValueError): config(**{'feed_forward':0})

    def test_bad_feed_forward_1(self):
        with self.assertRaises(ValueError): config(**{'feed_forward':1})

    def test_bad_feed_forward_2(self):
        with self.assertRaises(ValueError): config(**{'feed_forward':None})

    def test_reverse_involution_1(self):
        value=(1<<1)-3 if 1>1 else 1
        self.assertEqual(value,reverse_bits(reverse_bits(value,1),1))

    def test_reverse_involution_7(self):
        value=(1<<7)-3 if 7>1 else 1
        self.assertEqual(value,reverse_bits(reverse_bits(value,7),7))

    def test_reverse_involution_8(self):
        value=(1<<8)-3 if 8>1 else 1
        self.assertEqual(value,reverse_bits(reverse_bits(value,8),8))

    def test_reverse_involution_16(self):
        value=(1<<16)-3 if 16>1 else 1
        self.assertEqual(value,reverse_bits(reverse_bits(value,16),16))

    def test_reverse_involution_31(self):
        value=(1<<31)-3 if 31>1 else 1
        self.assertEqual(value,reverse_bits(reverse_bits(value,31),31))

    def test_reverse_involution_32(self):
        value=(1<<32)-3 if 32>1 else 1
        self.assertEqual(value,reverse_bits(reverse_bits(value,32),32))

    def test_reverse_involution_58(self):
        value=(1<<58)-3 if 58>1 else 1
        self.assertEqual(value,reverse_bits(reverse_bits(value,58),58))

    def test_reverse_involution_64(self):
        value=(1<<64)-3 if 64>1 else 1
        self.assertEqual(value,reverse_bits(reverse_bits(value,64),64))

    def test_prbs7_known_steps(self):
        c=config(7,0x41,1,'FIBONACCI',False)
        state=1
        actual=[]
        for _ in range(7):
            state,_=transition(c,state,0);actual.append(state)
        self.assertEqual([2,4,8,16,32,65,3],actual)
