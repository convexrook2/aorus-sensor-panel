"""Refresh fleet records with separate provenance for licensed model adaptations."""
from pathlib import Path
import json

ROOT=Path(__file__).resolve().parents[1]
source=ROOT/'Assets/Models/Source/Fleet'
report=json.loads((source/'build-report.json').read_text())
references=json.loads((source/'references.json').read_text())['references']
manifest=ROOT/'Assets/Models/sources.json'
records=json.loads(manifest.read_text(encoding='utf-8-sig'))
names={m['id'] for m in report['models']}
records=[r for r in records if r.get('Ship') not in names and r.get('Ship')!='AgathaKing']
for model in report['models']:
    ref=next(r for r in references if model['id'] in r['models'])
    native=ROOT/model['native']
    record=dict(Ship=model['id'],Title=model['id']+' / original reference-guided fan model',
        Artist='Authored for this project',Url=ref['url'],
        License='Original fan approximation; no third-party mesh licence claimed',
        SourceSHA256=model['sourceSha256'].upper(),SceneSHA256=model['nativeSha256'].upper(),
        SourceTriangles=model['triangles'],RenderTriangles=model['triangles'],SceneBytes=native.stat().st_size,
        OriginalPaint=True,Selection='TV-series hull or era-specific configuration',
        Preparation='tools/Build-ExpanseFleet.py + tools/Convert-HoloScene.py',
        EditableSource=model['source'].replace('\\','/'),ReferencePixelsBundled=False)
    if model['id']=='Pella':
        record.update(Title='Pella / adaptation of '+ref['title'],Artist=ref['artist'],
            License='Creative Commons Attribution 4.0 International (CC BY 4.0)',
            LicenseUrl=ref['licenseUrl'],AttributionFile=ref['attributionFile'],
            OriginalMesh=ref['baseMeshSource'],OriginalMeshSHA256=ref['originalMeshSha256'].upper(),
            OriginalPaint=False,Selection='Free Navy Pella appearance adapted from a licensed fan mesh',
            Changes=ref['changes'],VisualReferences=ref['visualReferences'],
            Preparation='tools/PellaMesh.py + tools/PellaModel.py + tools/Build-ExpanseFleet.py + tools/Convert-HoloScene.py')
    records.append(record)
records.append(dict(Ship='AgathaKing',Title='Agatha King / existing Truman-class mesh',
    Artist='mohamedhussien',ModelAlias='Truman',License='CC BY 4.0',
    Url='https://sketchfab.com/3d-models/truman-class-ec7e5f790d2940e489bf28c6e6d4427e',
    Preparation='Named selection over the existing credited Truman resource'))
manifest.write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
print('Updated provenance for '+str(len(report['models']))+' fleet models and the existing-hull alias.')
