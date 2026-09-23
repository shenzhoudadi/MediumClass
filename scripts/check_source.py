#!/usr/bin/env python3
"""Offline invariants only; this is NOT a build or game compatibility test."""
import argparse
from collections import defaultdict
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--strict', action='store_true', help='Fail for known unresolved TypeId collisions too.')
parser.add_argument('--typeid-assembly', action='append', default=[], metavar='DLL',
                    help='Also detect exact TypeId collisions with a game/dependency DLL (repeatable).')
parser.add_argument('--syntax', action='store_true', help='Also parse C# using optional tree-sitter packages.')
args = parser.parse_args()
errors = []
warnings = []

def require(condition, message):
    if not condition:
        errors.append(message)

def norm(value):
    return value.replace('-', '').lower()

baseline = json.loads((ROOT / 'maintenance/identifiers-baseline.json').read_text(encoding='utf-8'))
current_ids = {}
by_id = defaultdict(list)
sources = sorted(ROOT.rglob('*.cs'))
for path in sources:
    if 'obj' in path.relative_to(ROOT).parts or 'bin' in path.relative_to(ROOT).parts:
        continue
    source = path.read_text(encoding='utf-8-sig')
    values = re.findall(r'\[TypeId\("([a-fA-F0-9-]+)"\)\]', source)
    if values:
        key = path.relative_to(ROOT).as_posix()
        current_ids[key] = values
        for value in values:
            by_id[norm(value)].append(key)
require(current_ids == baseline['type_ids'], 'Serialized TypeIds changed. Update only with an explicit save migration plan.')
known_collisions = {key: sorted(value) for key, value in baseline['known_type_id_collisions'].items()}
for identifier, paths in sorted(by_id.items()):
    if len(paths) > 1:
        if sorted(paths) == known_collisions.get(identifier):
            warnings.append('UNRESOLVED legacy TypeId collision: ' + identifier + ' -> ' + ', '.join(paths))
        else:
            errors.append('NEW TypeId collision: ' + identifier)

# The local uniqueness check cannot detect collisions with the game or other mods.
for identifier, owner in baseline.get('reserved_external_type_ids', {}).items():
    require(norm(identifier) not in by_id, 'TypeId reserved by external type: ' + owner)
for filename in args.typeid_assembly:
    try:
        import dnfile
        assembly = dnfile.dnPE(filename, clr_lazy_load=True)
        require(assembly.net is not None, 'Not a managed assembly: ' + filename)
        if assembly.net is None:
            continue
        tables = assembly.net.mdtables
        local_ctors = {method.row_index for typedef in tables.TypeDef.rows
                       if str(typedef.TypeName) == 'TypeIdAttribute'
                       for method in typedef.MethodList}
        for attr in tables.CustomAttribute.rows:
            if attr.Parent.table.name != 'TypeDef':
                continue
            if attr.Type.table.name == 'MethodDef':
                is_typeid = attr.Type.row_index in local_ctors
            else:
                constructor = attr.Type.row
                cls = getattr(constructor, 'Class', None)
                is_typeid = cls is not None and str(getattr(cls.row, 'TypeName', '')) == 'TypeIdAttribute'
            if not is_typeid:
                continue
            value = attr.Value.value
            # GUID strings use one-byte SerString lengths; reject unexpected layouts.
            require(len(value) >= 3 and value[:2] == b'\x01\x00' and value[2] < 128,
                    'Unsupported TypeId attribute encoding: ' + filename)
            if len(value) < 3 or value[:2] != b'\x01\x00' or value[2] >= 128:
                continue
            identifier = norm(value[3:3 + value[2]].decode('utf-8'))
            if identifier in by_id:
                owner = attr.Parent.row
                errors.append('EXTERNAL TypeId collision: ' + identifier + ' -> '
                              + str(owner.TypeNamespace) + '.' + str(owner.TypeName)
                              + ' (' + filename + ')')
        assembly.close()
    except ImportError:
        errors.append('--typeid-assembly requires dnfile from scripts/requirements-checks.txt')
    except Exception as exc:
        errors.append('Could not audit assembly ' + filename + ': ' + str(exc))

guid_text = (ROOT / 'Utilities/Guids.cs').read_text(encoding='utf-8-sig')
guids = re.findall(r'"([a-fA-F0-9-]{32,36})"', guid_text)
require(guids == baseline['blueprint_guids'], 'Blueprint GUID values/order changed; save compatibility review required.')
require(len({norm(guid) for guid in guids}) == len(guids), 'Duplicate blueprint GUID declarations.')

for filename, expected_keys in baseline['localization_keys'].items():
    path = ROOT / filename
    try:
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        def keys(obj):
            if isinstance(obj, dict):
                if 'Key' in obj:
                    yield obj['Key']
                for value in obj.values():
                    yield from keys(value)
            elif isinstance(obj, list):
                for value in obj:
                    yield from keys(value)
        actual = list(keys(data))
        require(actual == expected_keys, f'Localization keys changed: {filename}')
        require(len(set(actual)) == len(actual), f'Duplicate localization keys: {filename}')
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        errors.append(f'Invalid UTF-8/JSON in {filename}: {exc}')

project = ET.parse(ROOT / 'MediumClass.csproj').getroot()
expected_packages = {'AssemblyPublicizer': '1.0.2', 'ILRepack.MSBuild.Task': '2.0.13', 'WW-Blueprint-Core': '2.8.7'}
actual_packages = {p.attrib['Include']: p.attrib.get('Version') for p in project.iter('PackageReference')}
require(actual_packages == expected_packages, 'Build package pins differ from the reviewed target.')
require(project.find('.//TargetFramework').text == 'net472', 'Unexpected target framework.')
for hint in project.iter('HintPath'):
    require(not re.search(r'[A-Za-z]:[\\/]', hint.text or ''), 'Absolute machine path in project reference.')
for target in project.findall('Target'):
    if target.attrib['Name'] in ('DeployMod', 'PackageMod', 'StageMod'):
        require('AfterTargets' not in target.attrib and 'BeforeTargets' not in target.attrib,
                'Packaging/deployment must be explicitly invoked.')
require(project.find("Target[@Name='Publicize']").attrib.get('BeforeTargets') == 'ResolveAssemblyReferences',
        'Publicized references must be refreshed before resolving assemblies.')
info = json.loads((ROOT / 'Info.json').read_text(encoding='utf-8-sig'))
for key, value in baseline['mod_identity'].items():
    require(info.get(key) == value, 'Mod save identity changed: ' + key)

if args.syntax:
    try:
        from tree_sitter import Language, Parser
        import tree_sitter_c_sharp
        csharp = Parser(Language(tree_sitter_c_sharp.language()))
        for path in sources:
            if 'obj' in path.relative_to(ROOT).parts or 'bin' in path.relative_to(ROOT).parts:
                continue
            require(not csharp.parse(path.read_bytes()).root_node.has_error, 'C# syntax errors: ' + str(path.relative_to(ROOT)))
    except ImportError:
        errors.append('--syntax requires: python -m pip install -r scripts/requirements-checks.txt')

for message in warnings:
    print('WARNING:', message)
if args.strict and warnings:
    errors.append('Strict source check failed: legacy TypeId collisions are unresolved.')
for message in errors:
    print('ERROR:', message)
print(f'Offline checks: {len(errors)} errors, {len(warnings)} known TypeId collisions; no compilation or game test was performed.')
sys.exit(1 if errors else 0)
