import re,sys,glob,statistics
exec(open('summ.py').read().split("for lab in sys.argv")[0])
mode=sys.argv[1]; variants=sys.argv[2].split(',')
metrics=sys.argv[3].split('|')
data={v:[parse(f) for f in sorted(glob.glob(f'out/m{mode}_{v}.*.txt'))] for v in variants}
def get(r,m):
    for k in r:
        if k==m or k.startswith(m): return r[k]
print(f'{"metric":55s}'+''.join(f'{v:>22s}' for v in variants))
for m in metrics:
    row=f'{m[:55]:55s}'
    for v in variants:
        vals=[x for x in (get(r,m) for r in data[v]) if x is not None]
        row+= f'{statistics.median(vals):9.0f} ({min(vals):4.0f}-{max(vals):4.0f})  ' if vals else f'{"-":>22s}'
    print(row)
