import re,sys,glob,statistics,collections
# usage: summ.py label [label...]  -> median (min-max) for each phase line / probe metric
def parse(fn):
    d={}
    for line in open(fn,encoding='utf-8',errors='ignore'):
        line=line.rstrip('\n')
        m=re.match(r'^\s+(\S.*?)\s{2,}([\d.]+)ms\s+[\d.]+%',line)
        if m: d[m.group(1).strip()]=float(m.group(2)); continue
        m=re.match(r'^Startup timing . total ([\d.]+)ms',line)
        if m: d['TOTAL']=float(m.group(1)); continue
        m=re.match(r'^\[probe\] (.*?)[: ]+([\d.]+)ms(.*)$',line)
        if m:
            k=re.sub(r'[\d.]+','#',m.group(1))
            key=m.group(1).strip()
            # disambiguate repeated keys
            i=2; k2=key
            while k2 in d: k2=f"{key} #{i}"; i+=1
            d[k2]=float(m.group(2))
    return d
for lab in sys.argv[1:]:
    runs=[parse(f) for f in sorted(glob.glob(f'out/{lab}.*.txt'))]
    print(f'== {lab} ({len(runs)} runs)')
    keys=[]
    for r in runs:
        for k in r:
            if k not in keys: keys.append(k)
    for k in keys:
        v=[r[k] for r in runs if k in r]
        print(f'  {k[:75]:75s} med {statistics.median(v):9.1f}  min {min(v):9.1f} max {max(v):9.1f}  n={len(v)}')
