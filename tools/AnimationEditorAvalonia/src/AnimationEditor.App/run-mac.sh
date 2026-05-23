#!/usr/bin/env bash
# Builds and launches Animation Editor as a macOS .app bundle.
# The bundle's Info.plist sets CFBundleDisplayName = "Animation Editor" so the
# Dock shows the correct name with a space. Use this instead of `dotnet run`
# when working on macOS.
#
# Usage:
#   ./run-mac.sh              # Debug build (default)
#   ./run-mac.sh Release      # Release build
#
# The -W flag makes `open` block until the app window closes, so the
# terminal stays busy like a normal `dotnet run` session.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="${1:-Debug}"
BUNDLE="$SCRIPT_DIR/bin/$CONFIG/net10.0/AnimationEditor.app"

DOTNET="$(command -v dotnet 2>/dev/null || echo /usr/local/share/dotnet/dotnet)"

"$DOTNET" build "$SCRIPT_DIR/AnimationEditor.App.csproj" \
    --configuration "$CONFIG" \
    --nologo -v q

open -W "$BUNDLE"
