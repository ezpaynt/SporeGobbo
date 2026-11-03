"""Reviewed tools-OFF normal capture only. Interactive delivery stays disabled."""
from pathlib import Path
import argparse,importlib.util,os,re,sys
sys.dont_write_bytecode=True
B=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('stage64d_current_runner',B/'run.py')
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
safe=base.safe;load=base.load;emit=base.emit
ROOT=base.ROOT;RUNS=base.RUNS;AUTH=base.AUTH

def validate_request(mode,scenario,profile):
    if (mode,scenario,profile)!=('capture','normal','normal'):raise ValueError('Only tools-OFF normal capture enabled; no interactive delivery')

def validate_result(result,required):
    if result.get('finished') is not True or result.get('error')!='' or result.get('runtimeErrors')!=[]:raise ValueError('Journey incomplete/runtime error')
    if result.get('checks')!=required or not required or any(not c.startswith('PASS ') for c in required):raise ValueError('Journey checks missing, duplicated, reordered or failed')

def player_diagnostics(log):
    # Existing inspected classifier narrowed to normal; no deliberate-refusal exemption.
    fatal=re.findall(r'(?m)^[A-Za-z_][\w.`+]*Exception:[^\r\n]*',log)
    if re.search(r'UnityEngine\.Debug:LogError\b',log):fatal.append('Debug.LogError in player')
    if re.search(r'(?m)^Assembly .*not valid|Could not load file or assembly',log):fatal.append('Assembly failure in player; no Editor classification applies')
    return sorted(set(fatal))

def validate_build(build,flow):
    pin=base.load_contract('build');plan=flow['build'];folder=Path(build['folder'])
    if build['sourceHashes']!=pin['sourceHashes'] or build['sourceHashes']!=safe.project_snapshot(base.SOURCE):raise ValueError('Stale build source')
    if build['run']!=plan['owned'] or folder!=Path(plan['storage'])/'Build' or build['identityToken']!=plan['token'] or build['stagedHashes']!=plan['stagedHashes']:raise ValueError('Not the fresh owned build')
    if build['executionPinSHA256']!=safe.digest(B/base.BUILD_PIN):raise ValueError('Stale build pin')
    safe.no_links(folder)
    if not folder.is_dir() or safe.snapshot(folder)!=build['files'] or not (folder/'SporeGobboV1.exe').is_file():raise ValueError('Binary/content changed')
    post=Path(build['run'])/'postflight.json'
    if safe.digest(post)!=build['postflightSHA256']:raise ValueError('Build postflight changed')
    base.require_passed(load(post));base.require_passed(load(Path(build['prebuildRun'])/'postflight.json'))
    if build['prebuildRun']!=flow['tests']['owned'] or build['testInventorySHA256']!=safe.digest(B/'inventories/regression.json'):raise ValueError('Prebuild provenance changed')
    return folder

def plan_capture(token,build_plan):
    if not re.fullmatch('[0-9a-f]{32}',token):raise ValueError('Invalid token')
    owned=safe.within(RUNS,token);storage=safe.within(ROOT/'S',token)
    if owned.exists() or storage.exists():raise ValueError('Never reuse runtime output/campaign')
    folder=Path(build_plan['storage'])/'Build'
    return dict(token=token,owned=str(owned),storage=str(storage),hostTimeoutSeconds=1800,internalTimeoutSeconds=900,mode='capture',scenario='normal',profile='normal',environmentOverrides=base.phase_environment('capture',storage,token),command=[str(folder/'SporeGobboV1.exe'),'-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-force-d3d11','-logFile',str(owned/'player.log')])

def main():
    args=argparse.ArgumentParser();args.add_argument('--mode',required=True);args.add_argument('--scenario',required=True);args.add_argument('--profile',required=True);a=args.parse_args()
    if sys.flags.optimize:raise ValueError('Optimized Python guards forbidden')
    validate_request(a.mode,a.scenario,a.profile)
    pin=base.load_contract('normal');flow=load(AUTH/'frozen-workflow.json');base.validate_authorization(flow);plan=flow['capture']
    if plan!=plan_capture(plan['token'],flow['build']) or pin['internalTimeoutSeconds']!=900:raise ValueError('Frozen runtime plan/deadline changed')
    build=load(AUTH/'build.json');folder=validate_build(build,flow)
    base.validate_owner(ROOT,base.owner_record())
    base.process_preflight();base.require_headroom(ROOT)
    before=base.capture_protection();references=base.verify_references(base.REFERENCES)
    emit(AUTH/'normal-capture-claim.json',dict(token=plan['token'],maximumLaunches=1,buildManifestSHA256=safe.digest(AUTH/'build.json')))
    owned=Path(plan['owned']);storage=Path(plan['storage']);owned.mkdir();(owned/'.spore-owned').write_text(plan['token'])
    emit(owned/'preflight.json',dict(plan=plan,protected=before,references=references,buildManifestSHA256=safe.digest(AUTH/'build.json')))
    code=-1;valid=False;error=None;fatal=[];launched=False
    try:
        storage.mkdir(parents=True);(storage/'.spore-owned').write_text(plan['token']);base.validate_storage(storage,plan['token'])
        env={k:v for k,v in os.environ.items() if not k.startswith('SPORE_')}
        env.update(plan['environmentOverrides'])
        base.validate_phase_environment('capture',env,storage,plan['token'])
        base.check_dependencies(pin);base.process_preflight()
        with safe.development_session(storage):
            launched=True;code=safe.execute_owned(plan['command'],env,1800)
        base.process_preflight();base.require_headroom(ROOT)
        fatal=player_diagnostics((owned/'player.log').read_text(encoding='utf-8-sig',errors='replace'))
        if code!=0 or fatal:raise ValueError('Player exit/log failed')
        validate_result(load(storage/'Evidence/result.json'),pin['requiredChecks']);valid=True
    except Exception as ex:error=repr(ex)
    finally:
        try:
            after=base.capture_protection();dependencies=[n for n,h in pin['files'].items() if safe.digest(safe.within(base.W,n))!=h]
            post=dict(validation=valid,exit=code,error=error,launched=launched,protectionChanges=base.protection_changes(before,after),sourceChanges=safe.differences(pin['sourceHashes'],safe.project_snapshot(base.SOURCE)),projectChanges=[],referenceChanges=base.verify_references(base.REFERENCES)!=references,dependencyChanges=dependencies,binaryChanges=safe.differences(build['files'],safe.snapshot(folder)),fatalPlayerDiagnostics=fatal,reflogBeforeHex=before['reflogHex'],reflogAfterHex=after['reflogHex'])
            post['passed']=valid and code==0 and error is None and not any(post[k] for k in ('protectionChanges','sourceChanges','referenceChanges','dependencyChanges','binaryChanges','fatalPlayerDiagnostics'))
            emit(owned/'postflight.json',post)
        except Exception as ex:emit(owned/'incomplete-postflight.json',dict(passed=False,error=repr(ex),originalError=error));raise
    print(__import__('json').dumps(dict(run=str(owned),passed=post['passed'],validation=valid,error=error),indent=2),flush=True)
    if not post['passed']:raise SystemExit(1)

if __name__=='__main__':main()
