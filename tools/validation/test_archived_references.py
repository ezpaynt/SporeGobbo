"""Ten preserved archive/path guards; only task-owned synthetic data."""
from pathlib import Path
from unittest import mock
import hashlib,json,sys,unittest,uuid,zipfile
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import safety as safe
import archived_references as refs
O=safe.within(safe.OPERATIONS,safe.MACHINE['execution'])
def fresh():
    path = O / 'Synthetic' / uuid.uuid4().hex
    path.mkdir(parents=True)
    return path


def write(path, value):
    path.write_text(json.dumps(value, indent=2))


def synthetic_reference():
    root = fresh(); body = b'synthetic terrain source'
    with zipfile.ZipFile(root / 'source.zip', 'x') as z:
        z.writestr('Assets/unit.txt', body)
    seal = dict(files={'source.zip': dict(sha256=safe.digest(root / 'source.zip'))})
    write(root / 'seal.json', seal)
    expected = hashlib.sha256(body).hexdigest()
    review = dict(schema=1, archiveRoot=str(root), manifest='seal.json', manifestSHA256=safe.digest(root / 'seal.json'),
                  requiredChecks=['sample'], checks=[dict(name='sample', kind='source', baseArchive='source.zip', expected={'Assets/unit.txt': expected})])
    write(root / 'review.json', review)
    return root, review


class ReferenceTests(unittest.TestCase):
    def test_valid_reference(self):
        root, review = synthetic_reference()
        self.assertEqual(refs.verify_references(root / 'review.json')['sample'], review['checks'][0]['expected'])

    def test_missing_archive(self):
        root, review = synthetic_reference()
        review['checks'][0]['baseArchive'] = 'absent.zip'; write(root / 'review.json', review)
        with self.assertRaises((ValueError, FileNotFoundError)):
            refs.verify_references(root / 'review.json')

    def test_altered_archive(self):
        root, _ = synthetic_reference()
        with (root / 'source.zip').open('ab') as f: f.write(b'changed')
        with self.assertRaises(ValueError): refs.verify_references(root / 'review.json')

    def test_altered_manifest(self):
        root, _ = synthetic_reference(); (root / 'seal.json').write_text('{}')
        with self.assertRaises(ValueError): refs.verify_references(root / 'review.json')

    def test_missing_manifest(self):
        root, review = synthetic_reference(); review['manifest'] = 'absent.json'; write(root / 'review.json', review)
        with self.assertRaises(FileNotFoundError): refs.verify_references(root / 'review.json')

    def test_member_integrity(self):
        root, review = synthetic_reference()
        check = review['checks'][0]; check.update(kind='file', member=dict(archive='source.zip', member='Assets/unit.txt', sha256='0'*64), expected='0'*64)
        write(root / 'review.json', review)
        with self.assertRaises(ValueError): refs.verify_references(root / 'review.json')

    def test_missing_member(self):
        root, review = synthetic_reference()
        review['checks'][0].update(kind='file', member=dict(archive='source.zip', member='absent', sha256='0'*64), expected='0'*64)
        write(root / 'review.json', review)
        with self.assertRaises(KeyError): refs.verify_references(root / 'review.json')

    def test_wrong_reference_and_missing_or_duplicate_check(self):
        for condition in ('wrong', 'missing', 'duplicate'):
            with self.subTest(condition=condition):
                root, review = synthetic_reference()
                if condition == 'wrong': review['checks'][0]['expected'] = {}
                if condition == 'missing': review['checks'] = []
                if condition == 'duplicate': review['checks'] *= 2
                write(root / 'review.json', review)
                with self.assertRaises(ValueError): refs.verify_references(root / 'review.json')

    def test_path_escape(self):
        for name in ('../escape.zip', 'C:/escape.zip'):
            root, review = synthetic_reference(); review['checks'][0]['baseArchive'] = name; write(root / 'review.json', review)
            with self.assertRaises(ValueError): refs.verify_references(root / 'review.json')

    def test_native_reparse_rejected(self):
        link = O / 'Synthetic/reparse-link'
        self.assertTrue(link.lstat().st_file_attributes & 1024, 'Real task-owned junction required; not a mocked reparse proof')
        with self.assertRaises(RuntimeError): safe.within(link, 'file.txt')

import builtins,copy,importlib.util,io,os

def runner_module():
    spec=importlib.util.spec_from_file_location('migration_runner',Path(__file__).parent/'run.py')
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result

def protected_fixture(root):
    for name in ('Assets','Packages','ProjectSettings','.git'):
        (root/name).mkdir(parents=True,exist_ok=True)
        (root/name/'fixture.txt').write_text('synthetic '+name)
    return root

def retired_fixture(root):
    body=b'synthetic historical recovery';h=hashlib.sha256(body).hexdigest()
    with zipfile.ZipFile(root/'legacy.zip','x') as z:z.writestr('Assets/legacy.txt',body)
    write(root/'legacy-seal.json',{'files':{'legacy.zip':{'sha256':safe.digest(root/'legacy.zip')}}})
    checks=[dict(name=n,kind='files',members={'record':dict(archive='legacy.zip',member='Assets/legacy.txt',sha256=h)},expected={'record':h}) for n in ('live.source','live.root','live.git')]
    value=dict(schema=1,archiveRoot=str(root),manifest='legacy-seal.json',manifestSHA256=safe.digest(root/'legacy-seal.json'),requiredChecks=[x['name'] for x in checks],checks=checks)
    write(root/'retired.json',value)
    return dict(schema=1,mode='retired-reference',rollbackRoots=[],retiredProof=dict(descriptor='retired.json',sha256=safe.digest(root/'retired.json')))

class MigrationTests(unittest.TestCase):
    def test_machine_missing_wrong_owner_and_identity_fail_closed(self):
        path=Path(__file__).parent/'safety.py'
        def import_fresh():
            spec=importlib.util.spec_from_file_location('isolated_safety_guard',path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
        import_fresh()
        original=Path.read_text
        with mock.patch.object(Path,'read_text',side_effect=FileNotFoundError('synthetic missing descriptor')):
            with self.assertRaises(FileNotFoundError):import_fresh()
        for key,value in [('schema',99),('project','C:/wrong-project'),('operations','C:/wrong-operations'),('owner','unreviewed-owner')]:
            changed=dict(safe.MACHINE,**{key:value})
            with mock.patch.object(Path,'read_text',return_value=json.dumps(changed)):
                with self.assertRaises(ValueError):import_fresh()

    def test_dual_protection_requires_original_live_and_rollback(self):
        r=runner_module();root=fresh();candidate=protected_fixture(root/'candidate');live=protected_fixture(root/'live');rollback=protected_fixture(root/'rollback')
        state=dict(schema=1,mode='dual',rollbackRoots=[str(rollback)],retiredProof=None)
        with mock.patch.object(r,'SOURCE',candidate),mock.patch.object(r.safe,'LIVE',live):
            self.assertEqual(r.protection_roots(state)[0],[candidate,live,rollback])
            with mock.patch.object(r.safe,'LIVE',root/'absent'):
                with self.assertRaises(ValueError):r.protection_roots(state)
            with self.assertRaises(ValueError):r.protection_roots(dict(state,rollbackRoots=[str(root/'absent')]))
            with self.assertRaises(ValueError):r.protection_roots(dict(state,mode='implicitly-retired'))

    def test_retirement_requires_complete_unchanged_archive_proof(self):
        r=runner_module();root=fresh();candidate=protected_fixture(root/'candidate');state=retired_fixture(root)
        with mock.patch.object(r,'SOURCE',candidate),mock.patch.object(r,'OPERATIONS',root),mock.patch.object(r.safe,'LIVE',root/'absent-live'):
            roots,facts=r.protection_roots(state);self.assertEqual(roots,[candidate]);self.assertEqual(set(facts),{'live.source','live.root','live.git'})
            bad=copy.deepcopy(state);bad['retiredProof']['sha256']='0'*64
            with self.assertRaises(ValueError):r.protection_roots(bad)
            bad=copy.deepcopy(state);bad['retiredProof']['descriptor']='absent.json'
            with self.assertRaises(FileNotFoundError):r.protection_roots(bad)
            with mock.patch.object(r,'verify_references',return_value={'live.source':{'partial':'x'}}):
                with self.assertRaises(ValueError):r.protection_roots(state)
            with (root/'legacy.zip').open('ab') as stream:stream.write(b'corrupt')
            with self.assertRaises(ValueError):r.protection_roots(state)

    def test_save_and_preference_protection_survives_both_modes(self):
        r=runner_module();root=fresh();candidate=protected_fixture(root/'candidate');live=protected_fixture(root/'live');data=root/'real-data-sentinel';data.mkdir();(data/'fixture.json').write_text('synthetic protected sentinel')
        log=live/'.git/logs/refs/remotes/origin/HEAD';log.parent.mkdir(parents=True);log.write_bytes(b'fixture\n')
        retired=retired_fixture(root);dual=dict(schema=1,mode='dual',rollbackRoots=[],retiredProof=None)
        for state in (dual,retired):
            with mock.patch.object(r,'SOURCE',candidate),mock.patch.object(r,'OPERATIONS',root),mock.patch.object(r.safe,'LIVE',live),mock.patch.object(r.safe,'REAL_DATA',data),mock.patch.object(r,'load',return_value=state),mock.patch.object(r.safe,'registry_snapshot',return_value={'sentinel':['value',1]}) as registry,mock.patch.object(r.safe,'validate_real_preferences') as prefs:
                result=r.capture_protection();self.assertEqual(result['saves'],{'fixture.json':safe.digest(data/'fixture.json')});registry.assert_called_once();prefs.assert_called_once_with({'sentinel':['value',1]})
                with mock.patch.object(r.safe,'REAL_DATA',root/'absent'):
                    with self.assertRaises(ValueError):r.capture_protection()
                with mock.patch.object(r.safe,'validate_real_preferences',side_effect=ValueError('changed preferences')):
                    with self.assertRaises(ValueError):r.capture_protection()

    def test_operations_do_not_read_six_retirement_roots(self):
        r=runner_module()
        retired=[Path(x) for x in safe.MACHINE['retirementRoots']]
        originals=(builtins.open,io.open,os.scandir,os.stat)
        blocked=[]
        def guard(value):
            if isinstance(value,(str,bytes,os.PathLike)):
                path=Path(os.fsdecode(value)).absolute()
                if any(path==root or root in path.parents for root in retired):
                    blocked.append(str(path));raise PermissionError('Synthetic denied historical path: '+str(path))
        def wrap(function):
            def call(path,*args,**kwargs):guard(path);return function(path,*args,**kwargs)
            return call
        with mock.patch.object(builtins,'open',wrap(originals[0])),mock.patch.object(io,'open',wrap(originals[1])),mock.patch.object(os,'scandir',wrap(originals[2])),mock.patch.object(os,'stat',wrap(originals[3])):
            for root in retired:
                with self.assertRaises(PermissionError):(root/'AGENTS.md').read_text()
            blocked.clear()
            for scope in ('build','normal'):r.load_contract(scope)
            refs.verify_references(r.REFERENCES)
            plan=r.plan_phase('tests',uuid.uuid4().hex,{})
            self.assertEqual(Path(plan['project']),r.ROOT/'Project')
            self.assertEqual(blocked,[])
            # Explicitly separate intentionally retained rollback protection from execution dependencies.
            state=r.load(r.OPERATIONS/'protection-state.json')
            with self.assertRaises(PermissionError):r.protection_roots(state)
        self.assertTrue(any(str(retired[-1]) in name for name in blocked))

    def test_guarded_archive_references_are_exactly_preserved(self):
        r=runner_module();facts=refs.verify_references(r.REFERENCES)
        self.assertEqual(len(facts),14)
        self.assertEqual(set(facts),set(r.load(r.REFERENCES)['requiredChecks']))

    def test_original_inventory_and_safety_algorithms_preserved(self):
        r=runner_module();contract=r.load(r.OPERATIONS/'Evidence/migration-contract.json')
        for relative,expected in contract['byteIdenticalTools'].items():
            self.assertEqual(safe.digest(Path(__file__).parent/relative),expected)
        import ast
        tree=ast.parse((Path(__file__).parent/'test_candidate_workflow.py').read_text())
        actual=sorted(n.name for c in tree.body if isinstance(c,ast.ClassDef) for n in c.body if isinstance(n,ast.FunctionDef) and n.name.startswith('test_'))
        self.assertEqual(actual,contract['retainedGuardMethods']);self.assertEqual(len(actual),26)


if __name__=="__main__":
 unittest.main(verbosity=2)
