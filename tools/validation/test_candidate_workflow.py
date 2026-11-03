"""Synthetic tooling guards only. Never launches Unity or writes protected fixtures."""
from pathlib import Path
import copy, importlib.util, json, os, sys, unittest, uuid
from unittest.mock import patch
import xml.etree.ElementTree as ET
sys.dont_write_bytecode=True
B=Path(__file__).resolve().parent
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
r=module('candidate_runner',B/'run.py');p=module('candidate_player',B/'player.py')
OUT=r.AUTH/'Synthetic'/uuid.uuid4().hex
OUT.mkdir(parents=True)

def good_post():
    return dict(passed=True,validation=True,exit=0,error=None,protectionChanges=[],sourceChanges=[],projectChanges=[],referenceChanges=False,dependencyChanges=[])

class WorkflowGuards(unittest.TestCase):
    def test_expected_assembly_messages_are_recorded_only_in_editor_reload(self):
        review=r.load(B/'assembly-diagnostics.json')
        lines=['Assembly '+n+'.dll at '+v['path']+' not valid. Loading of assembly skipped.' for n,v in review['expected'].items()]
        text='Begin MonoManager ReloadAssembly\n'+'\n'.join(lines)+'\n- Finished resetting the current domain, in 1 seconds\n'
        for phase in ('tests','build'):
            rows=r.classify_assembly_diagnostics(text,phase,review)
            self.assertEqual([x['diagnostic'] for x in rows],lines)
            self.assertTrue(all(x['classification']=='expected-not-built' and x['reason'] for x in rows))
        self.assertEqual(r.classify_assembly_diagnostics('No diagnostics','tests',review),[])

    def test_required_unknown_or_player_assembly_failure_is_never_classified(self):
        review=r.load(B/'assembly-diagnostics.json')
        for name in ('Assembly-CSharp','Assembly-CSharp-Editor','Unity.VisualScripting.DocCodeExamples','Unreviewed.Tests'):
            text='Begin MonoManager ReloadAssembly\nAssembly '+name+'.dll at Library/ScriptAssemblies/'+name+'.dll not valid. Loading of assembly skipped.\n- Finished resetting the current domain, in 1 seconds'
            with self.assertRaises(ValueError):r.classify_assembly_diagnostics(text,'tests',review)
        n=next(iter(review['expected']));line='Assembly '+n+'.dll at '+review['expected'][n]['path']+' not valid. Loading of assembly skipped.'
        for text,phase in [(line,'tests'),('Begin MonoManager ReloadAssembly\n'+line,'tests'),('Begin MonoManager ReloadAssembly\n'+line+'\n- Finished resetting the current domain, in 1 seconds','capture')]:
            with self.assertRaises(ValueError):r.classify_assembly_diagnostics(text,phase,review)
        self.assertTrue(p.player_diagnostics(line))

    def test_incomplete_or_changed_assembly_classification_fails(self):
        original=r.load(B/'assembly-diagnostics.json');n=next(iter(original['expected']))
        text='Begin MonoManager ReloadAssembly\nAssembly '+n+'.dll at '+original['expected'][n]['path']+' not valid. Loading of assembly skipped.\n- Finished resetting the current domain, in 1 seconds'
        for key,value in [('classification','required'),('path','Library/PlayerScriptAssemblies/'+n+'.dll'),('reason','')]:
            changed=copy.deepcopy(original);changed['expected'][n][key]=value
            with self.assertRaises(ValueError):r.classify_assembly_diagnostics(text,'tests',changed)
        with self.assertRaises(ValueError):r.classify_assembly_diagnostics(text.replace('Library/ScriptAssemblies','Library/PlayerScriptAssemblies'),'build',original)

    def test_actual_assembly_configuration_and_required_outputs_are_checked(self):
        pin={'assemblyReviewSHA256':r.safe.digest(B/'assembly-diagnostics.json')}
        original=r.verify_assembly_context(pin)
        with patch.object(r.safe,'digest',return_value='changed'):
            with self.assertRaises(ValueError):r.verify_assembly_context(pin)
        real_load=r.load
        for key,value in [('unityVersion','other'),('editorGraphSHA256','changed'),('configFiles',{}),('editorRequired',[])]:
            changed=copy.deepcopy(original);changed[key]=value
            with patch.object(r,'load',side_effect=lambda path:changed if Path(path).name=='assembly-diagnostics.json' else real_load(path)):
                with self.assertRaises(ValueError):r.verify_assembly_context(pin)
        with patch.object(Path,'is_file',return_value=False):
            with self.assertRaises(ValueError):r.verify_assembly_context(pin)

    def test_assembly_classification_never_masks_compile_or_load_errors(self):
        log=OUT/'assembly-negative.log'
        for line in ('Library/Foo.cs(1): error CS0001: failed','FileNotFoundException: missing','Could not load file or assembly Required'):
            log.write_text('Initialize engine version: 6000.4.7f1 (f3c3c4248748)\n'+line)
            with self.assertRaises(ValueError):r.validate_native_log(log,{},'tests')

    def test_actual_per_phase_environment_is_normal_and_isolated(self):
        token='f'*32;storage=r.ROOT/'S'/token;project=r.ROOT/'Project'
        for phase,mode in [('focused','tests'),('tests','tests'),('build','build'),('capture','capture')]:
            env=r.phase_environment(phase,storage,token,None if phase=='capture' else project)
            self.assertEqual(env['SPORE_V1_PROFILE'],'normal');self.assertEqual(env['SPORE_V1_DEVTOOLS'],'0')
            self.assertEqual(env['SPORE_TEST_TOKEN'],token);self.assertEqual(env['SPORE_TEST_ROOT'],str(storage));self.assertEqual(env['SPORE_STAGE60_MODE'],mode)
            self.assertEqual('SPORE_STAGE61_CAPTURE' in env,phase=='capture')
            r.validate_phase_environment(phase,env,storage,token,None if phase=='capture' else project)
            for change in [{'SPORE_V1_PROFILE':'development','SPORE_V1_DEVTOOLS':'1'},{'SPORE_TEST_ROOT':'C:/real-save'},{'SPORE_STAGE61_CAPTURE':'movement'},{'SPORE_V1_DEVTOOLS':'1'}]:
                with self.assertRaises(ValueError):r.validate_phase_environment(phase,dict(env,**change),storage,token,None if phase=='capture' else project)

    def test_frozen_plan_contains_exact_phase_environment(self):
        pin=r.load(B/r.BUILD_PIN);token='9'*32
        with patch.object(r,'load_contract',return_value=pin),patch.object(r,'validate_owner'):
            for phase in ('focused','tests','build'):
                plan=r.plan_phase(phase,token,{})
                self.assertEqual(plan['environmentOverrides'],r.phase_environment(phase,r.ROOT/'S'/token,token,r.ROOT/'Project'))
                self.assertEqual(plan['hostTimeoutSeconds'],1800)
            cap=p.plan_capture(token,plan)
            self.assertEqual(cap['environmentOverrides']['SPORE_V1_DEVTOOLS'],'0');self.assertNotIn('SPORE_STAGE60_PROJECT',cap['environmentOverrides'])

    def test_short_path_budget_includes_temporary_suffix(self):
        token='8'*32
        r.require_path_headroom([r.ROOT/'S'/token/'Stage61D'/token/('campaign-c.json.pending-'+token)])
        with self.assertRaises(ValueError):r.require_path_headroom(['C:/'+('a'*234)])
        with self.assertRaises(ValueError):r.phase_environment('tests',r.ROOT/'R'/token,token,r.ROOT/'Project')
        with self.assertRaises(ValueError):r.phase_environment('tests',r.ROOT/'S'/'short','short',r.ROOT/'Project')
        with self.assertRaises(ValueError):r.phase_environment('unreviewed',r.ROOT/'S'/token,token,r.ROOT/'Project')

    def test_focused_inventory_completeness_is_strict(self):
        names=r.load(r.AUTH/'focused-inventory.json')['activeCases']
        root=ET.Element('test-run',result='Passed',total='3',passed='3',failed='0',inconclusive='0',skipped='0',**{'start-time':'start','end-time':'end'})
        for n in names:ET.SubElement(root,'test-case',fullname=n,result='Passed',runstate='Runnable',**{'start-time':'start','end-time':'end'})
        path=OUT/'focused.xml'
        def check(tree,code=0):ET.ElementTree(tree).write(path);return r.validate_focused_result(path,code)
        check(root)
        for key,value in [('result','Skipped'),('runstate','Explicit'),('end-time',''),('fullname','unexpected')]:
            altered=copy.deepcopy(root);altered[0].set(key,value)
            with self.assertRaises(ValueError):check(altered)
        altered=copy.deepcopy(root);altered.remove(altered[0])
        with self.assertRaises(ValueError):check(altered)
        altered=copy.deepcopy(root);altered.append(copy.deepcopy(altered[0]))
        with self.assertRaises(ValueError):check(altered)
        with self.assertRaises(ValueError):check(root,2)

    def test_package_validation_errors_are_not_suppressed(self):
        path=OUT/'native.log';pin={'packageDLL':{'relative':'absent','sha256':'bad'}}
        for line in ['DirectoryNotFoundException: missing','Assembly Example.dll not valid. Loading of assembly skipped.','Assets/A.cs(1): error CS0001: wrong']:
            path.write_text('Initialize engine version: 6000.4.7f1\n'+line)
            with self.assertRaises(ValueError):r.validate_native_log(path,pin)

    def test_only_normal_profile(self):
        p.validate_request('capture','normal','normal')
        for a in [('play','normal','normal'),('capture','normal','development'),('capture','movement','development')]:
            with self.assertRaises(ValueError):p.validate_request(*a)

    def test_failed_prebuild_blocks_build(self):
        r.require_passed(good_post())
        for key,value in [('passed',False),('validation',False),('exit',-1),('error','timeout'),('protectionChanges',['source']),('referenceChanges',True)]:
            post=good_post();post[key]=value
            with self.assertRaises(ValueError):r.require_passed(post)

    def test_missing_postflight_fields_rejected(self):
        for key in good_post():
            post=good_post();del post[key]
            with self.assertRaises(ValueError):r.require_passed(post)

    def test_normal_complete_sequence_not_subset(self):
        checks=['PASS start','PASS loop','PASS loop','PASS finish']
        result=dict(finished=True,error='',runtimeErrors=[],checks=checks)
        p.validate_result(result,checks)
        for altered in [checks[:-1],checks+['PASS extra'],checks[::-1],['PASS start','PASS loop','PASS finish'],['FAIL start']+checks[1:]]:
            with self.assertRaises(ValueError):p.validate_result(dict(result,checks=altered),checks)

    def test_normal_errors_and_incomplete_rejected(self):
        result=dict(finished=True,error='',runtimeErrors=[],checks=['PASS done'])
        for key,value in [('finished',False),('error','failure'),('runtimeErrors',['assert'])]:
            with self.assertRaises(ValueError):p.validate_result(dict(result,**{key:value}),['PASS done'])
        with self.assertRaises(FileNotFoundError):r.load(OUT/'absent-result.json')

    def test_player_exception_and_error_log_not_exempted(self):
        self.assertEqual(p.player_diagnostics('normal log'),[])
        self.assertTrue(p.player_diagnostics('NullReferenceException: failed\nat Main'))
        self.assertTrue(p.player_diagnostics('UnityEngine.Debug:LogError (object)'))

    def test_identity(self):
        settings=(r.SOURCE/'ProjectSettings/ProjectSettings.asset').read_text();connect=(r.SOURCE/'ProjectSettings/UnityConnectSettings.asset').read_text();token='a'*32
        s,c=r.identity_text(settings,connect,token);r.validate_identity(s,c,token)
        for bad in [s.replace('SporeGobboSafety','DefaultCompany'),s.replace('cloudProjectId: ','cloudProjectId: actual'),s.replace('productGUID: '+token,'productGUID: bad')]:
            with self.assertRaises(ValueError):r.validate_identity(bad,c,token)
        with self.assertRaises(ValueError):r.validate_identity(s,c.replace('m_Enabled: 0','m_Enabled: 1'),token)

    def test_storage_and_path_escape(self):
        token='b'*32;storage=OUT/token;storage.mkdir();(storage/'.spore-owned').write_text(token);r.validate_storage(storage,token)
        (storage/'.spore-owned').write_text('wrong')
        with self.assertRaises(ValueError):r.validate_storage(storage,token)
        with self.assertRaises(FileNotFoundError):r.validate_storage(OUT/('c'*32),'c'*32)
        with self.assertRaises(ValueError):r.safe.within(OUT,'../escape')

    def test_wrong_owner_and_disabled_scope(self):
        with patch.object(r.getpass,'getuser',return_value='unreviewed-owner'):
            with self.assertRaises(ValueError):r.load_contract('build')
        with self.assertRaises(ValueError):r.load_contract('physical-witness')

    def test_stale_source_and_dependency_pin(self):
        with patch.object(r.getpass,'getuser',return_value='ezpay'),patch.object(r.safe,'project_snapshot',return_value={}):
            with self.assertRaises(ValueError):r.load_contract('build')
        with self.assertRaises(ValueError):r.check_dependencies({'files':{'Project/tools/validation/run.py':'0'*64}})

    def test_build_binary_and_provenance(self):
        root=OUT/'fake-build';folder=root/'Build';folder.mkdir(parents=True);(folder/'SporeGobboV1.exe').write_bytes(b'SYNTHETIC NOT EXECUTABLE')
        test=OUT/'fake-prebuild';test.mkdir();r.emit(test/'postflight.json',good_post());r.emit(root/'postflight.json',good_post())
        source={'asset':'hash'};plan=dict(owned=str(root),storage=str(root),token='d'*32,stagedHashes={'isolated':'hash'})
        flow=dict(build=plan,tests={'owned':str(test)})
        build=dict(run=str(root),folder=str(folder),sourceHashes=source,identityToken='d'*32,stagedHashes=plan['stagedHashes'],executionPinSHA256=r.safe.digest(B/r.BUILD_PIN),files=r.safe.snapshot(folder),postflightSHA256=r.safe.digest(root/'postflight.json'),prebuildRun=str(test),testInventorySHA256=r.safe.digest(B/'inventories/regression.json'))
        with patch.object(p.base,'load_contract',return_value={'sourceHashes':source}),patch.object(p.safe,'project_snapshot',return_value=source):
            p.validate_build(build,flow)
            for k,v in [('sourceHashes',{}),('executionPinSHA256','wrong'),('identityToken','bad'),('postflightSHA256','bad'),('testInventorySHA256','bad')]:
                with self.assertRaises(ValueError):p.validate_build(dict(build,**{k:v}),flow)
            (folder/'SporeGobboV1.exe').write_bytes(b'ALTERED')
            with self.assertRaises(ValueError):p.validate_build(build,flow)

    def test_owned_timeout_is_failure(self):
        self.assertEqual(r.safe.execute_owned([sys.executable,'-B','-c','import time;time.sleep(5)'],os.environ.copy(),.1),-1)

    def test_session_lock_released_on_exception(self):
        folder=OUT/'session';folder.mkdir()
        with self.assertRaises(RuntimeError):
            with r.safe.development_session(folder):raise RuntimeError('Synthetic setup failure')
        with r.safe.development_session(folder):pass

    def test_partial_setup_preserved_and_postflight_fails(self):
        # Throw after owned output exists, before any simulation. Production finally executes.
        root=OUT/'partial';root.mkdir();project=root/'Project';project.mkdir();(project/'ProjectSettings').mkdir();owned=root/'Run';storage=owned/'Storage'/('e'*32)
        r.emit(root/'project-state.json',{'hashes':{}})
        plan=dict(project=str(project),owned=str(owned),storage=str(storage),token='e'*32,phase='tests',previousProjectHashes={},sourceHashes={},stagedHashes={},isolatedSettings=['x','y'])
        before={'source':{},'reflogHex':''};pin={'sourceHashes':{},'files':{}}
        with patch.object(r,'ROOT',root),patch.object(r,'load_contract',return_value=pin),patch.object(r,'validate_owner'),patch.object(r,'process_preflight'),patch.object(r,'require_headroom'),patch.object(r,'plan_phase',return_value=plan),patch.object(r.safe,'project_snapshot',return_value={}),patch.object(r,'verify_references',return_value={}),patch.object(r,'capture_protection',return_value=before),patch.object(r,'validate_storage',side_effect=ValueError('Synthetic partial setup')),patch.object(r.safe,'execute_owned') as execute:
            post=r.execute_phase(plan)
        self.assertFalse(post['passed']);self.assertFalse(post['launched']);execute.assert_not_called();self.assertTrue((owned/'postflight.json').exists());self.assertTrue(storage.exists())

    def test_reflog_exception_stays_narrow(self):
        before={'source':{},'reflogHex':b'original\n'.hex()};commit=b'670a7bec8b366dc753331490d9c4cb56dd9895f7'
        line=commit+b' '+commit+b' User <u@example.invalid> 1 +0000\tremote set-head\n'
        self.assertEqual(r.protection_changes(before,dict(before,reflogHex=(b'original\n'+line).hex())),[])
        for data in [line,b'original\n'+line.replace(b'remote set-head',b'fetch'),b'original\n'+line.replace(commit,b'0'*40)]:
            self.assertTrue(r.protection_changes(before,dict(before,reflogHex=data.hex())))

    def test_inventory_parser_fails_closed(self):
        inventory=r.load(B/'inventories/regression.json')['activeCases']
        root=ET.Element('test-run',result='Passed',total='412',passed='412',failed='0',inconclusive='0',skipped='0',**{'start-time':'start','end-time':'end'})
        for n in inventory:ET.SubElement(root,'test-case',fullname=n,result='Passed',runstate='Runnable',**{'start-time':'start','end-time':'end'})
        target=OUT/'synthetic.xml'
        def write(tree):ET.ElementTree(tree).write(target,encoding='utf-8')
        write(root);r.validate_result(target,0,B/'inventories/regression.json','regression')
        for key,value in [('result','Skipped'),('runstate','Explicit'),('end-time','')]:
            altered=copy.deepcopy(root);altered[0].set(key,value);write(altered)
            with self.assertRaises(ValueError):r.validate_result(target,0,B/'inventories/regression.json','regression')
        altered=copy.deepcopy(root);altered.remove(altered[0]);write(altered)
        with self.assertRaises(ValueError):r.validate_result(target,0,B/'inventories/regression.json','regression')
        write(root)
        with self.assertRaises(ValueError):r.validate_result(target,-1,B/'inventories/regression.json','regression')

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(WorkflowGuards)
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    r.emit(OUT/'result.json',dict(passed=result.wasSuccessful(),tests=result.testsRun,failures=len(result.failures),errors=len(result.errors),syntheticOnly=True))
    print('Synthetic evidence: '+str(OUT))
    sys.exit(0 if result.wasSuccessful() else 1)
