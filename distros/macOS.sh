#!/usr/bin/env bash

RED='\033[1;31m'
GRN='\033[1;32m'
YLW='\033[1;33m'
ORG='\033[38;5;208m'
CYN='\033[1;36m'
BLU='\033[1;34m'
RST='\033[0m'

REMOTE_URL="https://raw.githubusercontent.com/Earth-Restored/Solace/refs/heads/main/distros/macOS.sh"
SELF_PATH="$(realpath "$0")"
GITHUB_REPO="Earth-Restored/Solace"
GITHUB_URL="https://github.com/$GITHUB_REPO.git"

SOLACE_DIR="$HOME/solace"
SERVER_DIR="$SOLACE_DIR/solace-server"
SOURCE_DIR="$SOLACE_DIR/solace-source"
SETTINGS_FILE="$SOLACE_DIR/settings.json"
VERSION_FILE="$SOLACE_DIR/version.txt"
PLIST="$HOME/Library/LaunchAgents/com.solace.server.plist"

case $(uname -m) in
    x86_64)        ARCH_PROFILE="x64"   ;;
    aarch64|arm64) ARCH_PROFILE="arm64" ;;
esac

echo "Checking for updates..."

update_self() {
    command -v curl >/dev/null 2>&1 || return
    TMP_PATH="$(mktemp /tmp/.earth_update_XXXXXX)"
    curl -fsSL --max-time 5 "$REMOTE_URL" -o "$TMP_PATH" 2>/dev/null
    if [ -s "$TMP_PATH" ]; then
        chmod +x "$TMP_PATH"
        if ! cmp -s "$TMP_PATH" "$SELF_PATH"; then
            if command mv -f "$TMP_PATH" "$SELF_PATH" 2>/dev/null; then
                echo "[Solace] updated, restarting..."
                exec "$SELF_PATH" "$@"
            else
                echo "[Solace] updating at $SELF_PATH requires sudo..."
                sudo mv -f "$TMP_PATH" "$SELF_PATH" || { echo "[Solace] update failed"; rm -f "$TMP_PATH"; return; }
                echo "[Solace] updated, restarting..."
                exec "$SELF_PATH" "$@"
            fi
        else
            rm -f "$TMP_PATH"
        fi
    else
        rm -f "$TMP_PATH"
    fi
}

update_self "$@"

rm -rf ~/Solace_update_* ~/Solace-*.zip 2>/dev/null || true

load_settings() {
    if [ -f "$SETTINGS_FILE" ]; then
        INSTALL_MODE=$(grep -o '"installMode": *"[^"]*"' "$SETTINGS_FILE" 2>/dev/null | cut -d'"' -f4)
        INSTALL_BRANCH=$(grep -o '"branch": *"[^"]*"' "$SETTINGS_FILE" 2>/dev/null | cut -d'"' -f4)
        CURRENT_VERSION=$(grep -o '"version": *"[^"]*"' "$SETTINGS_FILE" 2>/dev/null | cut -d'"' -f4)
    fi
    [ -z "$INSTALL_MODE" ] && INSTALL_MODE="prebuilt"
    [ -z "$INSTALL_BRANCH" ] && INSTALL_BRANCH="main"
    [ -z "$CURRENT_VERSION" ] && CURRENT_VERSION="unknown"
    if [ -f "$VERSION_FILE" ]; then CURRENT_VERSION=$(cat "$VERSION_FILE"); fi
}

is_running() { curl -s --max-time 1 http://127.0.0.1:5000 | grep -q .; }
is_process_alive() { pgrep -f run_launcher.ps1 >/dev/null 2>&1; }
get_pid() { pgrep -f run_launcher.ps1 2>/dev/null | head -1; }

get_local_ip() { ifconfig 2>/dev/null | grep "inet " | grep -v 127.0.0.1 | awk '{print $2}' | head -1; }

check_deps() {
    local missing=false
    for cmd in java pwsh fzf; do
        if ! command -v "$cmd" >/dev/null 2>&1; then echo -e "  ${RED}✗${RST} $cmd"; missing=true; fi
    done
    $missing
}

if [ "$1" = "eula" ]; then
    EULA_ACTION="$2"; EULA_FILE="$SERVER_DIR/staticdata/server_template_dir/eula.txt"
    [ "$EULA_ACTION" = "--delete" ] && { rm -f "$EULA_FILE"; echo "[Solace]: Deleted."; exit 0; }
    if [ ! -f "$EULA_FILE" ]; then
        clear
        echo "======================================="
        echo "        MINECRAFT SERVER EULA"
        echo "======================================="
        echo ""
        echo "[WARNING]: Agreeing to the EULA"
        echo "without starting the server first will"
        echo "NOT pre-download the files needed"
        echo "for Buildplate Launcher."
        echo ""
        echo "Please start the server first from"
        echo "the admin panel."
        echo ""
        echo "======================================="
        echo ""
        printf "Press ENTER to exit..."
        read -r
        exit 1
    fi
    grep -q "eula=true" "$EULA_FILE" && { echo "[Solace]: Already accepted."; exit 0; }
    clear
    echo "======================================="
    echo "        MINECRAFT SERVER EULA"
    echo "======================================="
    echo ""
    echo "Before starting the server, you must"
    echo "accept the End User License Agreement."
    echo ""
    echo "Read it here:"
    echo "https://aka.ms/MinecraftEULA"
    echo ""
    echo "Type YES to agree."
    echo ""
    echo "======================================="
    echo ""
    printf "Accept EULA > "
    read CONFIRM < /dev/tty
    CONFIRM="$(echo "$CONFIRM" | tr -d '\r\n')"
    if [ "$CONFIRM" = "YES" ]; then
        if grep -q "eula=false" "$EULA_FILE"; then sed -i 's/eula=false/eula=true/g' "$EULA_FILE"
        else echo "eula=true" >> "$EULA_FILE"; fi
        echo "[Solace]: EULA accepted."
    else echo "[Solace]: Not accepted."; fi
    exit 0
fi

if [ "$1" = "uninstall" ]; then
    clear; show_banner
    section_title "UNINSTALL"
    echo "  This will permanently remove all Solace files and the earth command."
    CONFIRM=$(printf "No, cancel\nYes, remove everything" | fzf --height=15% --reverse --border --prompt="Uninstall? > ")
    [ "$CONFIRM" != "Yes, remove everything" ] && exit 0
    if [ -f "$PLIST" ]; then launchctl unload "$PLIST" 2>/dev/null || true; rm -f "$PLIST" 2>/dev/null || true; fi
    rm -rf "$SOLACE_DIR" 2>/dev/null || true
    sudo rm -f "$SELF_PATH" 2>/dev/null || true
    echo "[Solace] Uninstalled."; exit 0
fi

if [ "$1" = "help" ] || [ "$1" = "--help" ] || [ "$1" = "-h" ]; then
    echo -e "${BLU}"
    echo "   _____       __"
    echo "  / ___/____  / /___ _________"
    echo "  \__ \/ __ \/ / __ \`/ ___/ _ \\"
    echo " ___/ / /_/ / / /_/ / /__/  __/"
    echo "/____/\____/_/\__,_/\___/\___/"
    echo -e "${RST}"
    echo "Usage: earth [COMMAND]"
    echo "  (no args)    Open the TUI menu"
    echo "  help         Show this help message"
    echo "  eula         Accept the Minecraft EULA"
    echo "  eula --delete  Delete the EULA file"
    echo "  uninstall    Completely remove Solace"
    exit 0
fi

start_server() {
    if is_process_alive; then return; fi
    if [ ! -f "$SERVER_DIR/run_launcher.ps1" ]; then echo -e "${RED}[ERROR] Server files not found${RST}"; sleep 2; return; fi
    if check_deps; then echo -e "${RED}Missing dependencies.${RST}"; read -r; return; fi
    cd "$SERVER_DIR" || return
    export DOTNET_ROOT="$HOME/.dotnet" PATH="$PATH:$HOME/.dotnet:$HOME/.dotnet/tools"
    export COMPlus_gcServer=0 COMPlus_gcConcurrent=1 DOTNET_GCHeapHardLimit=268435456
    pkill -f run_launcher.ps1 2>/dev/null; fuser -k 5000/tcp 2>/dev/null
    nohup pwsh ./run_launcher.ps1 > "$SERVER_DIR/logs/launcher.log" 2>&1 & disown; sleep 3
}

stop_server() {
    if [ -f "$PLIST" ]; then
        launchctl unload "$PLIST" 2>/dev/null || true
    fi
    local pid=$(get_pid)
    if [ -n "$pid" ]; then
        local pgid=$(ps -o pgid= "$pid" 2>/dev/null | tr -d ' ')
        kill -- -"$pgid" 2>/dev/null; kill "$pid" 2>/dev/null
    fi
    pkill -f run_launcher.ps1 2>/dev/null; fuser -k 5000/tcp 2>/dev/null
    for i in 1 2 3; do
        if is_process_alive; then
            pkill -9 -f run_launcher.ps1 2>/dev/null
            fuser -k 5000/tcp 2>/dev/null
            sleep 1
        else
            break
        fi
    done
    sleep 1
}

toggle_server() {
    if is_process_alive; then
        CH=$(printf "Yes\nNo" | fzf --height=20% --reverse --border --prompt="Stop server? > ")
        [ "$CH" = "Yes" ] && stop_server
    else
        first_start_checks && start_server
        if is_process_alive && [ -f "$EULA_FILE" ] && ! grep -q "eula=true" "$EULA_FILE" 2>/dev/null; then
            eula_confirmation_loop
        fi
    fi
}

EULA_FILE="$SERVER_DIR/staticdata/server_template_dir/eula.txt"
RESOURCEPACK="$SERVER_DIR/staticdata/resourcepacks/vanilla.zip"

first_start_checks() {
    if [ ! -f "$RESOURCEPACK" ]; then
        clear
        echo "======================================="
        echo "         RESOURCE PACK REQUIRED"
        echo "======================================="
        echo ""
        echo "It seems that the resource packs"
        echo "have not been installed yet."
        echo ""
        echo "Please refer to the Discord server"
        echo "for the commands needed to download"
        echo "the required resource packs."
        echo ""
        printf "Back\n" | fzf --height=15% --reverse --border --prompt="Resource Packs > " >/dev/null
        return 1
    fi

    if [ ! -f "$EULA_FILE" ]; then
        clear; show_banner
        section_title "FIRST TIME SETUP"
        echo ""
        echo "  Please follow steps 4-7 in the manual"
        echo "  server setup instructions."
        echo ""
        echo "  Read the full guide:"
        echo "  https://github.com/Earth-Restored/Solace/blob/main/INSTALLATION.md"
        echo ""
        echo "  If you lose access to your admin account,"
        echo "  you can reset it in:"
        echo "  Settings → Reset Account Database"
        echo ""
        printf "Continue\n" | fzf --height=15% --reverse --border --prompt="Setup > " >/dev/null
    fi

    return 0
}

# ─── EULA CONFIRMATION ─────────────────────────

eula_confirmation_loop() {
    grep -q "eula=true" "$EULA_FILE" 2>/dev/null && return 0
    [ ! -f "$EULA_FILE" ] && return 0

    while true; do
        clear; show_banner
        section_title "EULA CONFIRMATION"
        echo ""
        echo "  Before starting the server, you must accept the"
        echo "  End User License Agreement."
        echo ""
        echo "  Read it here:"
        echo "  https://aka.ms/MinecraftEULA"
        echo ""
        CHOICE=$(printf "Yes, I agree\nNo, I deny" | fzf \
            --height=20% --reverse --border --prompt="Accept EULA? > ")

        if [ "$CHOICE" = "Yes, I agree" ]; then
            sed -i 's/eula=false/eula=true/g' "$EULA_FILE" || echo "eula=true" >> "$EULA_FILE"
            echo ""; echo "[Solace] EULA accepted."; sleep 1
            return 0
        fi

        clear; show_banner
        section_title "ARE YOU SURE?"
        echo ""
        echo "  You must accept the EULA to run the server."
        echo ""
        CHOICE2=$(printf "Go back\nYes, I deny" | fzf \
            --height=20% --reverse --border --prompt="Confirm? > ")
        [ "$CHOICE2" = "Yes, I deny" ] && return 1
    done
}

process_viewer() {
while true; do
    clear
    echo -e "${BLU}"
    echo "   _____       __"
    echo "  / ___/____  / /___ _________"
    echo "  \__ \/ __ \/ / __ \`/ ___/ _ \\"
    echo " ___/ / /_/ / / /_/ / /__/  __/"
    echo "/____/\____/_/\__,_/\___/\___/"
    echo -e "${RST}"
    PID=$(get_pid)
    SELECT=$({
        echo "Back"
        if [ -n "$PID" ]; then
            ps -eo pid,cmd --no-headers 2>/dev/null | grep -E "pwsh|Launcher|ApiServer|EventBus|ObjectStore|TileRenderer|BuildplateLauncher" | grep -v grep
        fi
    } | fzf --height=50% --reverse --border --prompt="Process > ")
    [ -z "$SELECT" ] && continue; [ "$SELECT" = "Back" ] && return
    SELPID=$(echo "$SELECT" | awk '{print $1}'); [[ "$SELPID" =~ ^[0-9]+$ ]] || continue
    while true; do
        clear; echo "PID: $SELPID"
        tail -n 120 "$SERVER_DIR/logs/launcher.log" 2>/dev/null || echo "(no log)"
        CH=$(printf "Refresh\nBack" | fzf --height=10% --reverse --border --prompt="Log > ")
        [ "$CH" = "Back" ] && break
    done
done
}

update_prebuilt() {
    echo "[Solace] Fetching available releases..."
    local json=$(curl -s "https://api.github.com/repos/$GITHUB_REPO/releases?per_page=100")
    local tags=$(echo "$json" | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"//;s/"//')
    [ -z "$tags" ] && echo -e "${RED}[ERROR] Failed to fetch releases${RST}" && sleep 2 && return 1
    local sel=$(echo "$tags" | fzf --height=40% --reverse --border --prompt="Version > " --no-multi)
    [ -z "$sel" ] && return 1
    stop_server
    local zip_name="Solace-osx-${ARCH_PROFILE}.zip"
    local tmp=$(mktemp -d "/tmp/solace_update_XXXXXX") || return 1
    cd "$tmp" || return 1
    curl -L --progress-bar -o server.zip "https://github.com/$GITHUB_REPO/releases/download/${sel}/${zip_name}"
    unzip -o server.zip >/dev/null 2>&1
    if [ -d "Solace-osx-${ARCH_PROFILE}" ]; then
        
        mv "Solace-osx-${ARCH_PROFILE}/"* "$SERVER_DIR/" 2>/dev/null || true
    else
        
        find . -maxdepth 1 -not -name 'server.zip' -not -name '.' -exec mv {} "$SERVER_DIR/" \; 2>/dev/null || true
    fi
    chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
    echo "$sel" > "$VERSION_FILE"
    cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"prebuilt","branch":"main","version":"$sel","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
    cd /; rm -rf "$tmp"
    echo "[Solace] Update complete ($sel)"; sleep 5
}

update_solace() {
    load_settings

    if [ "$INSTALL_MODE" = "source" ]; then
        local sel
        sel=$(pick_branch "Update Source") || return
        stop_server
        if [ ! -d "$SOURCE_DIR/.git" ]; then
            rm -rf "$SOURCE_DIR"
            git clone --recurse-submodules -b "$sel" "$GITHUB_URL" "$SOURCE_DIR"
        else
            cd "$SOURCE_DIR" || return
            git fetch origin "$sel"
            git reset --hard "origin/$sel"
            git submodule update --init --recursive
        fi
        cd "$SOURCE_DIR" || return
        env DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" pwsh ./publish.ps1 --profiles "framework-dependent-osx-$ARCH_PROFILE"
        local build_dir="$SOURCE_DIR/build/Release/framework-dependent-osx-$ARCH_PROFILE"
        if [ -d "$build_dir" ]; then
            mkdir -p "$SERVER_DIR"
            cp -r "$build_dir/"* "$SERVER_DIR/" 2>/dev/null || true
            chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
            echo "$sel" > "$VERSION_FILE"
            cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"source","branch":"$sel","version":"$sel","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
            echo "[Solace] Build complete ($sel)"
        else
            echo -e "${RED}[ERROR] Build output not found${RST}"
        fi
        sleep 2
        return
    fi

    if [ "$INSTALL_BRANCH" = "dev" ]; then
        local confirm
        confirm=$(printf "Yes, continue\nNo, cancel" | fzf --height=20% --reverse --border --prompt="Dev builds are unstable. Continue? > ")
        [ "$confirm" != "Yes, continue" ] && return
        echo -e "${YLW}Downloading dev build...${RST}"
        stop_server
        local zip_name="Solace-Dev-osx-${ARCH_PROFILE}.zip"
        local tmp=$(mktemp -d "/tmp/solace_update_XXXXXX") || return 1
        cd "$tmp" || return 1
        curl -L --progress-bar -o server.zip "https://github.com/$GITHUB_REPO/releases/download/dev-build/${zip_name}" && echo -e "  ${GRN}✔${RST} Download complete"
        echo -ne "  ${BLU}>${RST} Extracting... " && unzip -o server.zip >/dev/null 2>&1 && echo -e "${GRN}done${RST}" || { echo -e "${RED}failed${RST}"; return 1; }
        if [ -d "Solace-Dev-osx-${ARCH_PROFILE}" ]; then
            
            mv "Solace-Dev-osx-${ARCH_PROFILE}/"* "$SERVER_DIR/" 2>/dev/null || true
        else
            
            find . -maxdepth 1 -not -name 'server.zip' -not -name '.' -exec mv {} "$SERVER_DIR/" \; 2>/dev/null || true
        fi
        chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
        echo "dev-build" > "$VERSION_FILE"
        cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"prebuilt","branch":"dev","version":"dev-build","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
        cd /; rm -rf "$tmp"
        echo "[Solace] Update complete (dev-build)"; sleep 5
    else
        echo "[Solace] Fetching available releases..."
        local json=$(curl -s "https://api.github.com/repos/$GITHUB_REPO/releases?per_page=100")
        local tags=$(echo "$json" | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"//;s/"//' | grep -v "^dev-build$")
        [ -z "$tags" ] && echo -e "${RED}[ERROR] No releases found${RST}" && sleep 2 && return 1
        local sel=$(echo "$tags" | fzf --height=40% --reverse --border --prompt="Version > " --no-multi)
        [ -z "$sel" ] && return
        stop_server
        local zip_name="Solace-osx-${ARCH_PROFILE}.zip"
        local tmp=$(mktemp -d "/tmp/solace_update_XXXXXX") || return 1
        cd "$tmp" || return 1
        echo -e "${YLW}Downloading $sel...${RST}"
        curl -L --progress-bar -o server.zip "https://github.com/$GITHUB_REPO/releases/download/${sel}/${zip_name}" && echo -e "  ${GRN}✔${RST} Download complete"
        echo -ne "  ${BLU}>${RST} Extracting... " && unzip -o server.zip >/dev/null 2>&1 && echo -e "${GRN}done${RST}" || { echo -e "${RED}failed${RST}"; return 1; }
        if [ -d "Solace-osx-${ARCH_PROFILE}" ]; then
            
            mv "Solace-osx-${ARCH_PROFILE}/"* "$SERVER_DIR/" 2>/dev/null || true
        else
            
            find . -maxdepth 1 -not -name 'server.zip' -not -name '.' -exec mv {} "$SERVER_DIR/" \; 2>/dev/null || true
        fi
        chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
        echo "$sel" > "$VERSION_FILE"
        cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"prebuilt","branch":"main","version":"$sel","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
        cd /; rm -rf "$tmp"
        echo "[Solace] Update complete ($sel)"; sleep 5
    fi
}

rebuild_source() {
    if [ ! -d "$SOURCE_DIR/.git" ]; then echo -e "${RED}[ERROR] Source directory not found${RST}"; sleep 2; return; fi
    cd "$SOURCE_DIR"
    rm -rf "$SOURCE_DIR/build"
    env DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" pwsh ./publish.ps1 --profiles "framework-dependent-osx-$ARCH_PROFILE"
    local build_dir="$SOURCE_DIR/build/Release/framework-dependent-osx-$ARCH_PROFILE"
    if [ -d "$build_dir" ]; then
        cp -r "$build_dir/"* "$SERVER_DIR/" 2>/dev/null || true; chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
        echo "source-build" > "$VERSION_FILE"; echo "[Solace] Rebuild complete"
    else echo -e "${RED}[ERROR] Build output not found${RST}"; fi
    sleep 2
}

settings_menu() {
    while true; do
        load_settings
        clear; show_banner
        section_title "SETTINGS"
        echo -e "  ${CYN}Mode:${RST}    $INSTALL_MODE"
        echo -e "  ${CYN}Branch:${RST}  $INSTALL_BRANCH"
        echo -e "  ${CYN}Version:${RST} $CURRENT_VERSION"; echo ""
        local options=("Switch Branch")
        if [ "$INSTALL_MODE" = "source" ] && [ -d "$SOURCE_DIR/.git" ]; then
            options+=("Rebuild from Source"); options+=("Delete Source Folder"); options+=("Switch to Prebuilt Mode")
        elif [ ! -d "$SOURCE_DIR/.git" ]; then options+=("Switch to Source Mode"); fi
        options+=("Startup"); options+=("Reset Account Database"); options+=("Uninstall"); options+=("Back")
        CHOICE=$(printf "%s\n" "${options[@]}" | fzf --height=40% --reverse --border --prompt="Settings > " --no-multi)
        case "$CHOICE" in
            "Back") return ;;
            "Startup")
                local state="Manual Start"
                [ -f "$PLIST" ] && launchctl list com.solace.server >/dev/null 2>&1 && state="Start on Boot"
                local sel2
                sel2=$(printf "Start on Boot\nManual Start\nBack" | fzf --height=20% --reverse --border --prompt="Startup (currently: $state) > " --no-multi)
                case "$sel2" in
                    "Start on Boot")
                        launchctl load "$PLIST" 2>/dev/null || true
                        echo "[Solace] Service will start on boot."; sleep 2 ;;
                    "Manual Start")
                        launchctl unload "$PLIST" 2>/dev/null || true
                        echo "[Solace] Service set to manual start."; sleep 2 ;;
                esac ;;
            "Switch Branch")
                if [ -d "$SOURCE_DIR/.git" ]; then
                    local sel
                    sel=$(pick_branch "Switch Branch") || break
                    rm -rf "$SOURCE_DIR"
                    git clone --recurse-submodules -b "$sel" "$GITHUB_URL" "$SOURCE_DIR"
                    INSTALL_BRANCH="$sel"; rebuild_source
                else
                    local sel
                    sel=$(pick_branch "Switch Branch") || break
                    stop_server
                    if [ "$sel" = "dev" ]; then
                        local zip_name="Solace-Dev-osx-${ARCH_PROFILE}.zip"
                        local tmp=$(mktemp -d "/tmp/solace_update_XXXXXX") || break
                        cd "$tmp" || break
                        curl -L --progress-bar -o server.zip "https://github.com/$GITHUB_REPO/releases/download/dev-build/${zip_name}" && echo -e "  ${GRN}✔${RST} Download complete"
                        echo -ne "  ${BLU}>${RST} Extracting... " && unzip -o server.zip >/dev/null 2>&1 && echo -e "${GRN}done${RST}" || { echo -e "${RED}failed${RST}"; break; }
                        if [ -d "Solace-Dev-osx-${ARCH_PROFILE}" ]; then
                            mv "Solace-Dev-osx-${ARCH_PROFILE}/"* "$SERVER_DIR/" 2>/dev/null || true
                        else
                            find . -maxdepth 1 -not -name 'server.zip' -not -name '.' -exec mv {} "$SERVER_DIR/" \; 2>/dev/null || true
                        fi
                        chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
                        echo "dev-build" > "$VERSION_FILE"
                        cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"prebuilt","branch":"dev","version":"dev-build","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
                        cd /; rm -rf "$tmp"
                        echo "[Solace] Switched to dev (dev-build)"
                    else
                        local json=$(curl -s "https://api.github.com/repos/$GITHUB_REPO/releases?per_page=100")
                        local tag=$(echo "$json" | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"//;s/"//' | grep -v "^dev-build$" | head -n1)
                        [ -z "$tag" ] && echo -e "${RED}[ERROR] No release found${RST}" && sleep 2 && break
                        local zip_name="Solace-osx-${ARCH_PROFILE}.zip"
                        local tmp=$(mktemp -d "/tmp/solace_update_XXXXXX") || break
                        cd "$tmp" || break
                        curl -L --progress-bar -o server.zip "https://github.com/$GITHUB_REPO/releases/download/${tag}/${zip_name}" && echo -e "  ${GRN}✔${RST} Download complete"
                        echo -ne "  ${BLU}>${RST} Extracting... " && unzip -o server.zip >/dev/null 2>&1 && echo -e "${GRN}done${RST}" || { echo -e "${RED}failed${RST}"; break; }
                        if [ -d "Solace-osx-${ARCH_PROFILE}" ]; then
                            mv "Solace-osx-${ARCH_PROFILE}/"* "$SERVER_DIR/" 2>/dev/null || true
                        else
                            find . -maxdepth 1 -not -name 'server.zip' -not -name '.' -exec mv {} "$SERVER_DIR/" \; 2>/dev/null || true
                        fi
                        chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
                        echo "$tag" > "$VERSION_FILE"
                        cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"prebuilt","branch":"main","version":"$tag","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
                        cd /; rm -rf "$tmp"
                        echo "[Solace] Switched to main ($tag)"
                    fi
                    sleep 5
                fi ;;
            "Rebuild from Source") [ -d "$SOURCE_DIR/.git" ] && rebuild_source ;;
            "Delete Source Folder")
                echo -e "${RED}WARNING: Deleting source switches to prebuilt mode.${RST}"
                CONFIRM=$(printf "Cancel\nDelete" | fzf --height=15% --reverse --border --prompt="Are you sure? > ")
                if [ "$CONFIRM" = "Delete" ]; then
                    rm -rf "$SOURCE_DIR"
                    cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"prebuilt","branch":"main","version":"deleted-source","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
                    echo "[Solace] Source deleted. Switched to prebuilt."; sleep 2
                fi ;;
            "Switch to Prebuilt Mode")
                CONFIRM=$(printf "Cancel\nSwitch to Prebuilt" | fzf --height=15% --reverse --border --prompt="Are you sure? > ")
                if [ "$CONFIRM" = "Switch to Prebuilt" ]; then
                    update_prebuilt && rm -rf "$SOURCE_DIR"
                fi ;;
            "Switch to Source Mode")
                CONFIRM=$(printf "Cancel\nClone & Build" | fzf --height=15% --reverse --border --prompt="Are you sure? > ")
                if [ "$CONFIRM" = "Clone & Build" ]; then
                    local sel
                    sel=$(pick_branch "Clone Branch") || break
                    rm -rf "$SOURCE_DIR"; git clone --recurse-submodules -b "$sel" "$GITHUB_URL" "$SOURCE_DIR"
                    cd "$SOURCE_DIR"
                    env DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" pwsh ./publish.ps1 --profiles "framework-dependent-osx-$ARCH_PROFILE"
                    local build_dir="$SOURCE_DIR/build/Release/framework-dependent-osx-$ARCH_PROFILE"
                    mkdir -p "$SERVER_DIR"
                    if [ -d "$build_dir" ]; then cp -r "$build_dir/"* "$SERVER_DIR/" 2>/dev/null || true; fi
                    chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
                    cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"source","branch":"$sel","version":"source-build","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
                    echo "source-build ($sel)" > "$VERSION_FILE"; echo "[Solace] Build complete"; sleep 2
                fi ;;
            "Reset Account Database")
                printf '\033[H\033[J'; show_banner
                section_title "RESET DATABASE"
                echo "  If you have lost access to the first account"
                echo "  and need to start fresh, you can reset the"
                echo "  user database."
                echo ""
                echo "  This will remove ALL existing accounts and"
                echo "  allow you to register a new primary admin"
                echo "  account."
                echo ""
                CONFIRM=$(printf "No, cancel\nYes, reset database" | fzf \
                    --height=20% --reverse --border --prompt="Reset? > ")
                if [ "$CONFIRM" = "Yes, reset database" ]; then
                    rm -f "$SERVER_DIR/launcher/Data/app.db"
                    echo "[Solace] Account database reset."
                    sleep 2
                fi ;;
            "Uninstall")
                printf '\033[H\033[J'; show_banner
                section_title "UNINSTALL"
                echo "  This will permanently remove all Solace files."; echo ""
                CONFIRM=$(printf "No, cancel\nYes, remove everything" | fzf --height=15% --reverse --border --prompt="Uninstall? > ")
                if [ "$CONFIRM" = "Yes, remove everything" ]; then
                    if [ -f "$PLIST" ]; then launchctl unload "$PLIST" 2>/dev/null || true; rm -f "$PLIST" 2>/dev/null || true; fi
                    rm -rf "$SOLACE_DIR" 2>/dev/null || true
                    sudo rm -f "$SELF_PATH" 2>/dev/null || true
                    tput cnorm 2>/dev/null; clear; echo "[Solace] Uninstalled."; exit 0
                fi ;;
        esac
    done
}

info_panel() {
while true; do
    clear; show_banner
    section_title "INFORMATION"
    echo "  Server: $SERVER_DIR"; [ -d "$SOURCE_DIR" ] && echo "  Source: $SOURCE_DIR"
    echo ""
    echo "  ── Setup Steps ──"
    echo "  1. Start the server from the main menu"
    echo "  2. Open the Admin Panel: http://127.0.0.1:5000"
    echo "  3. Create an admin account"
    echo "  4. Set the Server IP in Admin Panel → Config"
    echo "  5. Get a MapTiler API key:"
    echo "     https://cloud.maptiler.com/account/keys/"
    echo "  6. Set the MapTiler key in Admin Panel → Config"
    echo "     to render maps in-game"
    echo ""
    echo "  Resourcepack:"
    echo "    $RESOURCEPACK"
    echo "  Logs: $SERVER_DIR/logs/"
    echo ""
    CHOICE=$(printf "Back" | fzf --height=15% --reverse --border --prompt="Info > ")
    [ "$CHOICE" = "Back" ] && return
done
}

show_banner() {
    echo ""
    echo -e "${BLU}"
    echo "      _____       __"
    echo "     / ___/____  / /___ _________"
    echo "     \__ \/ __ \/ / __ \`/ ___/ _ \\"
    echo "    ___/ / /_/ / / /_/ / /__/  __/"
    echo "   /____/\____/_/\__,_/\___/\___/"
    echo -e "${RST}"
    echo ""
}

load_settings

check_update() {
    [ "$INSTALL_MODE" = "source" ] && return
    [ "$INSTALL_BRANCH" = "dev" ] && return
    local json=$(curl -s "https://api.github.com/repos/$GITHUB_REPO/releases?per_page=1")
    local latest=$(echo "$json" | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"//;s/"//' | grep -v "^dev-build$" | head -n1)
    [ -z "$latest" ] && return
    local dismissed=$(grep -o '"dismissedUpdate": *"[^"]*"' "$SETTINGS_FILE" 2>/dev/null | cut -d'"' -f4)
    [ "$dismissed" = "$latest" ] && return
    [ "$latest" = "$CURRENT_VERSION" ] && return
    local choice
    choice=$(printf "Yes, update\nNo, don't show again" | fzf --height=20% --reverse --border --prompt="New version $latest available. Update now? > ")
    if [ "$choice" = "Yes, update" ]; then
        update_solace
    else
        if grep -q '"dismissedUpdate"' "$SETTINGS_FILE" 2>/dev/null; then
            sed -i 's/"dismissedUpdate": *"[^"]*"/"dismissedUpdate": "'"$latest"'"/' "$SETTINGS_FILE"
        else
            sed -i '/"updatedAt"/i\  "dismissedUpdate": "'"$latest"'",' "$SETTINGS_FILE"
        fi
    fi
}

update_menu() {
    while true; do
        load_settings
        printf '\033[H\033[J'; show_banner
        section_title "UPDATE SOLACE"
        echo ""
        echo -e "  ${CYN}Mode:${RST}    $INSTALL_MODE"
        echo -e "  ${CYN}Branch:${RST}  $INSTALL_BRANCH"
        echo -e "  ${CYN}Version:${RST} $CURRENT_VERSION"
        echo ""
        if [ "$INSTALL_BRANCH" = "main" ]; then
            echo -e "  ${CYN}[1]${RST} Download Release"
        else
            echo -e "  ${CYN}[1]${RST} Download Dev Build"
        fi
        echo -e "  ${CYN}[2]${RST} Switch Branch"
        echo -e "  ${CYN}[0]${RST} Back"
        echo ""
        printf "Choice > "
        read -r CHOICE < /dev/tty
        CHOICE="$(echo "$CHOICE" | tr -d '\r\n')"
        case "$CHOICE" in
            1)
                if [ "$INSTALL_BRANCH" = "main" ]; then
                    echo "[Solace] Fetching releases..."
                    local json=$(curl -s "https://api.github.com/repos/$GITHUB_REPO/releases?per_page=100")
                    local tags=$(echo "$json" | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"//;s/"//' | grep -v "^dev-build$")
                    [ -z "$tags" ] && echo -e "${RED}[ERROR] No releases found${RST}" && sleep 2 && continue
                    local sel=$(echo "$tags" | fzf --height=40% --reverse --border --prompt="Version > " --no-multi)
                    [ -z "$sel" ] && continue
                    local zip_name="Solace-osx-${ARCH_PROFILE}.zip"
                    local tmp=$(mktemp -d "/tmp/solace_update_XXXXXX") || { echo "Failed"; sleep 2; continue; }
                    cd "$tmp" || continue
                    stop_server
                    echo -e "  ${BLU}>${RST} Downloading $sel ($zip_name)"
                    curl -L --progress-bar -o server.zip "https://github.com/$GITHUB_REPO/releases/download/${sel}/${zip_name}"
                    echo -e "  ${GRN}✔${RST} Download complete"
                    echo -ne "  ${BLU}>${RST} Extracting... " && unzip -o server.zip >/dev/null 2>&1 && echo -e "${GRN}done${RST}" || { echo -e "${RED}failed${RST}"; sleep 2; continue; }
                    local dir="Solace-osx-${ARCH_PROFILE}"
                    if [ -d "$dir" ]; then
                        mv "$dir/"* "$SERVER_DIR/" 2>/dev/null || true
                    else
                        find . -maxdepth 1 -not -name 'server.zip' -not -name '.' -exec mv {} "$SERVER_DIR/" \; 2>/dev/null || true
                    fi
                    chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
                    echo "$sel" > "$VERSION_FILE"
                    cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"prebuilt","branch":"main","version":"$sel","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
                    cd /; rm -rf "$tmp"
                    echo "[Solace] Update complete ($sel)"
                    sleep 3
                else
                    local confirm=$(printf "Yes, continue\nNo, cancel" | fzf --height=20% --reverse --border --prompt="Dev builds are unstable. Continue? > ")
                    [ "$confirm" != "Yes, continue" ] && continue
                    local zip_name="Solace-Dev-osx-${ARCH_PROFILE}.zip"
                    local tmp=$(mktemp -d "/tmp/solace_update_XXXXXX") || { echo "Failed"; sleep 2; continue; }
                    cd "$tmp" || continue
                    stop_server
                    echo -e "  ${BLU}>${RST} Downloading dev-build ($zip_name)"
                    curl -L --progress-bar -o server.zip "https://github.com/$GITHUB_REPO/releases/download/dev-build/${zip_name}"
                    echo -e "  ${GRN}✔${RST} Download complete"
                    echo -ne "  ${BLU}>${RST} Extracting... " && unzip -o server.zip >/dev/null 2>&1 && echo -e "${GRN}done${RST}" || { echo -e "${RED}failed${RST}"; sleep 2; continue; }
                    local dir="Solace-Dev-osx-${ARCH_PROFILE}"
                    if [ -d "$dir" ]; then
                        mv "$dir/"* "$SERVER_DIR/" 2>/dev/null || true
                    else
                        find . -maxdepth 1 -not -name 'server.zip' -not -name '.' -exec mv {} "$SERVER_DIR/" \; 2>/dev/null || true
                    fi
                    chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
                    echo "dev-build" > "$VERSION_FILE"
                    cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"prebuilt","branch":"dev","version":"dev-build","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
                    cd /; rm -rf "$tmp"
                    echo "[Solace] Update complete (dev-build)"
                    sleep 3
                fi ;;
            2)
                local sel
                sel=$(pick_branch "Switch Branch") || continue
                if [ "$sel" = "dev" ]; then
                    local zip_name="Solace-Dev-osx-${ARCH_PROFILE}.zip"
                    local tmp=$(mktemp -d "/tmp/solace_update_XXXXXX") || continue
                    cd "$tmp" || continue
                    stop_server
                    echo -e "  ${BLU}>${RST} Downloading dev-build ($zip_name)"
                    curl -L --progress-bar -o server.zip "https://github.com/$GITHUB_REPO/releases/download/dev-build/${zip_name}"
                    echo -e "  ${GRN}✔${RST} Download complete"
                    echo -ne "  ${BLU}>${RST} Extracting... " && unzip -o server.zip >/dev/null 2>&1 && echo -e "${GRN}done${RST}" || { echo -e "${RED}failed${RST}"; continue; }
                    local dir="Solace-Dev-osx-${ARCH_PROFILE}"
                    [ -d "$dir" ] && mv "$dir/"* "$SERVER_DIR/" 2>/dev/null || true
                    chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
                    echo "dev-build" > "$VERSION_FILE"
                    cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"prebuilt","branch":"dev","version":"dev-build","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
                    cd /; rm -rf "$tmp"
                    echo "[Solace] Switched to dev (dev-build)"
                else
                    local json=$(curl -s "https://api.github.com/repos/$GITHUB_REPO/releases?per_page=100")
                    local tag=$(echo "$json" | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"//;s/"//' | grep -v "^dev-build$" | head -n1)
                    [ -z "$tag" ] && echo -e "${RED}[ERROR] No release found${RST}" && sleep 2 && continue
                    local zip_name="Solace-osx-${ARCH_PROFILE}.zip"
                    local tmp=$(mktemp -d "/tmp/solace_update_XXXXXX") || continue
                    cd "$tmp" || continue
                    stop_server
                    echo -e "  ${BLU}>${RST} Downloading $tag ($zip_name)"
                    curl -L --progress-bar -o server.zip "https://github.com/$GITHUB_REPO/releases/download/${tag}/${zip_name}"
                    echo -e "  ${GRN}✔${RST} Download complete"
                    echo -ne "  ${BLU}>${RST} Extracting... " && unzip -o server.zip >/dev/null 2>&1 && echo -e "${GRN}done${RST}" || { echo -e "${RED}failed${RST}"; continue; }
                    local dir="Solace-osx-${ARCH_PROFILE}"
                    [ -d "$dir" ] && mv "$dir/"* "$SERVER_DIR/" 2>/dev/null || true
                    chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true
                    echo "$tag" > "$VERSION_FILE"
                    cat > "$SETTINGS_FILE" << JSONEOF
{"installMode":"prebuilt","branch":"main","version":"$tag","updatedAt":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"}
JSONEOF
                    cd /; rm -rf "$tmp"
                    echo "[Solace] Switched to main ($tag)"
                fi
                sleep 3 ;;
            0|q) break ;;
        esac
    done
}

check_update

section_title() {
    local title="$1"
    local len=${#title}
    local total=40
    local left=$(( (total - len) / 2 ))
    printf -v pad "%*s" $left ""
    echo -e "${BLU}========================================${RST}"
    echo -e "${BLU}${pad}${title}${RST}"
    echo -e "${BLU}========================================${RST}"
    echo ""
}

is_starting() {
    is_process_alive && ! is_running
}

pick_branch() {
    local prompt="${1:-Branch}"
    local sel=$(printf "main — Stable (recommended)\ndev — Unstable (may break your installation)" | fzf --height=20% --reverse --border --prompt="$prompt > " --no-multi)
    [ -z "$sel" ] && return 1
    local branch=$(echo "$sel" | sed 's/ —.*//')
    if [ "$branch" = "dev" ]; then
        echo -e "${YLW}⚠  Dev builds are unstable and may break your server.${RST}" >&2
        local confirm=$(printf "No, cancel\nYes, continue anyway" | fzf --height=15% --reverse --border --prompt="Are you sure? > ")
        [ "$confirm" != "Yes, continue anyway" ] && return 1
    fi
    echo "$branch"
}

while true; do
    printf '\033[H\033[J'; tput civis 2>/dev/null; show_banner
    LOCAL_IP=$(get_local_ip); [ -z "$LOCAL_IP" ] && LOCAL_IP="127.0.0.1"
    if is_running; then
        echo -e "  ${GRN}●${RST} ${GRN}[RUNNING]${RST}  |  http://${LOCAL_IP}:5000"
    elif is_starting; then
        echo -e "  ${YLW}●${RST} ${YLW}[STARTING]${RST}  |  http://${LOCAL_IP}:5000"
    else
        echo -e "  ${RED}●${RST} ${RED}[STOPPED]${RST}  |  http://${LOCAL_IP}:5000"
    fi
    echo -e "  ─────────────────────────────────────────────"
    echo ""
    echo -e "  ┌─────────────────────────────────────────────┐"
    echo -e "  │ ${CYN}[1]${RST} Start/Stop Server                       │"
    echo -e "  │ ${CYN}[2]${RST} Process Explorer                        │"
    echo -e "  │ ${CYN}[3]${RST} Update Solace                           │"
    echo -e "  │ ${CYN}[4]${RST} Settings                                │"
    echo -e "  │ ${CYN}[5]${RST} Information                             │"
    echo -e "  │ ${CYN}[0]${RST} Exit                                    │"
    echo -e "  └─────────────────────────────────────────────┘"
    if [ "$INSTALL_MODE" = "prebuilt" ]; then
        echo -e "  ${YLW}Solace TUI (Prebuilt - ${CURRENT_VERSION})${RST}"
    else
        echo -e "  ${YLW}Solace TUI (${INSTALL_MODE} - ${INSTALL_BRANCH})${RST}"
    fi
    read -t 2 -n 1 KEY < /dev/tty || true

    if is_process_alive && [ -f "$EULA_FILE" ] && ! grep -q "eula=true" "$EULA_FILE" 2>/dev/null; then
        eula_confirmation_loop
    fi

    case "$KEY" in
         1) toggle_server ;; 2) process_viewer ;; 3) update_menu ;;
        4) settings_menu ;; 5) info_panel ;;
        0|q) tput cnorm 2>/dev/null; clear; exit 0 ;;
    esac
done
