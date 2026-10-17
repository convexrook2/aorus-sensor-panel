"""Create ordinary editable GLBs while preserving the versioned gzip originals."""
import gzip,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
folder=ROOT/'unpacked';folder.mkdir(exist_ok=True)
manifest=json.loads((ROOT/'manifest.json').read_text())
for key in ('current','previous'):
    row=manifest[key];data=gzip.decompress((ROOT/row['file']).read_bytes())
    assert hashlib.sha256(data).hexdigest()==row['sha256']
    output=folder/Path(row['file']).with_suffix('').name
    if output.exists():
        assert output.read_bytes()==data,f'Preserving an edited {output}; use a new destination.'
    else:output.write_bytes(data)
    print(output)
