import binascii
import random
import sys
import pathlib
import unittest
import zlib
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2] / 'tests/Tooling/managed_compat'))
from lfsr_fixture import config, transition, CASES

class LfsrStreamingTests(unittest.TestCase):

    def test_crc32_stream_seed_0(self):
        rng = random.Random(0)
        payload = bytes(rng.randrange(256) for _ in range(1))
        state = 0xffffffff
        for byte in payload: state, _ = transition(config(), state, byte)
        self.assertEqual(zlib.crc32(payload), state ^ 0xffffffff)

    def test_crc16_stream_seed_0(self):
        rng = random.Random(0)
        payload = bytes(rng.randrange(256) for _ in range(1))
        initial = rng.randrange(65536)
        state = initial
        for byte in payload: state, _ = transition(config(16, 0x1021, 8, reverse=False), state, byte)
        self.assertEqual(binascii.crc_hqx(payload, initial), state)

    def test_crc32_stream_seed_1(self):
        rng = random.Random(1)
        payload = bytes(rng.randrange(256) for _ in range(2))
        state = 0xffffffff
        for byte in payload: state, _ = transition(config(), state, byte)
        self.assertEqual(zlib.crc32(payload), state ^ 0xffffffff)

    def test_crc16_stream_seed_1(self):
        rng = random.Random(1)
        payload = bytes(rng.randrange(256) for _ in range(2))
        initial = rng.randrange(65536)
        state = initial
        for byte in payload: state, _ = transition(config(16, 0x1021, 8, reverse=False), state, byte)
        self.assertEqual(binascii.crc_hqx(payload, initial), state)

    def test_crc32_stream_seed_2(self):
        rng = random.Random(2)
        payload = bytes(rng.randrange(256) for _ in range(3))
        state = 0xffffffff
        for byte in payload: state, _ = transition(config(), state, byte)
        self.assertEqual(zlib.crc32(payload), state ^ 0xffffffff)

    def test_crc16_stream_seed_2(self):
        rng = random.Random(2)
        payload = bytes(rng.randrange(256) for _ in range(3))
        initial = rng.randrange(65536)
        state = initial
        for byte in payload: state, _ = transition(config(16, 0x1021, 8, reverse=False), state, byte)
        self.assertEqual(binascii.crc_hqx(payload, initial), state)

    def test_crc32_stream_seed_7(self):
        rng = random.Random(7)
        payload = bytes(rng.randrange(256) for _ in range(8))
        state = 0xffffffff
        for byte in payload: state, _ = transition(config(), state, byte)
        self.assertEqual(zlib.crc32(payload), state ^ 0xffffffff)

    def test_crc16_stream_seed_7(self):
        rng = random.Random(7)
        payload = bytes(rng.randrange(256) for _ in range(8))
        initial = rng.randrange(65536)
        state = initial
        for byte in payload: state, _ = transition(config(16, 0x1021, 8, reverse=False), state, byte)
        self.assertEqual(binascii.crc_hqx(payload, initial), state)

    def test_crc32_stream_seed_17(self):
        rng = random.Random(17)
        payload = bytes(rng.randrange(256) for _ in range(18))
        state = 0xffffffff
        for byte in payload: state, _ = transition(config(), state, byte)
        self.assertEqual(zlib.crc32(payload), state ^ 0xffffffff)

    def test_crc16_stream_seed_17(self):
        rng = random.Random(17)
        payload = bytes(rng.randrange(256) for _ in range(18))
        initial = rng.randrange(65536)
        state = initial
        for byte in payload: state, _ = transition(config(16, 0x1021, 8, reverse=False), state, byte)
        self.assertEqual(binascii.crc_hqx(payload, initial), state)

    def test_crc32_stream_seed_31(self):
        rng = random.Random(31)
        payload = bytes(rng.randrange(256) for _ in range(32))
        state = 0xffffffff
        for byte in payload: state, _ = transition(config(), state, byte)
        self.assertEqual(zlib.crc32(payload), state ^ 0xffffffff)

    def test_crc16_stream_seed_31(self):
        rng = random.Random(31)
        payload = bytes(rng.randrange(256) for _ in range(32))
        initial = rng.randrange(65536)
        state = initial
        for byte in payload: state, _ = transition(config(16, 0x1021, 8, reverse=False), state, byte)
        self.assertEqual(binascii.crc_hqx(payload, initial), state)

    def test_crc32_stream_seed_63(self):
        rng = random.Random(63)
        payload = bytes(rng.randrange(256) for _ in range(64))
        state = 0xffffffff
        for byte in payload: state, _ = transition(config(), state, byte)
        self.assertEqual(zlib.crc32(payload), state ^ 0xffffffff)

    def test_crc16_stream_seed_63(self):
        rng = random.Random(63)
        payload = bytes(rng.randrange(256) for _ in range(64))
        initial = rng.randrange(65536)
        state = initial
        for byte in payload: state, _ = transition(config(16, 0x1021, 8, reverse=False), state, byte)
        self.assertEqual(binascii.crc_hqx(payload, initial), state)

    def test_crc32_stream_seed_99(self):
        rng = random.Random(99)
        payload = bytes(rng.randrange(256) for _ in range(33))
        state = 0xffffffff
        for byte in payload: state, _ = transition(config(), state, byte)
        self.assertEqual(zlib.crc32(payload), state ^ 0xffffffff)

    def test_crc16_stream_seed_99(self):
        rng = random.Random(99)
        payload = bytes(rng.randrange(256) for _ in range(33))
        initial = rng.randrange(65536)
        state = initial
        for byte in payload: state, _ = transition(config(16, 0x1021, 8, reverse=False), state, byte)
        self.assertEqual(binascii.crc_hqx(payload, initial), state)

    def test_crc32_stream_seed_255(self):
        rng = random.Random(255)
        payload = bytes(rng.randrange(256) for _ in range(55))
        state = 0xffffffff
        for byte in payload: state, _ = transition(config(), state, byte)
        self.assertEqual(zlib.crc32(payload), state ^ 0xffffffff)

    def test_crc16_stream_seed_255(self):
        rng = random.Random(255)
        payload = bytes(rng.randrange(256) for _ in range(55))
        initial = rng.randrange(65536)
        state = initial
        for byte in payload: state, _ = transition(config(16, 0x1021, 8, reverse=False), state, byte)
        self.assertEqual(binascii.crc_hqx(payload, initial), state)

    def test_crc32_stream_seed_65535(self):
        rng = random.Random(65535)
        payload = bytes(rng.randrange(256) for _ in range(10))
        state = 0xffffffff
        for byte in payload: state, _ = transition(config(), state, byte)
        self.assertEqual(zlib.crc32(payload), state ^ 0xffffffff)

    def test_crc16_stream_seed_65535(self):
        rng = random.Random(65535)
        payload = bytes(rng.randrange(256) for _ in range(10))
        initial = rng.randrange(65536)
        state = initial
        for byte in payload: state, _ = transition(config(16, 0x1021, 8, reverse=False), state, byte)
        self.assertEqual(binascii.crc_hqx(payload, initial), state)

    def test_serial_composition_crc32(self):
        settings = config(*CASES['crc32'])
        width = settings['Width']; data_width = settings['DataWidth']
        rng = random.Random(99)
        for _ in range(16):
            state = rng.getrandbits(width); data = rng.getrandbits(data_width)
            actual_state, actual_data = transition(settings, state, data)
            serial = dict(settings, DataWidth=1)
            expected_state = state; expected_data = 0
            indices = range(data_width) if settings['Reverse'] else range(data_width-1, -1, -1)
            for index in indices:
                expected_state, output = transition(serial, expected_state, (data >> index) & 1)
                expected_data |= output << index
            self.assertEqual((expected_state, expected_data), (actual_state, actual_data))

    def test_serial_composition_crc16(self):
        settings = config(*CASES['crc16'])
        width = settings['Width']; data_width = settings['DataWidth']
        rng = random.Random(99)
        for _ in range(16):
            state = rng.getrandbits(width); data = rng.getrandbits(data_width)
            actual_state, actual_data = transition(settings, state, data)
            serial = dict(settings, DataWidth=1)
            expected_state = state; expected_data = 0
            indices = range(data_width) if settings['Reverse'] else range(data_width-1, -1, -1)
            for index in indices:
                expected_state, output = transition(serial, expected_state, (data >> index) & 1)
                expected_data |= output << index
            self.assertEqual((expected_state, expected_data), (actual_state, actual_data))

    def test_serial_composition_crc32c(self):
        settings = config(*CASES['crc32c'])
        width = settings['Width']; data_width = settings['DataWidth']
        rng = random.Random(99)
        for _ in range(16):
            state = rng.getrandbits(width); data = rng.getrandbits(data_width)
            actual_state, actual_data = transition(settings, state, data)
            serial = dict(settings, DataWidth=1)
            expected_state = state; expected_data = 0
            indices = range(data_width) if settings['Reverse'] else range(data_width-1, -1, -1)
            for index in indices:
                expected_state, output = transition(serial, expected_state, (data >> index) & 1)
                expected_data |= output << index
            self.assertEqual((expected_state, expected_data), (actual_state, actual_data))

    def test_serial_composition_prbs7(self):
        settings = config(*CASES['prbs7'])
        width = settings['Width']; data_width = settings['DataWidth']
        rng = random.Random(99)
        for _ in range(16):
            state = rng.getrandbits(width); data = rng.getrandbits(data_width)
            actual_state, actual_data = transition(settings, state, data)
            serial = dict(settings, DataWidth=1)
            expected_state = state; expected_data = 0
            indices = range(data_width) if settings['Reverse'] else range(data_width-1, -1, -1)
            for index in indices:
                expected_state, output = transition(serial, expected_state, (data >> index) & 1)
                expected_data |= output << index
            self.assertEqual((expected_state, expected_data), (actual_state, actual_data))

    def test_serial_composition_scrambler(self):
        settings = config(*CASES['scrambler'])
        width = settings['Width']; data_width = settings['DataWidth']
        rng = random.Random(99)
        for _ in range(16):
            state = rng.getrandbits(width); data = rng.getrandbits(data_width)
            actual_state, actual_data = transition(settings, state, data)
            serial = dict(settings, DataWidth=1)
            expected_state = state; expected_data = 0
            indices = range(data_width) if settings['Reverse'] else range(data_width-1, -1, -1)
            for index in indices:
                expected_state, output = transition(serial, expected_state, (data >> index) & 1)
                expected_data |= output << index
            self.assertEqual((expected_state, expected_data), (actual_state, actual_data))

    def test_serial_composition_descrambler(self):
        settings = config(*CASES['descrambler'])
        width = settings['Width']; data_width = settings['DataWidth']
        rng = random.Random(99)
        for _ in range(16):
            state = rng.getrandbits(width); data = rng.getrandbits(data_width)
            actual_state, actual_data = transition(settings, state, data)
            serial = dict(settings, DataWidth=1)
            expected_state = state; expected_data = 0
            indices = range(data_width) if settings['Reverse'] else range(data_width-1, -1, -1)
            for index in indices:
                expected_state, output = transition(serial, expected_state, (data >> index) & 1)
                expected_data |= output << index
            self.assertEqual((expected_state, expected_data), (actual_state, actual_data))

    def test_serial_composition_galois_ff(self):
        settings = config(*CASES['galois_ff'])
        width = settings['Width']; data_width = settings['DataWidth']
        rng = random.Random(99)
        for _ in range(16):
            state = rng.getrandbits(width); data = rng.getrandbits(data_width)
            actual_state, actual_data = transition(settings, state, data)
            serial = dict(settings, DataWidth=1)
            expected_state = state; expected_data = 0
            indices = range(data_width) if settings['Reverse'] else range(data_width-1, -1, -1)
            for index in indices:
                expected_state, output = transition(serial, expected_state, (data >> index) & 1)
                expected_data |= output << index
            self.assertEqual((expected_state, expected_data), (actual_state, actual_data))

    def test_serial_composition_minimal(self):
        settings = config(*CASES['minimal'])
        width = settings['Width']; data_width = settings['DataWidth']
        rng = random.Random(99)
        for _ in range(16):
            state = rng.getrandbits(width); data = rng.getrandbits(data_width)
            actual_state, actual_data = transition(settings, state, data)
            serial = dict(settings, DataWidth=1)
            expected_state = state; expected_data = 0
            indices = range(data_width) if settings['Reverse'] else range(data_width-1, -1, -1)
            for index in indices:
                expected_state, output = transition(serial, expected_state, (data >> index) & 1)
                expected_data |= output << index
            self.assertEqual((expected_state, expected_data), (actual_state, actual_data))

    def test_crc32_split_payload_7(self):
        payload = bytes(range(7))
        for split in range(len(payload)+1):
            expected = zlib.crc32(payload[split:], zlib.crc32(payload[:split]))
            state = 0xffffffff
            for chunk in (payload[:split], payload[split:]):
                for byte in chunk: state, _ = transition(config(), state, byte)
            self.assertEqual(expected, state ^ 0xffffffff)

    def test_crc32_split_payload_16(self):
        payload = bytes(range(16))
        for split in range(len(payload)+1):
            expected = zlib.crc32(payload[split:], zlib.crc32(payload[:split]))
            state = 0xffffffff
            for chunk in (payload[:split], payload[split:]):
                for byte in chunk: state, _ = transition(config(), state, byte)
            self.assertEqual(expected, state ^ 0xffffffff)

    def test_crc32_split_payload_32(self):
        payload = bytes(range(32))
        for split in range(len(payload)+1):
            expected = zlib.crc32(payload[split:], zlib.crc32(payload[:split]))
            state = 0xffffffff
            for chunk in (payload[:split], payload[split:]):
                for byte in chunk: state, _ = transition(config(), state, byte)
            self.assertEqual(expected, state ^ 0xffffffff)
