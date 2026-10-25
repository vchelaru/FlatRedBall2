# Rebuilds Variants/apos-*.knifx.gz: Apos.Shapes 0.9.1's own apos-shapes.fx with only some of its
# 16 "ShapesN" techniques kept. Same compiler and flags Apos uses (ShadowDuskCLI 0.21.0,
# --target-runtime kni-knifx); with all 16 kept the output is byte-identical to the shipped knifx.
#
#   gh api "repos/Apostolique/Apos.Shapes/contents/Source/Content/apos-shapes.fx?ref=v0.9.1" --jq .content | base64 -d > apos-shapes.fx
#   dotnet tool install shadowdusk.cli --version 0.21.0 --tool-path sdtool
#   python make-variants.py apos-shapes.fx sdtool/ShadowDuskCLI.exe
import gzip, re, subprocess, sys, os

fx_path, cli = sys.argv[1], sys.argv[2]
out_dir = os.path.dirname(os.path.abspath(__file__))
src = open(fx_path, encoding='utf-8-sig').read()
start = src.index('technique Shapes0 {')
head, techniques = src[:start], re.findall(r'(technique Shapes\d+ \{.*?\n\})', src[start:], re.S)
assert len(techniques) == 16

for name, keep in [('v0', [0]), ('v012', [0, 1, 2])]:
    fx = os.path.join(out_dir, f'apos-{name}.fx')
    knifx = fx[:-3] + '.knifx'
    open(fx, 'w', encoding='utf-8').write(head + '\n'.join(techniques[i] for i in keep) + '\n')
    subprocess.run([cli, fx, knifx, '--target-runtime', 'kni-knifx'], check=True)
    with open(knifx, 'rb') as f, gzip.open(knifx + '.gz', 'wb', 9) as g:
        g.write(f.read())
    os.remove(fx)
    os.remove(knifx)
