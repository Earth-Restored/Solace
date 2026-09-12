#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="$SCRIPT_DIR/bin/Release/publish"

dotnet publish -c Release --self-contained false -p:UseAppHost=false -o "$OUTPUT_DIR" \
    -p:EFCoreCompileQueries=false \
    -p:EFCorePrecompileQueries=false \
    -p:EFPrecompileQueriesStage=None \
    -p:EFScaffoldModelStage=None

RUNTIMES_PATH="$OUTPUT_DIR/runtimes"

if [ -d "$RUNTIMES_PATH" ]; then
    for dir in "$RUNTIMES_PATH"/*; do
        if [ -d "$dir" ]; then
            folder_name="$(basename "$dir")"
            if [[ ! "$folder_name" =~ ^(win.*|osx.*|linux-x64|linux-arm64|linux-arm)$ ]]; then
                rm -rf "$dir"
            fi
        fi
    done
fi