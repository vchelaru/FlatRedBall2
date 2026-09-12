#!/bin/sh
# Installs the .NET 10 SDK into $PREFIX (default /opt/frb-dotnet) for sandboxes that have no
# .NET and cannot reach Microsoft's download hosts.
#
# Why not dotnet-install.sh: in Claude Code on the web containers the egress policy denies
# builds.dotnet.microsoft.com, aka.ms and dotnetcli.azureedge.net (403 on CONNECT), so the
# install script cannot even be fetched, and packages.microsoft.com's noble feed carries only
# .NET 6 bits. conda-forge's CDN *is* reachable and ships a real SDK build; a .conda package is
# just a zip of zstd tarballs, so this unpacks one directly — no conda, no package manager.
#
# Idempotent: re-running with the SDK already present does nothing.
set -e

PREFIX=${PREFIX:-/opt/frb-dotnet}
BASE=https://conda.anaconda.org/conda-forge/linux-64

# Keep the SDK version in step with .github/workflows/tests.yml (dotnet-version: '10.0.x').
PKGS="dotnet-sdk-10.0.400-h4e4d5bb_0.conda
      dotnet-runtime-10.0.11-h4e4d5bb_0.conda
      icu-78.2-h33c6efd_0.conda
      libgcc-15.3.0-h3363355_4.conda
      libstdcxx-15.3.0-h934c35e_4.conda"

write_env() {
    # The :- defaults matter: this file gets sourced by callers running under `set -u`.
    cat > "$PREFIX/env.sh" <<EOF
export DOTNET_ROOT=$PREFIX/lib/dotnet
export PATH=\$DOTNET_ROOT:\${PATH:-}
export LD_LIBRARY_PATH=$PREFIX/lib:\${LD_LIBRARY_PATH:-}
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
# gumcli and other dotnet tools used by the samples target net8.0, and only the 10.x runtime is
# installed here — without roll-forward the Gum pack step fails with "framework not found".
export DOTNET_ROLL_FORWARD=LatestMajor
EOF
    printf '#!/bin/sh\n. %s/env.sh\nexec "$DOTNET_ROOT/dotnet" "$@"\n' "$PREFIX" > /usr/local/bin/dotnet
    chmod +x /usr/local/bin/dotnet
}

if [ -x "$PREFIX/lib/dotnet/dotnet" ]; then
    write_env
    echo "dotnet already present at $PREFIX"
    exit 0
fi

# zstandard decodes the .conda payloads; pypi is reachable where the .NET hosts are not.
pip install --quiet zstandard

mkdir -p "$PREFIX/.pkgs"
for f in $PKGS; do
    curl -fsSL --max-time 420 -o "$PREFIX/.pkgs/$f" "$BASE/$f"
done

PREFIX="$PREFIX" python3 - <<'PY'
import glob, io, os, tarfile, zipfile, zstandard

prefix = os.environ['PREFIX']
for package in sorted(glob.glob(os.path.join(prefix, '.pkgs', '*.conda'))):
    with zipfile.ZipFile(package) as archive:
        payloads = [n for n in archive.namelist()
                    if n.startswith('pkg-') and n.endswith('.tar.zst')]
        for payload in payloads:
            reader = zstandard.ZstdDecompressor(max_window_size=2**31).stream_reader(
                io.BytesIO(archive.read(payload)))
            with tarfile.open(fileobj=io.BytesIO(reader.read())) as tar:
                tar.extractall(prefix, filter='tar')
PY

rm -rf "$PREFIX/.pkgs"
write_env
. "$PREFIX/env.sh"
dotnet --version
