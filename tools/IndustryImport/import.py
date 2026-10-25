"""Pinned public industry snapshots -> Bing.MockData catalog format v1.

Run one source at a time: python tools/IndustryImport/import.py vehicles|ror|rxnorm|icd|books|airlines
Downloads are bounded and atomic; raw inputs and manifests remain available for auditing.
"""
from __future__ import annotations
import argparse, csv, datetime, hashlib, io, json, os, re, shutil, sqlite3, time, urllib.parse, urllib.request, zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'tools/IndustryImport/raw'
OUT = ROOT / 'src/Bing.MockData.Catalogs/Data'
TODAY = '2026-10-25'

def sha(path):
    with open(path, 'rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()

def download(url, path, maximum=512*1024*1024):
    path = Path(path); path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists(): return path
    temp = path.with_suffix(path.suffix+'.partial')
    request = urllib.request.Request(url, headers={'User-Agent':'Bing.MockData-offline-catalog-import/1.0', 'Accept':'application/json,*/*'})
    try:
        with urllib.request.urlopen(request, timeout=60) as response, temp.open('wb') as target:
            size=0
            while block := response.read(1024*1024):
                size+=len(block)
                if size>maximum: raise ValueError('Download exceeds source budget')
                target.write(block)
        os.replace(temp,path)
    finally:
        if temp.exists(): temp.unlink()
    return path

def notice(source, url, text):
    folder=OUT/'notices'; folder.mkdir(parents=True,exist_ok=True)
    (folder/(source+'.txt')).write_text(text+'\nSource: '+url+'\nRetrieved: '+TODAY+'\n',encoding='utf-8')

class Writer:
    def __init__(self, name, version, url, license, coverage, inputs):
        OUT.mkdir(parents=True,exist_ok=True)
        self.path=OUT/(name+'.sqlite3');self.temp=OUT/(name+'.building.sqlite3')
        if self.temp.exists(): self.temp.unlink()
        self.db=sqlite3.connect(self.temp)
        self.db.executescript('''PRAGMA application_id=1112364081; PRAGMA user_version=1; PRAGMA journal_mode=DELETE; PRAGMA cache_size=-8192;
        CREATE TABLE CatalogInfo(Id TEXT PRIMARY KEY,Version TEXT NOT NULL,SourceUri TEXT NOT NULL,License TEXT NOT NULL,SnapshotDate TEXT NOT NULL,RecordCount INTEGER NOT NULL,SourceSha256 TEXT NOT NULL,Coverage TEXT NOT NULL,FileFormatVersion INTEGER NOT NULL);
        CREATE TABLE Records(Ordinal INTEGER PRIMARY KEY,Key TEXT NOT NULL COLLATE BINARY UNIQUE,Kind TEXT NOT NULL,CountryCode TEXT,Name TEXT NOT NULL,PayloadJson TEXT NOT NULL);''')
        self.name=name;self.version=version;self.url=url;self.license=license;self.coverage=coverage;self.inputs=inputs;self.count=0;self.excluded={}
    def add(self,key,kind,country,name,payload):
        if not key or not name: self.reject('missing-key-or-name');return
        try:self.db.execute('INSERT INTO Records VALUES(?,?,?,?,?,?)',(self.count+1,key,kind,country,name,json.dumps(payload,ensure_ascii=False,separators=(',',':'))));self.count+=1
        except sqlite3.IntegrityError:self.reject('duplicate-key')
    def reject(self,reason):self.excluded[reason]=self.excluded.get(reason,0)+1
    def finish(self):
        files=[{'file':str(p.relative_to(ROOT)).replace('\\','/'),'sha256':sha(p),'bytes':p.stat().st_size} for p in self.inputs]
        digest=hashlib.sha256(json.dumps(files,sort_keys=True).encode()).hexdigest()
        coverage=self.coverage+'; excluded='+json.dumps(self.excluded,sort_keys=True)
        self.db.execute('INSERT INTO CatalogInfo VALUES(?,?,?,?,?,?,?,?,1)',(self.name,self.version,self.url,self.license,TODAY,self.count,digest,coverage))
        self.db.executescript('CREATE INDEX IX_Records_KindCountryKey ON Records(Kind,CountryCode,Key COLLATE BINARY);CREATE INDEX IX_Records_NameKey ON Records(Name COLLATE BINARY,Key COLLATE BINARY);')
        kinds=dict(self.db.execute('SELECT Kind,count(*) FROM Records GROUP BY Kind'));self.db.commit();self.db.close()
        if self.temp.stat().st_size>512*1024*1024:raise ValueError('Catalog exceeds 512 MiB; split it explicitly before delivery')
        os.replace(self.temp,self.path)
        manifest={'id':self.name,'version':self.version,'source':self.url,'license':self.license,'downloadDate':TODAY,'records':self.count,'kindCounts':kinds,'excluded':self.excluded,'coverage':self.coverage,'sourceSha256':digest,'outputSha256':sha(self.path),'inputs':files,'status':'IMPORTED'}
        (OUT/(self.name+'.manifest.json')).write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps({'id':self.name,'records':self.count,'kinds':kinds,'excluded':self.excluded}),flush=True)
    def abort(self):
        self.db.close()
        if self.temp.exists(): self.temp.unlink()

def stream_array(stream):
    decoder=json.JSONDecoder();buffer='';started=False;eof=False
    while True:
        if not eof:
            block=stream.read(65536);eof=not block;buffer+=block
        buffer=buffer.lstrip()
        if not started:
            if not buffer: continue
            if buffer[0]!='[':raise ValueError('Expected root array')
            buffer=buffer[1:];started=True
        while True:
            buffer=buffer.lstrip()
            if buffer.startswith(','):buffer=buffer[1:].lstrip()
            if buffer.startswith(']'):return
            try:value,end=decoder.raw_decode(buffer)
            except json.JSONDecodeError:
                if eof:raise
                break
            yield value;buffer=buffer[end:]

def vehicles():
    version='v2026.10.4';url='https://github.com/vehiclesdb/vehiclesdb/releases/download/'+version+'/'
    folder=RAW/('vehicles-'+version);data=download(url+'vehicles.json',folder/'vehicles.json');credit=download(url+'ATTRIBUTION.md',folder/'ATTRIBUTION.md')
    notice('vehicles',url,'Vehicle data by VehiclesDB — https://vehiclesdb.com. CC-BY-4.0. Converted into read-only SQLite; original per-country attribution follows.\n'+credit.read_text(encoding='utf-8'))
    w=Writer('vehicles',version,url,'CC-BY-4.0 (plus upstream notices)','Full published makes/models; one model-country evidence row per availability entry; simulated VIN has no catalog mapping',[data,credit])
    try:
        document=json.loads(data.read_text(encoding='utf-8'))
        for make in document['makes']:
            w.add('make|'+make['slug'],'manufacturer',None,make['name'],{k:v for k,v in make.items() if k!='models'})
            for model in make['models']:
                payload=dict(model,manufacturer=make['name'],manufacturerId=make['slug'])
                for country in model.get('availability') or [None]:
                    key='model|'+make['slug']+'|'+model['slug']+'|'+model.get('kind','')+'|'+(country or '')
                    w.add(key,'model',country.upper() if country else None,make['name']+' '+model['name'],payload)
        w.finish()
    except BaseException:w.abort();raise

def ror():
    version='v2.15-2026-10-20';url='https://zenodo.org/api/records/23500708/files/v2.15-2026-10-20-ror-data.zip/content';data=download(url,RAW/'ror-v2.15/ror.zip')
    notice('ror','https://ror.org/about/faqs/','ROR registry metadata is CC0. Coverage: research organizations worldwide, including inactive records; not a complete school directory.')
    w=Writer('ror',version,url,'CC0-1.0','Full ROR v2 snapshot; original labels and relationships; country filters use first listed location',[data])
    try:
        with zipfile.ZipFile(data) as z:
            candidates=[n for n in z.namelist() if n == version+'-ror-data.json']
            if len(candidates)!=1:raise ValueError('Expected one ROR schema v2 JSON file: '+str(z.namelist()))
            with io.TextIOWrapper(z.open(candidates[0]),encoding='utf-8') as stream:
                for record in stream_array(stream):
                    names=record.get('names',[]);name=next((n['value'] for n in names if 'ror_display' in n.get('types',[])),names[0]['value'] if names else None)
                    locations=record.get('locations',[]);country=locations[0].get('geonames_details',{}).get('country_code') if locations else None
                    w.add(record['id'],'institution',country,name,record)
        w.finish()
    except BaseException:w.abort();raise

def icd():
    url='https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/2027/icd10cm-table-and-index-2027.zip';data=download(url,RAW/'icd10cm-fy2027/icd.zip')
    notice('icd10cm','https://www.cdc.gov/other/agencymaterials.html','Source: CDC/NCHS ICD-10-CM FY2027. US government coding data. No CDC endorsement. This extract contains diagnostic codes and descriptions only, not third-party images, ICD-10-PCS, or other national modifications. Coding structure public domain reference: https://stacks.cdc.gov/view/cdc/27312')
    descriptions=download('https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/2027/icd10cm-code-descriptions-2027.zip',RAW/'icd10cm-fy2027/descriptions.zip')
    w=Writer('icd10cm','FY2027',url,'US-government-public-domain','US diagnostic classification effective 2026-10-01 through 2027-09-30; tabular hierarchy plus complete ordered codes including seventh-character expansions; parent is closest published code ancestor',[data,descriptions])
    try:
        with zipfile.ZipFile(data) as z:
            names=[n for n in z.namelist() if 'tabular' in n.lower() and n.lower().endswith('.xml')]
            if len(names)!=1:raise ValueError('Ambiguous ICD tabular XML: '+str(names))
            with z.open(names[0]) as stream:
                stack=[]
                for event,node in ET.iterparse(stream,events=('start','end')):
                    if event=='start' and node.tag=='diag':stack.append(node)
                    elif event=='end' and node.tag=='diag':
                        code=node.findtext('name');name=node.findtext('desc');parent=stack[-2].findtext('name') if len(stack)>1 else None
                        payload={'code':code,'description':name,'parentCode':parent,'version':'FY2027','countryCode':'US'}
                        w.add(code,'diagnosis','US',name,payload);stack.pop();node.clear()
        with zipfile.ZipFile(descriptions) as z:
            filename=next(n for n in z.namelist() if n.endswith('/icd10cm-order-2027.txt'))
            for row in io.TextIOWrapper(z.open(filename),encoding='utf-8'):
                raw=row[6:13].strip();code=raw if len(raw)<=3 else raw[:3]+'.'+raw[3:];name=row[77:].strip()
                existing=w.db.execute('SELECT PayloadJson FROM Records WHERE Key=?',(code,)).fetchone()
                if existing:
                    payload=json.loads(existing[0]);payload['billable']=row[14]=='1';payload['description']=name
                    w.db.execute('UPDATE Records SET Name=?,PayloadJson=? WHERE Key=?',(name,json.dumps(payload,separators=(',',':')),code))
                else:
                    parent=None
                    for length in range(len(raw)-1,2,-1):
                        p=raw[:length];p=p if len(p)<=3 else p[:3]+'.'+p[3:]
                        if w.db.execute('SELECT 1 FROM Records WHERE Key=?',(p,)).fetchone():parent=p;break
                    w.add(code,'diagnosis','US',name,{'code':code,'description':name,'parentCode':parent,'version':'FY2027','countryCode':'US','billable':row[14]=='1'})

        w.finish()
    except BaseException:w.abort();raise

def rxnorm():
    url='https://download.nlm.nih.gov/rxnorm/RxNorm_full_prescribe_10052026.zip';data=download(url,RAW/'rxnorm-2026-10-05/rxnorm.zip')
    notice('rxnorm-cpc','https://www.nlm.nih.gov/research/umls/rxnorm/docs/rxnormfiles.html','NLM RxNorm Current Prescribable Content, 2026-10-05. Public subset, no UMLS license required. Terms: https://www.nlm.nih.gov/research/umls/rxnorm/docs/termsofservice.html. Includes US prescribable drugs and common OTC; not the restricted full RxNorm. No diagnosis-to-drug inference.')
    w=Writer('rxnorm-cpc','2026-10-05',url,'NLM-CPC-unrestricted','Complete CPC terms and supplied relationships; one representative term per RXCUI (prefer RXNORM source); source terms retained separately',[data])
    relation_writers=[]
    try:
        with zipfile.ZipFile(data) as z:
            conso=next(n for n in z.namelist() if n.endswith('RXNCONSO.RRF'))
            for row in csv.reader(io.TextIOWrapper(z.open(conso),encoding='utf-8'),delimiter='|'):
                if len(row)<18:w.reject('malformed-conso');continue
                fields=['rxcui','lat','ts','lui','stt','sui','ispref','rxaui','saui','scui','sdui','sab','tty','code','name','srl','suppress','cvf'];payload=dict(zip(fields,row));payload['version']='2026-10-05'
                w.add('term|'+row[7],'medication-term','US',row[14],payload)
                existing=w.db.execute('SELECT PayloadJson FROM Records WHERE Key=?',('medication|'+row[0],)).fetchone()
                if existing is None:w.add('medication|'+row[0],'medication','US',row[14],payload)
                elif row[11]=='RXNORM' and json.loads(existing[0])['sab']!='RXNORM':w.db.execute('UPDATE Records SET Name=?,PayloadJson=? WHERE Key=?',(row[14],json.dumps(payload,separators=(',',':')),'medication|'+row[0]))
            rel=next((n for n in z.namelist() if n.endswith('RXNREL.RRF')),None)
            if rel:
                for row in csv.reader(io.TextIOWrapper(z.open(rel),encoding='utf-8'),delimiter='|'):
                    if len(row)<16:w.reject('malformed-rel');continue
                    payload=dict(zip(['rxcui1','rxaui1','stype1','rel','rxcui2','rxaui2','stype2','rela','rui','srui','sab','sl','rg','dir','suppress','cvf'],row))
                    if not relation_writers or relation_writers[-1].count>=400000:
                        relation_writers.append(Writer('rxnorm-cpc-relations-'+str(len(relation_writers)+1),'2026-10-05',url,'NLM-CPC-unrestricted','Complete CPC RXNREL partition; stable source order, maximum 400000 rows per part; references RXCUI in rxnorm-cpc',[data]))
                    key=row[8] or hashlib.sha256('|'.join(row).encode()).hexdigest();relation_writers[-1].add('relation|'+key,'medication-relation','US',row[7] or row[3],payload)
        w.finish()
        for part in relation_writers:part.finish()
    except BaseException:
        w.abort()
        for part in relation_writers:part.abort()
        raise

def books():
    folder=RAW/'crossref-books-2026-10-25';folder.mkdir(parents=True,exist_ok=True);cursor='*';pages=[];count=0
    notice('crossref','https://www.crossref.org/documentation/retrieve-metadata/','Crossref factual bibliographic metadata (CC0). Only DOI, ISBN, title, author, publisher, date and type are selected. Abstracts and full text are excluded. Up to 100000 unique book records; this is not the full Crossref registry.')
    w=Writer('crossref-books','2026-10-25','https://api.crossref.org/works','CC0-1.0 factual metadata','First 100000 qualifying book records from a frozen cursor harvest; DOI stable keys; abstracts/full text excluded',pages)
    try:
        for page in range(1000):
            query=urllib.parse.urlencode({'filter':'type:book','rows':1000,'cursor':cursor,'select':'DOI,ISBN,title,author,publisher,published,type'})
            file=download('https://api.crossref.org/works?'+query,folder/(f'{page:04}.json'),32*1024*1024);pages.append(file);message=json.loads(file.read_text(encoding='utf-8'))['message'];items=message['items']
            if not items:break
            for item in items:
                if count>=100000:w.reject('harvest-cap');continue
                title='; '.join(item.get('title',[]))
                if not title or not item.get('DOI'):w.reject('missing-title-or-doi');continue
                before=w.count;w.add(item['DOI'].lower(),'book',None,title,item);count+=w.count-before
            print('Crossref qualifying books:',count,flush=True)
            if count>=100000:break
            next_cursor=message.get('next-cursor')
            if not next_cursor:break
            cursor=next_cursor;time.sleep(.25)
        if count<100000 and items:raise RuntimeError('Crossref harvest incomplete; cursor did not complete target')
        w.finish()
    except BaseException:w.abort();raise

def airlines():
    query='''SELECT ?airline ?airlineLabel ?iata ?icao ?countryCode WHERE { ?airline wdt:P31/wdt:P279* wd:Q46970 . OPTIONAL { ?airline wdt:P229 ?iata } OPTIONAL { ?airline wdt:P230 ?icao } OPTIONAL { ?airline wdt:P17/wdt:P297 ?countryCode } SERVICE wikibase:label { bd:serviceParam wikibase:language "en". } }'''
    url='https://query.wikidata.org/sparql?'+urllib.parse.urlencode({'query':query,'format':'json'});data=download(url,RAW/'wikidata-airlines-2026-10-25/airlines.json',64*1024*1024)
    notice('wikidata-airlines','https://www.wikidata.org/wiki/Wikidata:Licensing','Wikidata structured data CC0. Snapshot of airline instances and subclasses, labels and source-provided IATA/ICAO/country identifiers. Includes historical carriers; no route claims.')
    w=Writer('wikidata-airlines','2026-10-25',url,'CC0-1.0','SPARQL airline instances and subclasses including historical carriers; no real route dataset',[data])
    try:
        for row in json.loads(data.read_text(encoding='utf-8'))['results']['bindings']:
            p={k:v['value'] for k,v in row.items()};key=p['airline']+'|'+p.get('countryCode','')+'|'+p.get('iata','')+'|'+p.get('icao','');w.add(key,'airline',p.get('countryCode'),p.get('airlineLabel',p['airline']),p)
        w.finish()
    except BaseException:w.abort();raise

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('source',choices=['vehicles','ror','rxnorm','icd','books','airlines']);args=parser.parse_args()
    globals()[args.source]()
