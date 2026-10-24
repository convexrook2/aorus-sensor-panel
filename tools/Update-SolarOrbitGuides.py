"""Fetch offline geometric outer-planet trajectory guides from JPL, sequentially.

Keeps the existing one-year runtime ephemeris unchanged. Guides span roughly
one revolution; their ends are never joined into an invented closed ellipse.
"""
import argparse,datetime,gzip,json,re,urllib.parse,urllib.request
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
DEFINITIONS=[('Mars','499',686.98),('Jupiter','599',4332.59),('Saturn','699',10759.22),('Uranus','799',30688.5),('Neptune','899',60182.0)]

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--receipt',type=Path,required=True)
    args=parser.parse_args();receipt=args.receipt.resolve()
    assert receipt.is_relative_to((ROOT/'artifacts').resolve()) and not receipt.exists()
    receipt.mkdir(parents=True)
    original=json.loads(gzip.decompress((ROOT/'Assets/SolarNavigation/nasa-horizons.json.gz').read_bytes()))
    start=max(b['Samples'][0][0] for b in original['Bodies']);stop=min(b['Samples'][-1][0] for b in original['Bodies']);centre=(start+stop)/2
    bodies=[]
    for name,target,days in DEFINITIONS:
        parameters={'format':'json','COMMAND':f"'{target}'",'CENTER':"'500@10'",'MAKE_EPHEM':"'YES'",'EPHEM_TYPE':"'VECTORS'",'START_TIME':f"'JD{centre-days/2:.9f}'",'STOP_TIME':f"'JD{centre+days/2:.9f}'",'STEP_SIZE':"'720'",'OUT_UNITS':"'AU-D'",'REF_PLANE':"'ECLIPTIC'",'REF_SYSTEM':"'ICRF'",'VEC_CORR':"'NONE'",'VEC_TABLE':"'2'",'CSV_FORMAT':"'YES'",'OBJ_DATA':"'YES'"}
        url='https://ssd.jpl.nasa.gov/api/horizons.api?'+urllib.parse.urlencode(parameters)
        print('NASA/JPL:',name,'geometric ecliptic vectors,',days,'days',flush=True)
        with urllib.request.urlopen(url,timeout=90) as response:data=json.load(response)
        (receipt/(name+'.json')).write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
        assert 'error' not in data,data.get('error')
        match=re.search(r'\$\$SOE\s*([\s\S]*?)\s*\$\$EOE',data['result']);assert match,name
        samples=[]
        for line in match.group(1).splitlines():
            fields=line.split(',')
            if len(fields)>=8:samples.append([float(fields[i].strip()) for i in [0,2,3,4,5,6,7]])
        assert 700<=len(samples)<=730 and samples[0][0]<start and samples[-1][0]>stop,name
        bodies.append({'Id':name,'Target':target,'WindowDays':days,'Query':url,'Samples':samples})
    document={'Schema':1,'Source':'NASA/JPL Horizons','TimeScale':'TDB','Frame':'J2000 ecliptic / ICRF, geometric vectors','Units':'AU and AU/day','RetrievedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'Bodies':bodies}
    output=ROOT/'Assets/SolarNavigation/nasa-orbits.json.gz'
    assert not output.exists(),'Preserve the current dataset; refresh it under a separately recorded update'
    output.write_bytes(gzip.compress(json.dumps(document,separators=(',',':')).encode(),compresslevel=9,mtime=0))
    (receipt/'summary.json').write_text(json.dumps({'source':document['Source'],'output':str(output),'bodies':[{'id':b['Id'],'samples':len(b['Samples']),'windowDays':b['WindowDays']} for b in bodies]},indent=2)+'\n',encoding='utf-8')
    print('Saved',output.stat().st_size,'bytes; existing runtime ephemeris preserved',flush=True)

if __name__=='__main__':main()
