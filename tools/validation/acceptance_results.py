"""Fail-closed completeness checks for the two explicitly reviewed scopes only."""
from collections import Counter
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def validate_result(path, exit_code, inventory_path, scope="collision-contract"):
    if exit_code != 0:
        raise ValueError('Unity exit was not zero')
    inventory = json.loads(Path(inventory_path).read_text())
    names = inventory['activeCases']
    expected = {'collision-contract':100, 'regression':412}.get(scope)
    if expected is None or inventory['scope'] != scope or len(names) != expected or len(set(names)) != expected:
        raise ValueError('Invalid reviewed inventory')
    root = ET.parse(path).getroot()
    if root.tag != 'test-run':
        raise ValueError('Missing NUnit test-run')
    leaves = list(root.iter('test-case'))
    if Counter(n.get('fullname') for n in leaves) != Counter(names):
        raise ValueError('Selected case identities/multiplicities differ from reviewed inventory')
    nodes = [root, *root.iter('test-suite'), *leaves]
    for node in nodes:
        if node.get('result') != 'Passed' or node.get('label'):
            raise ValueError('Non-passing selected node: ' + str(node.get('fullname', node.tag)))
        if node.get('runstate', 'Runnable') != 'Runnable':
            raise ValueError('Non-runnable selected node')
        if not node.get('start-time') or not node.get('end-time'):
            raise ValueError('Incomplete execution timestamps')
        if node.tag != 'test-case':
            count = len(list(node.iter('test-case')))
            if count == 0:
                raise ValueError('Empty selected suite')
            for key, value in [('total', count), ('passed', count), ('failed', 0),
                                  ('inconclusive', 0), ('skipped', 0)]:
                if node.get(key) != str(value):
                    raise ValueError('Inconsistent aggregate ' + key)
    return dict(scope=scope, expected=expected, executed=len(leaves),
                passed=expected, skipped=0, inventoryMatched=True)
