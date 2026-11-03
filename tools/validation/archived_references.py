"""Read the three fixed preserved references; never open their historical projects."""
from pathlib import Path
from contextlib import ExitStack
import hashlib
import json
import sys
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
import safety as safe


def load(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def verify_references(review_path):
    """Return exactly the facts checked by the former expanded-reference guards."""
    safe.no_links(Path(review_path))
    review = load(review_path)
    if review.get('schema') != 1:
        raise ValueError('Unknown archived reference contract')
    root = Path(review['archiveRoot'])
    safe.no_links(root)
    if not root.is_absolute():
        raise ValueError('Archive root must be absolute')
    manifest = safe.within(root, review['manifest'])
    if safe.digest(manifest) != review['manifestSHA256']:
        raise ValueError('Archive manifest changed')
    sealed = load(manifest)['files']
    facts = {}
    with ExitStack() as stack:
        opened = {}

        def archive(name):
            if name not in opened:
                path = safe.within(root, name)
                if name not in sealed or safe.digest(path) != sealed[name]['sha256']:
                    raise ValueError('Missing/changed sealed archive: ' + name)
                z = stack.enter_context(zipfile.ZipFile(path))
                names = z.namelist()
                if len(names) != len(set(names)):
                    raise ValueError('Duplicate archive member')
                for member in names:
                    safe.within(root, member)
                    if '\\' in member or (z.getinfo(member).external_attr >> 16) & 0o170000 == 0o120000:
                        raise ValueError('Unreviewed archive member shape')
                opened[name] = z
            return opened[name]

        def member_hash(entry):
            data = archive(entry['archive']).read(entry['member'])
            value = hashlib.sha256(data).hexdigest()
            if value != entry['sha256']:
                raise ValueError('Archived member changed: ' + entry['member'])
            return value

        for check in review['checks']:
            name = check['name']
            if name in facts:
                raise ValueError('Duplicate reference responsibility')
            kind = check['kind']
            if kind == 'source':
                z = archive(check['baseArchive'])
                value = {n: hashlib.sha256(z.read(n)).hexdigest() for n in z.namelist()
                         if not n.endswith('/') and n.startswith(('Assets/', 'Packages/', 'ProjectSettings/'))}
                for n in check.get('removed', []):
                    if n not in value:
                        raise ValueError('Unknown source removal')
                    del value[n]
                for n, entry in check.get('delta', {}).items():
                    safe.within(root, n)
                    value[n] = member_hash(entry)
            elif kind == 'files':
                value = {n: member_hash(entry) for n, entry in check['members'].items()}
            elif kind == 'file':
                value = member_hash(check['member'])
            else:
                raise ValueError('Unknown archived reference check')
            if value != check['expected']:
                raise ValueError('Archived reference differs: ' + name)
            facts[name] = value
    if set(facts) != set(review['requiredChecks']) or len(facts) != len(review['requiredChecks']):
        raise ValueError('Missing/duplicate reference checks')
    return facts
