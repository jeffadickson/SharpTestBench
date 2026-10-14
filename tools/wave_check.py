#!/usr/bin/env python3
"""Validate VCD content and JSON/JUnit artifact links for the known fixture."""
import argparse
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET


def check(label, known_values=True):
    build = Path(__file__).resolve().parents[1] / 'build'
    manifest = json.loads((build / (label + '-artifacts.json')).read_text())
    artifact, = manifest['Artifacts']
    wave = build / artifact['Path']
    assert manifest['SchemaVersion'] == 1 and artifact['Format'] == 'vcd'
    assert artifact['Exists'] and artifact['Bytes'] == wave.stat().st_size > 0
    text = wave.read_text()
    assert '$enddefinitions' in text and re.search(r'^#\d+', text, re.M)
    if known_values:
        match = re.search(r'\$var\s+\S+\s+8\s+(\S+)\s+data(?:\[|\s)', text)
        assert match, 'data signal missing in VCD'
        identifier = match[1]
        values = {int(m[1], 2) for m in re.finditer(r'^b([01]+)\s+' + re.escape(identifier) + r'\s*$', text, re.M)}
        assert {20, 50}.issubset(values), values
    rows = json.loads((build / manifest['Results']).read_text())
    assert rows and all(r['Artifacts'] == [artifact] for r in rows)
    junit = ET.parse((build / manifest['Results']).with_suffix('.xml'))
    cases = junit.findall('.//testcase')
    assert len(cases) == len(rows)
    assert all(c.find("properties/property[@name='waveform']").get('value') == artifact['Path'] for c in cases)
    print('WAVE_PASS', label, 'VCD content and result/JUnit links')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--label', required=True)
    args = parser.parse_args()
    check(args.label)
