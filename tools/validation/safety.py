"""Preserved controlled-run primitives; explicit private machine configuration."""
from pathlib import Path
import os,sys,json,hashlib,subprocess,re,contextlib,getpass
sys.dont_write_bytecode=True

def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def no_links(p):
 for q in [p,*p.parents]:
  try:attrs=getattr(q.lstat(),'st_file_attributes',0)
  except FileNotFoundError:attrs=0
  if q.is_symlink() or attrs&1024:raise RuntimeError('Linked path forbidden: '+str(q))


def within(root,child):
 if Path(child).is_absolute() or re.match(r'^[A-Za-z]:',str(child)):raise ValueError('Absolute child forbidden')
 target=Path(os.path.abspath(root/child));root=Path(os.path.abspath(root))
 if target==root or root not in target.parents:raise ValueError('Path escape')
 no_links(target);return target


def snapshot(root):
 result={}
 if root.exists():
  no_links(root)
  for p in root.rglob('*'):
   no_links(p)
   if p.is_file():result[p.relative_to(root).as_posix()]=digest(p)
 return result


def project_snapshot(root):return {f'{n}/{p}':v for n in ('Assets','Packages','ProjectSettings') for p,v in snapshot(root/n).items()}


def differences(a,b):return sorted(k for k in set(a)|set(b) if a.get(k)!=b.get(k))


def registry_snapshot():
 import winreg
 result={}
 def visit(path):
  try:k=winreg.OpenKey(winreg.HKEY_CURRENT_USER,path)
  except FileNotFoundError:return
  with k:
   i=0
   while True:
    try:n,v,t=winreg.EnumValue(k,i);result[path+'\\'+n]=[v.hex() if isinstance(v,bytes) else v,t];i+=1
    except OSError:break
   i=0
   while True:
    try:n=winreg.EnumKey(k,i);i+=1;visit(path+'\\'+n)
    except OSError:break
 for p in MACHINE['registryRoots']:visit(p)
 return result


def execute_owned(cmd,env,timeout):
 try:return subprocess.run(cmd,env=env,timeout=timeout,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0)).returncode
 except subprocess.TimeoutExpired:return -1


@contextlib.contextmanager
def development_session(storage):
 import msvcrt
 path=storage/'.spore-session-lock';no_links(path)
 with path.open('a+b') as handle:
  if handle.tell()==0:handle.write(b'0');handle.flush()
  handle.seek(0)
  try:msvcrt.locking(handle.fileno(),msvcrt.LK_NBLCK,1)
  except OSError as error:raise RuntimeError('Another protected player owns this development storage') from error
  try:yield
  finally:handle.seek(0);msvcrt.locking(handle.fileno(),msvcrt.LK_UNLCK,1)


PROJECT=Path(__file__).resolve().parents[2]
OPERATIONS=PROJECT.parent/'Operations'
no_links(OPERATIONS/'machine.json')
MACHINE=json.loads((OPERATIONS/'machine.json').read_text(encoding='utf-8-sig'))
if MACHINE.get('schema')!=1 or Path(MACHINE['project'])!=PROJECT or Path(MACHINE['operations'])!=OPERATIONS or getpass.getuser().lower()!=MACHINE['owner'].lower():
 raise ValueError('Missing/wrong machine identity; no historical fallback')
# Historical LIVE ancestry is checked by run.protection_roots in dual mode.
# Retired-reference mode must not read that retired path even during import.
if not Path(MACHINE['live']).is_absolute():raise ValueError('Historical live identity must be absolute')
for key in ('project','validation','operations','archive','unity','python','realData'):
 p=Path(MACHINE[key])
 if not p.is_absolute():raise ValueError('Machine path must be absolute: '+key)
 no_links(p)
LIVE=Path(MACHINE['live']);UNITY=Path(MACHINE['unity']);REAL_DATA=Path(MACHINE['realData'])
def validate_real_preferences(values):
 expected={}
 for root,record in MACHINE['preferenceBaseline'].items():
  for v in record['values']:expected[root+'\\'+v['name']]=(v['type'],v['valueHash'])
 actual={}
 for name,(v,t) in values.items():
  original=bytes.fromhex(v) if t==3 else v
  actual[name]=(t,hashlib.sha256(repr(original).encode()).hexdigest())
 if actual!=expected:raise ValueError('Real preferences differ from reviewed owner baseline')
