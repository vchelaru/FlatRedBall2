import json,glob,os,shutil,sys,re
SP=os.environ['SP']; GP=sys.argv[2]; os.makedirs(GP,exist_ok=True)
variant=sys.argv[1].split('+')
# restore pristine
for e in os.listdir(GP):
    p=os.path.join(GP,e)
    shutil.rmtree(p) if os.path.isdir(p) else os.remove(p)
for e in os.listdir(SP+'/pristine'):
    s=os.path.join(SP,'pristine',e); d=os.path.join(GP,e)
    shutil.copytree(s,d) if os.path.isdir(s) else shutil.copy2(s,d)

def load(p): return json.load(open(p,encoding='utf-8'))
def save(p,o,indent=2): json.dump(o,open(p,'w',encoding='utf-8'),indent=indent,ensure_ascii=False)

gumj=os.path.join(GP,'GumProject.gumj')
proj=load(gumj)
ext={'Screens':'.gusj','Components':'.gucj','Standards':'.gutj'}
def path_of(kind,name): return os.path.join(GP,kind,name+ext[kind])

if 'trim' in variant or 'trimbeh' in variant:
    els={}
    for kind,key in (('Screens','ScreenReferences'),('Components','ComponentReferences'),('Standards','StandardElementReferences')):
        for r in proj[key]:
            els[r['Name']]=(kind,key,load(path_of(kind,r['Name'])))
    roots=['GameScreenGum','CardGum','FoundationSlot','Styles','WinOverlayGum']
    reach=set()
    def walk(n):
        if n in reach: return
        reach.add(n); d=els[n][2]
        if d.get('BaseType'): walk(d['BaseType'])
        for i in d.get('Instances',[]): walk(i['BaseType'])
    for r in roots: walk(r)
    print('reachable',len(reach),sorted(reach))
    for kind,key in (('Screens','ScreenReferences'),('Components','ComponentReferences'),('Standards','StandardElementReferences')):
        keep=[]
        for r in proj[key]:
            if r['Name'] in reach: keep.append(r)
            else: os.remove(path_of(kind,r['Name']))
        proj[key]=keep
    # remove now-empty dirs
    for d,_,_ in list(os.walk(GP,topdown=False)):
        if d!=GP and not os.listdir(d): os.rmdir(d)
    if 'trimbeh' in variant:
        need=set()
        for n in reach:
            for b in els[n][2].get('Behaviors',[]): need.add(b.get('BehaviorName') or b.get('Name'))
        print('behaviors needed',need)
        keep=[]
        for r in proj['BehaviorReferences']:
            if r['Name'] in need: keep.append(r)
            else: os.remove(os.path.join(GP,'Behaviors',r['Name']+'.behj'))
        proj['BehaviorReferences']=keep
    save(gumj,proj)

if 'font' in variant:
    proj['FontRanges']='32-126'
    save(gumj,proj)

if 'nonull' in variant:
    def strip(o):
        if isinstance(o,dict): return {k:strip(v) for k,v in o.items() if v is not None}
        if isinstance(o,list): return [strip(v) for v in o]
        return o
    for f in glob.glob(GP+'/**/*.*j',recursive=True):
        if f.endswith('.setj') : continue
        o=load(f); save(f,strip(o))
if 'minify' in variant:
    for f in glob.glob(GP+'/**/*.*j',recursive=True):
        if f.endswith('.setj'): continue
        o=load(f); save(f,o,indent=None)
print('files',sum(len(fs) for _,_,fs in os.walk(GP)))
