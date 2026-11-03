"""Maintained, fail-closed current-candidate build gate. No historical runner import.
One authorized fixed regression -> conditional build; player.py owns normal capture.
"""
from pathlib import Path
import argparse, getpass, hashlib, importlib.util, json, os, re, shutil, subprocess, sys
sys.dont_write_bytecode = True
B=Path(__file__).resolve().parent; SOURCE=B.parents[1]; W=SOURCE.parent
sys.path.insert(0,str(B))
import safety as safe
from archived_references import verify_references
from acceptance_results import validate_result
OPERATIONS=W/'Operations'; PROFILES=OPERATIONS/'Profiles'
ROOT=Path(safe.MACHINE['validation']); RUNS=ROOT/'R'
BUILD_PIN=PROFILES/'execution-source-cutover.json'
AUTH=safe.within(OPERATIONS,safe.MACHINE['execution'])
REFERENCES=OPERATIONS/'References/reference-review.json'


def load(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def emit(path,value):
    with Path(path).open('x',encoding='utf-8') as stream: json.dump(value,stream,indent=2)

def load_contract(scope):
    profiles=load(PROFILES/'execution-profiles.json')
    if profiles.get('schema')!=1 or scope not in ('build','normal') or scope not in profiles['enabled']:
        raise ValueError('Scope disabled or unreviewed')
    entry=profiles['enabled'][scope]; path=safe.within(PROFILES,entry['pin'])
    if safe.digest(path)!=entry['sha256']: raise ValueError('Execution pin changed')
    pin=load(path)
    if pin['scope']!=scope or getpass.getuser().lower()!=pin['expectedOwner'].lower():
        raise ValueError('Scope/Windows owner mismatch')
    if pin['hostTimeoutSeconds']!=1800: raise ValueError('Host deadline changed')
    for name,expected in pin['files'].items():
        if safe.digest(safe.within(W,name))!=expected: raise ValueError('Execution dependency changed: '+name)
    if safe.project_snapshot(SOURCE)!=pin['sourceHashes']: raise ValueError('Candidate source changed')
    return pin

def process_preflight():
    script="Get-CimInstance Win32_Process | Where-Object { $_.Name -in @('Unity.exe','UnityPackageManager.exe','UnityShaderCompiler.exe','bee_backend.exe','UnityCrashHandler64.exe') -or $_.Name -match '^SporeGobbo.*\\.exe$' } | Select-Object ProcessId,Name,ExecutablePath,CommandLine | ConvertTo-Json -Compress"
    result=subprocess.run(['powershell.exe','-NoProfile','-NonInteractive','-Command',script],capture_output=True,text=True,timeout=30,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    if result.returncode or result.stderr.strip(): raise ValueError('Process ownership inspection failed')
    if result.stdout.strip(): raise ValueError('Competing Unity/player process: '+result.stdout)

def require_headroom(root):
    if shutil.disk_usage(root).free<5*1024**3: raise ValueError('Less than 5 GiB execution headroom; no cleanup authorized')
    # The cutover allowance is explicit private execution configuration, not a general cleanup rule.
    used=safe.MACHINE['budgetEvidenceAllowanceBytes']
    for entry in safe.MACHINE['budgetRoots']:
        total=0
        for base,dirs,files in os.walk(entry['path'],followlinks=False):
            for name in dirs+files:
                path=Path(base)/name
                if path.lstat().st_file_attributes&1024:
                    # Measurement never traverses links, including retained negative-test fixtures.
                    # Admission still independently rejects links through safe.no_links/within.
                    if name in dirs:dirs.remove(name)
            total+=sum((Path(base)/name).lstat().st_size for name in files if not (Path(base)/name).lstat().st_file_attributes&1024)
        used+=max(0,total-entry['baselineBytes'])
    if used>safe.MACHINE['additionalAllowanceBytes']: raise ValueError('Cutover additional working allowance exceeded; no cleanup authorized')


def check_dependencies(pin):
    for name,expected in pin['files'].items():
        if safe.digest(safe.within(W,name))!=expected: raise ValueError('Execution dependency drift: '+name)

def phase_environment(phase,storage,token,project=None):
    """Gameplay profile and physical isolation are independent, explicit per phase."""
    modes={'focused':'tests','tests':'tests','build':'build','capture':'capture'}
    if phase not in modes or not re.fullmatch('[0-9a-f]{32}',token): raise ValueError('Unknown phase/token')
    if Path(storage)!=safe.within(ROOT/'S',token): raise ValueError('Storage is not the reviewed short owned path')
    env=dict(SPORE_TEST_ROOT=str(storage),SPORE_TEST_TOKEN=token,SPORE_STAGE60_MODE=modes[phase],SPORE_V1_PROFILE='normal',SPORE_V1_DEVTOOLS='0',GIT_OPTIONAL_LOCKS='0')
    if phase=='capture':
        if project is not None: raise ValueError('Capture must not inherit Editor project state')
        env['SPORE_STAGE61_CAPTURE']='normal'
    else:
        if Path(project)!=ROOT/'Project': raise ValueError('Wrong validation project')
        env['SPORE_STAGE60_PROJECT']=str(project)
    return env

def validate_phase_environment(phase,env,storage,token,project=None):
    actual={k:v for k,v in env.items() if k.startswith('SPORE_') or k=='GIT_OPTIONAL_LOCKS'}
    if actual!=phase_environment(phase,storage,token,project): raise ValueError('Phase environment differs from reviewed profile/isolation')

def require_path_headroom(paths):
    for path in paths:
        if len(str(path))+24>=260: raise ValueError('Insufficient complete-path temporary-suffix headroom: '+str(path))

def validate_focused_result(path,exit_code):
    # Three-case scope only; original 412/100 parser remains byte-identical.
    from collections import Counter
    import xml.etree.ElementTree as ET
    names=load(AUTH/'focused-inventory.json')['activeCases']
    expected=['Stage64BLayoutTests.NormalProfileCannotSilentlyAdaptOldV1Campaign','Stage61PersistenceTests.InterruptedWritesKeepPrimaryAndBackupAndPreserveFailureEvidence','Stage64BLayoutTests.OldAuthenticatedFixtureReadThenAtomicCommitKeepsExactBackup']
    if names!=expected or exit_code!=0: raise ValueError('Focused inventory/exit invalid')
    root=ET.parse(path).getroot();leaves=list(root.iter('test-case'))
    if root.tag!='test-run' or Counter(n.get('fullname') for n in leaves)!=Counter(expected): raise ValueError('Focused cases/multiplicity differ')
    for n in [root,*root.iter('test-suite'),*leaves]:
        if n.get('result')!='Passed' or n.get('label') or n.get('runstate','Runnable')!='Runnable' or not n.get('start-time') or not n.get('end-time'): raise ValueError('Incomplete/nonpassing focused node')
        if n.tag!='test-case':
            count=len(list(n.iter('test-case')))
            if not count or any(n.get(k)!=str(v) for k,v in [('total',count),('passed',count),('failed',0),('inconclusive',0),('skipped',0)]): raise ValueError('Focused aggregate inconsistent')
    return dict(scope='profile-path',expected=3,executed=3,passed=3,skipped=0,inventoryMatched=True)

def verify_assembly_context(pin):
    path=B/'assembly-diagnostics.json'
    if safe.digest(path)!=pin['assemblyReviewSHA256']: raise ValueError('Assembly classification changed')
    review=load(path); project=ROOT/'Project'
    if review.get('schema')!=1 or review.get('unityVersion')!='6000.4.7f1' or review.get('phases')!=['tests','build'] or not review.get('expected') or not review.get('configFiles'): raise ValueError('Incomplete assembly classification')
    for name,h in review['configFiles'].items():
        if safe.digest(safe.within(project,name))!=h: raise ValueError('Assembly applicability changed: '+name)
    raw=load(safe.within(project,review['editorMetadata']))
    data=raw['ScriptCompilationBuildProgram.Data.ScriptCompilationData']
    if raw['BeeBuildProgramCommon.Data.ConfigurationData']['UnityVersion']!=review['unityVersion'] or data['BuildPlayerDataOutput']!='Library/BuildPlayerData/Editor': raise ValueError('Wrong compilation phase/version')
    graph=[{k:a[k] for k in ('Name','SourceFiles','References','PrebuiltReferences','Defines')} for a in data['Assemblies']]
    if hashlib.sha256(json.dumps(graph,sort_keys=True).encode()).hexdigest()!=review['editorGraphSHA256']: raise ValueError('Editor compilation graph changed')
    names=sorted(a['Name'] for a in graph)
    if names!=review['editorRequired'] or set(names).intersection(review['expected']): raise ValueError('Required/excluded assembly conflict')
    for name in names:
        if not (project/'Library/ScriptAssemblies'/(name+'.dll')).is_file(): raise ValueError('Missing required Editor assembly: '+name)
    return review

def classify_assembly_diagnostics(text,phase,review):
    if phase not in ('tests','build') or review.get('phases')!=['tests','build']: raise ValueError('No assembly exception for this phase')
    # Only completed Editor reload intervals qualify, including Editor startup in a build process.
    spans=[(m.start(),m.end()) for m in re.finditer(r'(?ms)^Begin MonoManager ReloadAssembly\r?\n.*?^- Finished resetting the current domain,[^\r\n]*',text)]
    expected=[]
    for m in re.finditer(r'(?m)^Assembly [^\r\n]*not valid[^\r\n]*',text):
        exact=re.fullmatch(r'Assembly ([\w.-]+)\.dll at (Library/ScriptAssemblies/[\w.-]+\.dll) not valid\. Loading of assembly skipped\.',m[0])
        rule=review['expected'].get(exact[1]) if exact else None
        if not rule or rule.get('classification')!='expected-not-built' or rule.get('path')!=exact[2] or not rule.get('reason') or not any(a<=m.start()<b for a,b in spans):
            raise ValueError('Unexpected or unproven assembly diagnostic: '+m[0])
        expected.append(dict(diagnostic=m[0],line=text[:m.start()].count('\n')+1,classification=rule['classification'],reason=rule['reason'],context='Editor reload',hostPhase=phase))
    return expected

def validate_native_log(path,pin,phase=None):
    text=Path(path).read_text(encoding='utf-8-sig',errors='replace')
    bad=re.findall(r'(?m)^[A-Za-z_][\w.`+]*Exception:[^\r\n]*|^.*\berror CS\d+:[^\r\n]*|^.*Could not load file or assembly[^\r\n]*',text)
    if bad: raise ValueError('Unexpected native assembly/compiler/exception diagnostics: '+str(bad[:5]))
    versions=[line for line in text.splitlines() if line.startswith('Initialize engine version: 6000.4.7f1 (f3c3c4248748)')]
    if not versions: raise ValueError('Actual Unity runtime version not evidenced')
    review=verify_assembly_context(pin)
    expected=classify_assembly_diagnostics(text,phase,review)
    dll=ROOT/'Project/Library/PackageCache'/pin['packageDLL']['relative']
    if not dll.is_file() or safe.digest(dll)!=pin['packageDLL']['sha256']: raise ValueError('Package DLL identity changed/missing')
    return dict(runtimeVersionEvidence=versions,packageDLL=str(dll),dllSHA256=safe.digest(dll),pathCharacters=len(str(dll)),unexpectedDiagnostics=bad,expectedDiagnostics=expected,requiredEditorAssemblies=len(review['editorRequired']),assemblyReviewSHA256=pin['assemblyReviewSHA256'])

def identity_text(settings, connect, token):
    if not re.fullmatch('[0-9a-f]{32}', token):
        raise ValueError('Invalid ownership token')
    values = [('companyName', 'SporeGobboSafety'), ('productName', 'Safety_' + token),
              ('productGUID', token), ('clonedFromGUID', ''), ('cloudProjectId', ''), ('organizationId', '')]
    for key, value in values:
        settings, count = re.subn(r'(?m)^  ' + key + r':.*$', '  ' + key + ': ' + value, settings)
        if count != 1:
            raise ValueError('Missing/duplicate identity key: ' + key)
    connect, count = re.subn(r'(?m)^(\s+m_(?:Enabled|InitializeOnStartup|EngineDiagnosticsEnabled|CaptureEditorExceptions):) [01]$', r'\1 0', connect)
    if not count:
        raise ValueError('UnityConnect isolation keys unavailable')
    validate_identity(settings, connect, token)
    return settings, connect


def validate_identity(settings, connect, token):
    expected = dict(companyName='SporeGobboSafety', productName='Safety_' + token,
                    productGUID=token, clonedFromGUID='', cloudProjectId='', organizationId='')
    if not re.fullmatch('[0-9a-f]{32}', token):
        raise ValueError('Invalid identity token')
    for key, value in expected.items():
        matches = re.findall(r'(?m)^  ' + key + r':([^\r\n]*)$', settings)
        if len(matches) != 1 or matches[0].strip() != value:
            raise ValueError('Unsafe disposable identity: ' + key)
    flags = re.findall(r'(?m)^\s+m_(?:Enabled|InitializeOnStartup|EngineDiagnosticsEnabled|CaptureEditorExceptions): ([01])$', connect)
    if not flags or any(value != '0' for value in flags):
        raise ValueError('UnityConnect/analytics isolation failed')


def owner_record():
    return dict(workspace=str(B), source=str(SOURCE), runRoot=str(RUNS),
                purpose='Stage64D single short disposable validation environment, never development source or installation')


def validate_owner(root, expected):
    safe.no_links(root)
    if not root.is_dir() or load(root / '.spore-stage64d-owner.json') != expected:
        raise ValueError('Unowned controlled environment')


def validate_storage(storage, token):
    safe.no_links(storage)
    if storage.name != token or not re.fullmatch('[0-9a-f]{32}', token) or (storage / '.spore-owned').read_text() != token:
        raise ValueError('Storage ownership mismatch')


def protection_roots(state):
    """Explicit role transition; absence is never an implicit retirement signal."""
    if state.get('schema') != 1 or state.get('mode') not in ('dual', 'retired-reference'):
        raise ValueError('Unknown protection state')
    rollback = [Path(p) for p in state['rollbackRoots']]
    if len(rollback) != len(set(rollback)):
        raise ValueError('Duplicate rollback protection')
    retired = None
    if state['mode'] == 'dual':
        roots = [SOURCE, safe.LIVE, *rollback]
    else:
        proof = state['retiredProof']
        path = safe.within(OPERATIONS, proof['descriptor'])
        if safe.digest(path) != proof['sha256']:
            raise ValueError('Retired-reference proof changed')
        retired = verify_references(path)
        if set(retired) != {'live.source', 'live.root', 'live.git'} or not all(retired.values()):
            raise ValueError('Incomplete live recovery proof')
        roots = [SOURCE, *rollback]
    for root in roots:
        if not root.is_absolute():
            raise ValueError('Protected roots must be absolute')
        safe.no_links(root)
        for name in ('Assets', 'Packages', 'ProjectSettings', '.git'):
            if not (root / name).is_dir():
                raise ValueError('Required protected path missing: ' + str(root / name))
    return roots, retired


def capture_protection():
    state = load(OPERATIONS / 'protection-state.json')
    roots, retired = protection_roots(state)
    git = {}
    reflog = safe.LIVE / '.git/logs/refs/remotes/origin/HEAD'
    protected = {}
    for root in roots:
        for relative, digest in safe.snapshot(root / '.git').items():
            path = root / '.git' / relative
            if path != reflog:
                git[str(path)] = digest
        protected[str(root)] = dict(source=safe.project_snapshot(root),
            root={p.name: safe.digest(p) for p in root.iterdir() if p.is_file()},
            docs=safe.snapshot(root / 'docs'), tools=safe.snapshot(root / 'tools'))
    if not safe.REAL_DATA.is_dir():
        raise ValueError('Required real-data protection root missing')
    preferences = safe.registry_snapshot()
    safe.validate_real_preferences(preferences)
    return dict(protected=protected, retiredReference=retired,
        saves=safe.snapshot(safe.REAL_DATA), preferences=preferences, git=git,
        reflogHex=reflog.read_bytes().hex() if state['mode'] == 'dual' else '')



def protection_changes(before, after):
    changes = [key for key in before if key != 'reflogHex' and before[key] != after.get(key)]
    old = bytes.fromhex(before['reflogHex']); new = bytes.fromhex(after['reflogHex'])
    if not new.startswith(old):
        changes.append('reflog rewrite')
    else:
        commit = b'670a7bec8b366dc753331490d9c4cb56dd9895f7'
        pattern = re.compile(rb'([0-9a-f]{40}) ([0-9a-f]{40}) [^\r\n\t]+ <[^<>\r\n]*> ([0-9]+) ([+-][0-9]{4})\tremote set-head\n')
        for line in new[len(old):].splitlines(keepends=True):
            match = pattern.fullmatch(line)
            if not match or match[1] != match[2] or match[1] != commit:
                changes.append('unapproved reflog append')
    return changes



def plan_phase(phase,token,previous):
    if phase not in ('focused','tests','build') or not re.fullmatch('[0-9a-f]{32}',token): raise ValueError('Invalid phase/token')
    pin=load_contract('build'); validate_owner(ROOT,owner_record())
    owned=safe.within(RUNS,token); project=ROOT/'Project'; storage=safe.within(ROOT/'S',token)
    if owned.exists() or storage.exists(): raise ValueError('Never reuse execution output/storage')
    texts=identity_text((SOURCE/'ProjectSettings/ProjectSettings.asset').read_text(encoding='utf-8-sig'),(SOURCE/'ProjectSettings/UnityConnectSettings.asset').read_text(encoding='utf-8-sig'),token)
    staged=dict(pin['sourceHashes'])
    for name,text in zip(('ProjectSettings.asset','UnityConnectSettings.asset'),texts):
        staged['ProjectSettings/'+name]=hashlib.sha256(text.replace('\n',os.linesep).encode()).hexdigest()
    command=[str(safe.UNITY),'-batchmode','-nographics','-projectPath',str(project)]
    if phase=='focused': command+=['-runTests','-testPlatform','EditMode','-testFilter',';'.join(load(AUTH/'focused-inventory.json')['activeCases']),'-testResults',str(owned/'results.xml')]
    elif phase=='tests': command+=['-runTests','-testPlatform','EditMode','-testFilter',pin['testFilter'],'-testCategory',pin['categoryFilter'],'-testResults',str(owned/'results.xml')]
    else: command+=['-quit','-executeMethod','Stage63Build.Build']
    command+=['-logFile',str(owned/'unity.log')]
    return dict(phase=phase,token=token,owned=str(owned),storage=str(storage),project=str(project),command=command,environmentOverrides=phase_environment(phase,storage,token,project),hostTimeoutSeconds=1800,sourceHashes=pin['sourceHashes'],previousProjectHashes=previous,stagedHashes=staged,isolatedSettings=list(texts),pinSHA256=safe.digest(B/BUILD_PIN),profilesSHA256=safe.digest(PROFILES/'execution-profiles.json'))

def require_passed(post):
    required={'passed','validation','exit','error','protectionChanges','sourceChanges','projectChanges','referenceChanges','dependencyChanges'}
    if not isinstance(post,dict) or not required.issubset(post): raise ValueError('Incomplete phase postflight')
    if not isinstance(post,dict) or post.get('passed') is not True or post.get('validation') is not True or post.get('exit')!=0 or post.get('error') is not None:
        raise ValueError('Preceding phase lacks complete clean success')
    for key in ('protectionChanges','sourceChanges','projectChanges','referenceChanges','dependencyChanges'):
        if key not in post or post[key]: raise ValueError('Preceding postflight incomplete/dirty: '+key)

def execute_phase(plan):
    pin=load_contract('build'); validate_owner(ROOT,owner_record()); process_preflight(); require_headroom(ROOT)
    project=Path(plan['project']); owned=Path(plan['owned']); storage=Path(plan['storage'])
    if plan!=plan_phase(plan['phase'],plan['token'],plan['previousProjectHashes']): raise ValueError('Frozen execution plan changed')
    actual=safe.project_snapshot(project)
    if actual!=plan['previousProjectHashes'] or actual!=load(ROOT/'project-state.json')['hashes']: raise ValueError('Disposable changed since reviewed staging')
    references=verify_references(REFERENCES); before=capture_protection()
    owned.mkdir(); (owned/'.spore-owned').write_text(plan['token']); emit(owned/'preflight.json',dict(plan=plan,protected=before,references=references))
    code=-1; valid=False; error=None; launched=False
    try:
        storage.mkdir(parents=True); (storage/'.spore-owned').write_text(plan['token']); validate_storage(storage,plan['token'])
        for name,text in zip(('ProjectSettings.asset','UnityConnectSettings.asset'),plan['isolatedSettings']):
            safe.within(project,'ProjectSettings/'+name).write_text(text,encoding='utf-8')
        validate_identity((project/'ProjectSettings/ProjectSettings.asset').read_text(),(project/'ProjectSettings/UnityConnectSettings.asset').read_text(),plan['token'])
        if safe.project_snapshot(project)!=plan['stagedHashes']: raise ValueError('Staged source differs from frozen plan')
        check_dependencies(pin); process_preflight()
        env={k:v for k,v in os.environ.items() if not k.startswith('SPORE_')}
        env.update(plan['environmentOverrides'])
        validate_phase_environment(plan['phase'],env,storage,plan['token'],project)
        with safe.development_session(storage):
            launched=True; code=safe.execute_owned(plan['command'],env,plan['hostTimeoutSeconds'])
        process_preflight() # A surviving child/tool process blocks the next phase.
        require_headroom(ROOT)
        emit(owned/'native-log-verification.json',validate_native_log(owned/'unity.log',pin,plan['phase']))
        if plan['phase']=='focused':
            emit(owned/'acceptance-completeness.json',validate_focused_result(owned/'results.xml',code))
        elif plan['phase']=='tests':
            emit(owned/'acceptance-completeness.json',validate_result(owned/'results.xml',code,B/'inventories/regression.json','regression'))
        else:
            folder=storage/'Build'; safe.no_links(folder)
            if code!=0 or (folder/'build-result.txt').read_text().strip()!='Succeeded' or not (folder/'SporeGobboV1.exe').is_file(): raise ValueError('Build receipt/exit/executable failed')
        valid=True
    except Exception as ex: error=repr(ex)
    finally:
        try:
            after=capture_protection(); actual=safe.project_snapshot(project)
            dependency_changes=[n for n,h in pin['files'].items() if safe.digest(safe.within(W,n))!=h]
            post=dict(validation=valid,exit=code,error=error,launched=launched,protectionChanges=protection_changes(before,after),sourceChanges=safe.differences(pin['sourceHashes'],safe.project_snapshot(SOURCE)),projectChanges=safe.differences(plan['stagedHashes'],actual),referenceChanges=verify_references(REFERENCES)!=references,dependencyChanges=dependency_changes,reflogBeforeHex=before['reflogHex'],reflogAfterHex=after['reflogHex'])
            post['passed']=valid and code==0 and error is None and not any(post[k] for k in ('protectionChanges','sourceChanges','projectChanges','referenceChanges','dependencyChanges'))
            emit(owned/'postflight.json',post)
            if not any(post[k] for k in ('protectionChanges','sourceChanges','projectChanges','referenceChanges','dependencyChanges')):
                (ROOT/'project-state.json').write_text(json.dumps(dict(hashes=actual,prior=str(owned),passed=post['passed']),indent=2))
        except Exception as ex:
            emit(owned/'incomplete-postflight.json',dict(passed=False,error=repr(ex),originalError=error)); raise
    print(json.dumps(dict(phase=plan['phase'],run=str(owned),passed=post['passed'],error=error)),flush=True)
    return post

def validate_authorization(flow):
    gates=load(AUTH/'non-unity-gates.json'); auth=load(AUTH/'execution-authorization.json')
    if gates.get('passed') is not True or safe.digest(AUTH/'non-unity-gates.json')!=auth['gatesSHA256'] or safe.digest(AUTH/'frozen-workflow.json')!=auth['workflowSHA256']:
        raise ValueError('Reviewed preparatory gates/plan unavailable or changed')
    if flow!=load(AUTH/'frozen-workflow.json'): raise ValueError('Not the frozen workflow')
    for name,h in gates['reviewedFileHashes'].items():
        if safe.digest(safe.within(W,name))!=h: raise ValueError('Validated tooling changed: '+name)

def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--mode',required=True); parser.add_argument('--scope',required=True); options=parser.parse_args()
    if sys.flags.optimize or (options.mode,options.scope)!=('build','regression'): raise ValueError('Only reviewed build/regression continuation enabled')
    flow=load(AUTH/'frozen-workflow.json'); validate_authorization(flow); load_contract('build')
    if load(AUTH/'classification.json').get('passed') is not True: raise ValueError('Classification not established')
    emit(AUTH/'build-workflow-claim.json',dict(tests=flow['tests']['token'],build=flow['build']['token'],maximumLaunches=2))
    tests=execute_phase(flow['tests']); require_passed(tests)
    built=execute_phase(flow['build']); require_passed(built)
    plan=flow['build']; folder=Path(plan['storage'])/'Build'
    emit(AUTH/'build.json',dict(run=plan['owned'],folder=str(folder),sourceHashes=plan['sourceHashes'],stagedHashes=plan['stagedHashes'],identityToken=plan['token'],files=safe.snapshot(folder),prebuildRun=flow['tests']['owned'],testInventorySHA256=safe.digest(B/'inventories/regression.json'),postflightSHA256=safe.digest(Path(plan['owned'])/'postflight.json'),executionPinSHA256=plan['pinSHA256']))

if __name__=='__main__': main()
