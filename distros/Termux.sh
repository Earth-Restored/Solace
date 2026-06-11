#!/data/data/com.termux/files/usr/bin/bash

RED='\033[1;31m'
GRN='\033[1;32m'
YLW='\033[1;33m'
ORG='\033[38;5;208m'
CYN='\033[1;36m'
BLU='\033[1;34m'
RST='\033[0m'

REMOTE_URL="https://raw.githubusercontent.com/Earth-Restored/Solace/refs/heads/main/distros/Termux.sh"
SELF_PATH="$(realpath "$0")"
GITHUB_REPO="Earth-Restored/Solace"
GITHUB_URL="https://github.com/$GITHUB_REPO.git"

RELEASE_ARCH="linux-arm64"

# ─── SELF-UPDATE ──────────────────────────────────────────

echo "Checking for updates..."

update_self() {
    command -v curl >/dev/null 2>&1 || return
    TMP_PATH="$(mktemp /data/data/com.termux/files/usr/tmp/.earth_update_XXXXXX)"
    curl -fsSL --max-time 5 "$REMOTE_URL" -o "$TMP_PATH" 2>/dev/null
    if [ -s "$TMP_PATH" ]; then
        chmod +x "$TMP_PATH"
        if ! cmp -s "$TMP_PATH" "$SELF_PATH"; then
            if command mv -f "$TMP_PATH" "$SELF_PATH" 2>/dev/null; then
                echo "[Solace] updated"
            else
                echo "[Solace] updating at $SELF_PATH requires sudo..."
                sudo mv -f "$TMP_PATH" "$SELF_PATH" || { echo "[Solace] update failed"; rm -f "$TMP_PATH"; return; }
                echo "[Solace] updated"
            fi
            if [ -n "$PROOT" ]; then
                echo "Please exit proot environment and run the command again."
                exit 0
            else
                echo "[Solace] restarting..."
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

# ─── EULA ─────────────────────────────────────────────────

if [ "$1" = "eula" ]; then
    EULA_ACTION="$2"
    TMP_SCRIPT=$(mktemp)
    cat > "$TMP_SCRIPT" << 'EOF'
#!/usr/bin/env bash
export TERM=xterm
EULA_FILE="$HOME/Solace/staticdata/server_template_dir/eula.txt"
if [ "$EULA_ACTION" = "--delete" ]; then
    rm -f "$EULA_FILE"
    echo "[Solace]: The eula file has been deleted."
    exit 0
fi
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
if grep -q "eula=true" "$EULA_FILE"; then
    echo "[Solace]: EULA already accepted."
    exit 0
fi
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
    if grep -q "eula=false" "$EULA_FILE"; then
        sed -i 's/eula=false/eula=true/g' "$EULA_FILE"
    else
        echo "eula=true" >> "$EULA_FILE"
    fi
    echo ""
    echo "[Solace]: EULA accepted."
else
    echo ""
    echo "[Solace]: EULA not accepted."
fi
EOF
    chmod +x "$TMP_SCRIPT"
    proot-distro login ubuntu --shared-tmp -- env EULA_ACTION="$EULA_ACTION" bash "$TMP_SCRIPT"
    rm -f "$TMP_SCRIPT"
    exit 0
fi

# ─── UNINSTALL ────────────────────────────────────────────

if [ "$1" = "uninstall" ]; then
    echo "[Solace] Stopping server..."
    proot-distro login ubuntu -- bash -c '
        PID_FILE=~/Solace/server.pid
        if [ -f "$PID_FILE" ]; then
            PID=$(cat "$PID_FILE")
            PGID=$(ps -o pgid= "$PID" 2>/dev/null | tr -d " ")
            kill -- -"$PGID" 2>/dev/null
            kill "$PID" 2>/dev/null
        fi
        pkill -f run_launcher.ps1 2>/dev/null
        fuser -k 5000/tcp 2>/dev/null
        rm -f "$PID_FILE"
    ' 2>/dev/null

    echo "[Solace] Removing Solace files..."
    proot-distro login ubuntu -- rm -rf ~/Solace 2>/dev/null

    echo "[Solace] Removing earth command..."
    sudo rm -f "$SELF_PATH" 2>/dev/null || rm -f "$SELF_PATH"

    echo "[Solace] Solace has been uninstalled."
    exit 0
fi

# ─── HELP ─────────────────────────────────────────────────

if [ "$1" = "help" ] || [ "$1" = "--help" ] || [ "$1" = "-h" ]; then
    echo -e "${BLU}"
    echo "   _____       __"
    echo "  / ___/____  / /___ _________"
    echo "  \__ \/ __ \/ / __ \`/ ___/ _ \\"
    echo " ___/ / /_/ / / /_/ / /__/  __/"
    echo "/____/\____/_/\__,_/\___/\___/"
    echo -e "${RST}"
    echo "Usage: earth [COMMAND]"
    echo ""
    echo "Commands:"
    echo "  (no args)    Open the TUI menu"
    echo "  help         Show this help message"
    echo "  eula         Accept the Minecraft EULA"
    echo "  eula --delete  Delete the EULA file"
    echo "  uninstall    Completely remove Solace"
    echo ""
    echo "Platform: Termux (Android) - proot-distro + Ubuntu"
    echo ""
    exit 0
fi

# ─── MAIN DASHBOARD (runs inside proot-distro) ────────────

proot-distro login ubuntu -- bash << 'DASHBOARD'
#!/usr/bin/env bash

RED='\033[1;31m'
GRN='\033[1;32m'
YLW='\033[1;33m'
ORG='\033[38;5;208m'
CYN='\033[1;36m'
BLU='\033[1;34m'
RST='\033[0m'

GITHUB_REPO="Earth-Restored/Solace"
GITHUB_URL="https://github.com/$GITHUB_REPO.git"

DB=~/Solace/nohup.log
PID_FILE=~/Solace/server.pid
TIME_FILE=~/Solace/server.start
SOLACE_DIR=~/Solace
EULA_FILE="$SOLACE_DIR/staticdata/server_template_dir/eula.txt"
RESOURCEPACK="$SOLACE_DIR/staticdata/resourcepacks/vanilla.zip"
SETTINGS_FILE="$SOLACE_DIR/settings.json"
VERSION_FILE="$SOLACE_DIR/version.txt"
RELEASE_ARCH="linux-arm64"

mkdir -p "$SOLACE_DIR"

# ─── PROOT UTILITY FUNCTIONS ────────────────────────

load_settings() {
    INSTALL_MODE="prebuilt"
    INSTALL_BRANCH="main"
    CURRENT_VERSION="unknown"
    if [ -f "$SETTINGS_FILE" ]; then
        INSTALL_MODE=$(grep -o '"installMode": *"[^"]*"' "$SETTINGS_FILE" 2>/dev/null | cut -d'"' -f4)
        INSTALL_BRANCH=$(grep -o '"branch": *"[^"]*"' "$SETTINGS_FILE" 2>/dev/null | cut -d'"' -f4)
        CURRENT_VERSION=$(grep -o '"version": *"[^"]*"' "$SETTINGS_FILE" 2>/dev/null | cut -d'"' -f4)
    fi
    [ -z "$INSTALL_MODE" ] && INSTALL_MODE="prebuilt"
    [ -z "$INSTALL_BRANCH" ] && INSTALL_BRANCH="main"
    [ -z "$CURRENT_VERSION" ] && CURRENT_VERSION="unknown"
    if [ -f "$VERSION_FILE" ]; then
        CURRENT_VERSION=$(cat "$VERSION_FILE")
    fi
}

is_running() {
    curl -s --max-time 1 http://127.0.0.1:5000 | grep -q .
}

is_process_alive() {
    if [ -f "$PID_FILE" ]; then
        PID=$(cat "$PID_FILE")
        kill -0 "$PID" 2>/dev/null && return 0
    fi
    pgrep -f run_launcher.ps1 >/dev/null 2>&1
}

get_pid() {
    [ -f "$PID_FILE" ] && cat "$PID_FILE"
}

check_deps() {
    local missing=false
    for cmd in java pwsh fzf; do
        if ! command -v "$cmd" >/dev/null 2>&1; then
            echo -e "  ${RED}✗${RST} $cmd (inside Ubuntu)"
            missing=true
        fi
    done
    $missing
}

# ─── ACTIONS ──────────────────────────────────────

start_server() {
    if is_process_alive; then
        return
    fi

    cd "$SOLACE_DIR" || return

    export DOTNET_ROOT=$HOME/.dotnet
    export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools
    export COMPlus_gcServer=0
    export COMPlus_gcConcurrent=1
    export DOTNET_GCHeapHardLimit=268435456

    if check_deps; then
        clear
        echo -e "${RED}Missing dependencies inside Ubuntu!${RST}"
        echo "Run the installer again."
        printf "Press ENTER to return..."
        read -r
        return
    fi

    nohup setsid pwsh run_launcher.ps1 > "$DB" 2>&1 < /dev/null &
    PID=$!
    disown

    echo "$PID" > "$PID_FILE"
    date +%s > "$TIME_FILE"
}

stop_server() {
    if ! is_process_alive; then
        return
    fi

    if [ -f "$PID_FILE" ]; then
        PID=$(cat "$PID_FILE")
        PGID=$(ps -o pgid= "$PID" 2>/dev/null | tr -d ' ')
        kill -- -"$PGID" 2>/dev/null
        kill "$PID" 2>/dev/null
    fi

    pkill -f run_launcher.ps1 2>/dev/null
    fuser -k 5000/tcp 2>/dev/null
    rm -f "$PID_FILE" "$TIME_FILE"
    sleep 2
}

toggle_server() {
    if is_process_alive; then
        CH=$(printf "Yes\nNo" | fzf \
            --height=20% --reverse --border --prompt="Stop server? > ")
        [ "$CH" = "Yes" ] && stop_server
    else
        start_server
    fi
}

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

# ─── PROCESS VIEWER ──────────────────────────────

process_viewer() {
while true; do
    clear; show_banner
    section_title "PROCESS EXPLORER"
    PID=$(get_pid)

    if is_running; then
        echo -e "  ${GRN}[RUNNING]${RST}"
    else
        echo -e "  ${RED}[STOPPED]${RST}"
    fi
    echo ""

    SELECT=$(
    {
        echo "Back to Main Menu"
        if [ -n "$PID" ]; then
            ps -eo pid,ppid,cmd --no-headers 2>/dev/null | \
            grep -E "pwsh|Launcher|ApiServer|EventBus|ObjectStore|TileRenderer|BuildplateLauncher" | \
            grep -v grep
        fi
    } | fzf --height=50% --reverse --border --prompt="Process > ")
    [ -z "$SELECT" ] && continue
    [ "$SELECT" = "Back to Main Menu" ] && return

    SELPID=$(echo "$SELECT" | awk '{print $1}')
    [[ "$SELPID" =~ ^[0-9]+$ ]] || continue

    while true; do
        clear
        echo "==== PROCESS LOG ===="
        echo "PID: $SELPID"
        echo ""
        tail -n 120 "$DB"
        CH=$(printf "Refresh\nBack" | fzf --height=10% --reverse --border --prompt="Log > ")
        [ "$CH" = "Back" ] && break
    done
done
}

# ─── UPDATE ──────────────────────────────────────

update_solace() {
    load_settings
    if [ "$INSTALL_BRANCH" = "dev" ]; then
        echo -e "${YLW}Downloading latest developer build...${RST}"
        force_stop_server
        local zip_name="Solace-Dev-${RELEASE_ARCH}.zip"
        local tmp=$(mktemp -d ~/Solace_update_XXXXXX) || return 1
        cd "$tmp" || return 1
        curl -L --progress-bar -o server.zip "https://github.com/$GITHUB_REPO/releases/download/dev-build/${zip_name}"
        unzip -o server.zip >/dev/null 2>&1
        
        find . -maxdepth 1 -not -name 'server.zip' -not -name '.' -exec mv {} "$SOLACE_DIR/" \; 2>/dev/null || true
        chmod -R +x "$SOLACE_DIR/components/" 2>/dev/null || true
        echo "dev-build" > "$VERSION_FILE"
        cat > "$SETTINGS_FILE" << JSONEOF
{
  "installMode": "prebuilt",
  "branch": "dev",
  "version": "dev-build",
  "updatedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
JSONEOF
        rm -rf "$tmp"
        echo "[Solace] Update complete (dev-build)"
        sleep 5
    else
        echo "[Solace] Fetching available releases..."
        local json=$(curl -s "https://api.github.com/repos/$GITHUB_REPO/releases?per_page=100")
        local tags=$(echo "$json" | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"//;s/"//' | grep -v "^dev-build$")
        [ -z "$tags" ] && echo -e "${RED}[ERROR] No releases found${RST}" && sleep 2 && return 1
        local sel=$(echo "$tags" | fzf --height=40% --reverse --border --prompt="Version > " --no-multi)
        [ -z "$sel" ] && return
        force_stop_server
        local zip_name="Solace-${RELEASE_ARCH}.zip"
        local tmp=$(mktemp -d ~/Solace_update_XXXXXX) || return 1
        cd "$tmp" || return 1
        curl -L --progress-bar -o server.zip "https://github.com/$GITHUB_REPO/releases/download/${sel}/${zip_name}"
        unzip -o server.zip >/dev/null 2>&1
        
        find . -maxdepth 1 -not -name 'server.zip' -not -name '.' -exec mv {} "$SOLACE_DIR/" \; 2>/dev/null || true
        chmod -R +x "$SOLACE_DIR/components/" 2>/dev/null || true
        cat > "$SETTINGS_FILE" << JSONEOF
{
  "installMode": "prebuilt",
  "branch": "main",
  "version": "$sel",
  "updatedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
JSONEOF
        echo "$sel" > "$VERSION_FILE"
        rm -rf "$tmp"
        echo "[Solace] Update complete ($sel)"
        sleep 5
    fi
}

force_stop_server() {
    if is_running; then
        if [ -f "$PID_FILE" ]; then
            PID=$(cat "$PID_FILE")
            PGID=$(ps -o pgid= "$PID" 2>/dev/null | tr -d ' ')
            kill -- -"$PGID" 2>/dev/null
            kill "$PID" 2>/dev/null
        fi
        pkill -f run_launcher.ps1 2>/dev/null
        fuser -k 5000/tcp 2>/dev/null
        rm -f "$PID_FILE" "$TIME_FILE"
        sleep 2
    fi
}

# ─── SETTINGS ────────────────────────────────────

settings_menu() {
    while true; do
        load_settings
        clear; show_banner
        section_title "SETTINGS"
        echo ""
        echo -e "  ${CYN}Mode:${RST}    $INSTALL_MODE"
        echo -e "  ${CYN}Branch:${RST}  $INSTALL_BRANCH"
        echo -e "  ${CYN}Version:${RST} $CURRENT_VERSION"
        echo ""
        echo "  Server: $SOLACE_DIR"
        echo ""

        CHOICE=$(printf "Switch Branch (re-download)\nReset Account Database\nUninstall\nBack" | fzf \
            --height=20% --reverse --border --prompt="Settings > " --no-multi)

        case "$CHOICE" in
            "Back") return ;;
            "Switch Branch (re-download)")
                local sel
                sel=$(pick_branch "Switch Branch") || break
                force_stop_server
                local tag
                if [ "$sel" = "dev" ]; then
                    tag="dev-build"
                else
                    echo "[Solace] Fetching releases..."
                    local release_json
                    release_json=$(curl -s "https://api.github.com/repos/$GITHUB_REPO/releases?per_page=100")
                    tag=$(echo "$release_json" | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"//;s/"//' | grep -v "^dev-build$" | head -n1)
                fi
                [ -z "$tag" ] && echo -e "${RED}[ERROR] No release found${RST}" && sleep 2 && break
                local prefix="Solace"
                [ "$sel" = "dev" ] && prefix="Solace-Dev"
                URL="https://github.com/$GITHUB_REPO/releases/download/${tag}/${prefix}-${RELEASE_ARCH}.zip"
                TMP_DIR="$(mktemp -d ~/Solace_update_XXXXXX)"
                cd "$TMP_DIR" || continue
                curl -L --fail "$URL" -o update.zip
                unzip -o update.zip >/dev/null 2>&1
                cp -r . "$SOLACE_DIR"/
                cat > "$SETTINGS_FILE" << JSONEOF
{
  "installMode": "prebuilt",
  "branch": "$sel",
  "version": "$tag",
  "updatedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
JSONEOF
                echo "$tag" > "$VERSION_FILE"
                rm -rf "$TMP_DIR"
                echo "[Solace] Switched to $sel ($tag)"
                sleep 5
                ;;
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
                    rm -f "$SOLACE_DIR/launcher/Data/app.db"
                    echo "[Solace] Account database reset."
                    sleep 2
                fi ;;
            "Uninstall")
                printf '\033[H\033[J'; show_banner
                section_title "UNINSTALL"
                echo "  This will permanently remove all Solace files."; echo ""
                CONFIRM=$(printf "No, cancel\nYes, remove everything" | fzf --height=15% --reverse --border --prompt="Uninstall? > ")
                if [ "$CONFIRM" = "Yes, remove everything" ]; then
                    force_stop_server
                    rm -rf "$SOLACE_DIR"
                    echo "[Solace] Uninstalled inside Ubuntu."
                    echo "Run 'rm /data/data/com.termux/files/usr/bin/earth' to finish."
                    sleep 3
                    exit 0
                fi ;;
        esac
    done
}

# ─── INFORMATION ────────────────────────────────

info_panel() {
while true; do
    clear; show_banner
    section_title "INFORMATION"
    echo
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
    echo "    $SOLACE_DIR/staticdata/resourcepacks/vanilla.zip"
    echo ""
    echo "  Solace Storage:"
    echo "    Files are stored inside Ubuntu using proot-distro"
    echo "    Enter Ubuntu with: proot-distro login ubuntu"
    echo ""
    echo "  APK:"
    echo "    Patch your own Minecraft Earth app and set IP to 127.0.0.1"
    echo ""
    CHOICE=$(printf "Back" | fzf --height=15% --reverse --border --prompt="Info > ")
    [ "$CHOICE" = "Back" ] && return
done
}

open_admin_panel() {
    termux-open-url "http://127.0.0.1:5000" 2>/dev/null || \
    echo "Open: http://127.0.0.1:5000"
    sleep 2
}

# ─── BANNER ──────────────────────────────────────

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

# ─── MAIN DASHBOARD LOOP ─────────────────────────

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

load_settings

while true; do
    printf '\033[H\033[J'; tput civis 2>/dev/null; show_banner

    LOCAL_IP="127.0.0.1"
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
    echo -e "  │ ${CYN}[3]${RST} Open Admin Panel                        │"
    echo -e "  │ ${CYN}[4]${RST} Update Solace                           │"
    echo -e "  │ ${CYN}[5]${RST} Settings                                │"
    echo -e "  │ ${CYN}[6]${RST} Information                             │"
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
        1)
            if is_process_alive; then
                CH=$(printf "Yes\nNo" | fzf --height=20% --reverse --border --prompt="Stop server? > ")
                [ "$CH" = "Yes" ] && stop_server
            else
                first_start_checks && start_server
            fi
            if is_process_alive && [ -f "$EULA_FILE" ] && ! grep -q "eula=true" "$EULA_FILE" 2>/dev/null; then
                eula_confirmation_loop
            fi
            ;;
        2) process_viewer ;;
        3) open_admin_panel ;;
        4) update_solace ;;
        5) settings_menu ;;
        6) info_panel ;;
         0|q)
            if is_process_alive; then
                clear; show_banner
                section_title "SERVER IS RUNNING"
                echo ""
                echo "  Exiting the TUI will cause the server"
                echo "  to stop. Please turn off the server"
                echo "  from the Admin Panel first to avoid"
                echo "  unexpected issues."
                echo ""
                CHOICE=$(printf "Cancel — Return to menu\nExit — Exit TUI (server will stop)" | fzf \
                    --height=20% --reverse --border --prompt="Exit? > ")
                [ "$CHOICE" = "Exit — Exit TUI (server will stop)" ] || continue
            fi
            tput cnorm 2>/dev/null; clear; break ;;
    esac
done

DASHBOARD
