"""Retained-fixture transport: real retained 0.3.3 Quickstart save into a fresh pinned reader.
Usage: python3 Tools/prepare-quickstart-033-upgrade.py REPO PIN SOURCE DESTINATION
Never modifies the source or a sealed destination. Uses the repository's native copy trust.
This proves load preservation only; it does not advance old-save construction.
"""
import hashlib,json,subprocess,sys
from pathlib import Path
repo=Path(sys.argv[1]);pin=sys.argv[2];source=Path(sys.argv[3]);destination=Path(sys.argv[4])
sys.path.insert(0,str(repo/'Tools'))
from upgrade_profile_inputs import Commit,json_bytes,require,sha
import upgrade_profile_state as state
import scenario_profile
fs=state.fs
game=Path('/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ.exe')
oldpin='e96b50e1c7ce698ed05749963d4fb885ab42d054'
prefix='Mods/ThousandAndFirst/'
require(fs.CLI_ROOT.fullmatch(str(source)) and fs.CLI_ROOT.fullmatch(str(destination))
        and source!=destination,'distinct canonical scenario roots required')
require(subprocess.check_output(['git','-C',str(repo),'rev-parse','HEAD'],text=True).strip()==pin,'candidate tree differs from pin')
require(not subprocess.check_output(['git','-C',str(repo),'status','--porcelain'],text=True).strip(),'candidate tree is dirty')
fs.directory(source);state.stopped_source(source,game)
subprocess.run([repo/"Tools/check-player-log.sh",source/"Player.log"],check=True)
source_seal=Path(str(source)+'.seal');target_seal=Path(str(destination)+'.seal')
fs.directory(source_seal);fs.empty_destination(destination);fs.empty_destination(target_seal)
local=source/'Local'
seal_raw=fs.read_bytes(source_seal/'profile.sha256',4*1024**2)
expected=scenario_profile.read_seal(str(source_seal/'profile.sha256'))
require(scenario_profile.inventory(str(local))==expected,'source Local differs from closed seal')
request_raw=fs.read_bytes(source_seal/'request.txt',1024);request=fs.request_text(request_raw)
marker='Name="r_TAF_ScenarioRequest_v1" Value="'
embark=fs.read_bytes(local/(prefix+'Harness/EmbarkModules.xml'),fs.MAX_LOCAL_FILE).decode('utf-8')
require(embark.count(marker)==1 and embark.split(marker)[1].split('"',1)[0]==request,'old request binding differs')
require(not (local/'scenario-load.txt').exists() and not (local/'scenario-load-snapshot.txt').exists(),'source already a load profile')
old=Commit(repo,oldpin).runtime();excluded={'manifest.json','README.md','CHANGELOG.md','workshop.json'}
for path,raw in old.items():
 if path not in excluded:
  require(expected.get(scenario_profile.normalize(prefix+path))==sha(raw),'old executable/content differs from public 0.3.3: '+path)
oldkeys={scenario_profile.normalize(prefix+p) for p in old}
require(all(not p.startswith(prefix.casefold()) or p in oldkeys or p.startswith((prefix+'Harness/').casefold()) for p in expected),'extra old production content')
oldmanifest=json.loads(fs.read_bytes(local/(prefix+'manifest.json'),fs.MAX_LOCAL_FILE))
require(oldmanifest.get('version')=='0.3.3' or oldmanifest.get('Version')=='0.3.3','old manifest version differs')
receipt=fs.read_bytes(source/'scenario-save-receipt.txt',512);lines=receipt.decode('ascii').split('\n')
require(len(lines)==6 and lines[-1]=='' and lines[0]=='taf-scenario-save-v1' and fs.GUID.fullmatch(lines[1])
        and all(fs.SHA.fullmatch(x) for x in lines[2:5]),'malformed old save receipt')
journal=fs.read_bytes(source/'scenario-journal.tsv',4*1024**2)
from personas import persona_matrix
rows=persona_matrix.read_journal(journal.decode('utf-8-sig'))
require(not any(outcome=='REFUSED' for _,outcome,_ in rows),'old source journal refused')
boots=[body for verb,outcome,body in rows if verb=='QUICKSTART-BOOT-COMPLETE' and outcome=='OK']
completed=[body for verb,outcome,body in rows if verb=='QUICKSTART-SAVE-COMPLETE' and outcome=='OK']
require(len(boots)==1 and boots[0].startswith('quickstart-save marsh yes;')
        and len(completed)==1 and all(field in completed[0] for field in ('real-save=true','exact-owner-and-stock=true','no-bootstrap-replay=true')),
        'old source does not prove an exact fresh starting grant')
require(b'public const int StarterWaterDrams = 24;' in old['Core/KingdomQuickstartRules.cs'],'old pinned grant differs')
gameid=lines[1];snapshot=fs.read_bytes(source/'scenario-save-snapshot.txt',fs.MAX_SNAPSHOT)
require(snapshot.startswith(b'taf-quickstart-save-v1:') and sha(snapshot)==lines[4],'old Quickstart snapshot differs')
saves=source/'Synced/Saves';fs.directory(saves)
require(sorted(p.name for p in saves.iterdir())==[gameid],'old source has more than its exact save')
save=saves/gameid;fs.directory(save)
require(set(p.name for p in save.iterdir())==set(fs.SAVE_FILES),'save contains unsupported artifacts')
hashes={name:fs.digest(save/name) for name in fs.SAVE_FILES}
require(hashes[fs.SAVE_FILES[0]]==lines[2] and hashes[fs.SAVE_FILES[1]]==lines[3],'old primary/info receipt differs')
files,dirs=state.inventory(source,['Synced']);state.validate_state(files,dirs)
state.native('Inspect',state.native_plan(source,None,['Synced'],files,dirs))
frozen={source/'process-ownership.json':fs.read_bytes(source/'process-ownership.json',16384),
        source_seal/'profile.sha256':seal_raw,source_seal/'request.txt':request_raw,
        source/'scenario-journal.tsv':journal,source/'scenario-save-receipt.txt':receipt,source/'scenario-save-snapshot.txt':snapshot}
current=Commit(repo,pin);runtime=current.runtime();harness=current.harness()
inputs={prefix+p:b for p,b in runtime.items()}
manifest=json.loads(inputs[prefix+'manifest.json']);manifest['Directories'][0]['Paths'].append('/Harness/')
manifest['title'] += ' [DEV SCENARIO HARNESS]';inputs[prefix+'manifest.json']=json_bytes(manifest)
for path,raw in harness.items():inputs[prefix+'Harness/'+path]=raw
path=prefix+'Harness/EmbarkModules.xml';raw=inputs[path].decode('utf-8');require(raw.count(marker)==1,'current request marker is ambiguous')
a,b=raw.split(marker);prior,tail=b.split('"',1);inputs[path]=(a+marker+request+'"'+tail).encode('utf-8')
for name in ('PlayerOptions.json','ModSettings.json','scenario-script.txt'):
 inputs[name]=fs.read_bytes(local/name,fs.MAX_LOCAL_FILE)
require(b'quickstart-save marsh yes' in inputs['scenario-script.txt'],'source script is not the retained marsh Quickstart save')
inputs['scenario-load.txt']=('taf-scenario-load-v1\n'+gameid+'\n'+lines[2]+'\n'+lines[3]+'\n'+hashes['Cache.db']+'\n'+lines[4]+'\n').encode('ascii')
inputs['scenario-load-snapshot.txt']=snapshot
inputs['scenario-historical-quickstart.txt']=('taf-quickstart-033-grant-v1\n'+oldpin+'\n'+gameid+'\n'+lines[4]+'\n24\n').encode('ascii')
fs.create_directory(destination,allow_empty=True);fs.create_directory(target_seal,allow_empty=True)
fs.create_directory(destination/'Local');fs.create_directory(destination/'Save')
for path,raw in sorted(inputs.items()):
 target=destination/'Local'/path
 cursor=destination/'Local'
 for component in Path(path).parts[:-1]:
  cursor=cursor/component
  if cursor.exists():fs.directory(cursor)
  else:fs.create_directory(cursor)
 fs.write_new(target,raw)
proof=state.native('Copy',state.native_plan(source,destination,['Synced'],files,dirs))
require(state.inventory(destination,['Synced'])==(files,dirs),'new save inventory differs')
for path,raw in frozen.items():require(fs.read_bytes(path,max(1,len(raw)))==raw,'source witness changed')
require(scenario_profile.inventory(str(local))==expected,'source Local changed')
require(state.inventory(source,['Synced'])==(files,dirs),'old saved state changed')
state.stopped_source(source,game)
actual=scenario_profile.inventory(str(destination/'Local'))
want={scenario_profile.normalize(path):sha(raw) for path,raw in inputs.items()}
require(len(want)==len(inputs) and actual==want,'fresh current Local differs from pinned inputs')
seal=scenario_profile.SEAL_HEADER+'\n'+''.join(actual[p]+'  '+p+'\n' for p in sorted(actual))
evidence={'schema':'taf-scenario-load-source-v1','sourceRoot':str(source),'gameId':gameid,'processAuthority':False,
 'sourceHashes':{str(p.relative_to(source)) if p.is_relative_to(source) else '.seal/'+p.name:sha(b) for p,b in frozen.items()},'saveHashes':hashes}
transport={'schema':'taf-quickstart-cross-version-v1','oldProductionPin':oldpin,'candidateCommit':pin,'sourceRoot':str(source),
 'destinationRoot':str(destination),'oldPublicCodeAndContentVerified':True,'oldPackagingExclusions':sorted(excluded),
 'nativeCopyProof':proof,'historicalInitialWaterDrams':24,'oldSourceJournalSha256':sha(journal),'scope':'real 0.3.3 save cold-load preservation only; no construction advance',
 'inputHashes':want}
fs.write_new(destination/'quickstart-upgrade-transport.json',json_bytes(transport))
fs.write_new(destination/'load-source-evidence.json',json_bytes(evidence))
fs.write_new(target_seal/'profile.sha256',seal.encode('utf-8'));fs.write_new(target_seal/'request.txt',request_raw)
subprocess.run([sys.executable,repo/'Tools/scenario_run_record.py','seal',destination,'--tree',repo,'--role','cold-load-session',
 '--seed','#43101','--script','quickstart-save marsh yes','--turn-budget','100','--timeout-seconds','1800',
 '--profile-name','quickstart-033-upgrade'],check=True)
print(json.dumps({'status':'prepared','source':str(source),'destination':str(destination),'candidate':pin,'oldProductionPin':oldpin}))
