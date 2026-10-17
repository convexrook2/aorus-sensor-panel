"""Create ordinary editable GLBs while preserving the versioned gzip originals."""
import gzip,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
folder=ROOT/'unpacked';folder.mkdir(exist_ok=True)
manifest=json.loads((ROOT/'manifest.json').read_text())
for row in manifest.get('assets',[manifest['current'],manifest['previous']]):
    key=row['file'];data=gzip.decompress((ROOT/row['file']).read_bytes())
    if hashlib.sha256(data).hexdigest()!=row['sha256']:
        raise ValueError(f'{key}: decoded model checksum differs from manifest')
    output=folder/Path(row['file']).with_suffix('').name
    if output.exists():
        if output.read_bytes()!=data:
            raise FileExistsError(f'Preserving an edited {output}; use a new destination.')
    else:output.write_bytes(data)
    print(output)
