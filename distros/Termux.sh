#!/data/data/com.termux/files/usr/bin/bash

REMOTE_URL="https://raw.githubusercontent.com/Earth-Restored/Solace/refs/heads/main/distros/Termux.sh"
SELF_PATH="$(realpath "$0")"

RELEASE_ARCH="linux-arm64"

echo "Checking for updates..."

update_self() {
    command -v curl >/dev/null 2>&1 || return

    TMP_PATH="$(mktemp /data/data/com.termux/files/usr/tmp/.earth_update_XXXXXX)"

    curl -fsSL --max-time 5 "$REMOTE_URL" -o "$TMP_PATH" 2>/dev/null

    if [ -s "$TMP_PATH" ]; then
        chmod +x "$TMP_PATH"

        if ! cmp -s "$TMP_PATH" "$SELF_PATH"; then
            mv "$TMP_PATH" "$SELF_PATH"
            echo "[Solace] updated"

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

# ─── EULA SUBCOMMAND ──────────────────────────────────────────

if [ "$1" = "eula" ]; then
    EULA_ACTION="$2"

    TMP_SCRIPT=$(mktemp)

    cat > "$TMP_SCRIPT" << 'EOF'
#!/usr/bin/env bash

export TERM=xterm

EULA_FILE="$HOME/Solace/staticdata/server_template_dir/eula.txt"

if [ "$EULA_ACTION" = "--delete" ]; then
    rm -f "$EULA_FILE"
    echo "[Solace]: The eula file has been deleted. You can now start the server to generate a new one."
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

# ─── UNINSTALL SUBCOMMAND ─────────────────────────────────----

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
    rm -f "$SELF_PATH"

    echo "[Solace] Solace has been uninstalled."
    exit 0
fi

# ─── HELP SUBCOMMAND ──────────────────────────────────────────

if [ "$1" = "help" ] || [ "$1" = "--help" ] || [ "$1" = "-h" ]; then
    echo -e "\033[1;34m"
    echo "   _____       __"
    echo "  / ___/____  / /___ _________"
    echo "  \__ \/ __ \/ / __ \`/ ___/ _ \\"
    echo " ___/ / /_/ / / /_/ / /__/  __/"
    echo "/____/\____/_/\__,_/\___/\___/"
    echo -e "\033[0m"
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

# ─── MAIN SCRIPT (runs inside proot-distro) ───────────────────

proot-distro login ubuntu -- bash << 'EOF'

DB=~/Solace/nohup.log
PID_FILE=~/Solace/server.pid
TIME_FILE=~/Solace/server.start
SOLACE_DIR=~/Solace
EULA_FILE="$SOLACE_DIR/staticdata/server_template_dir/eula.txt"
RESOURCEPACK="$SOLACE_DIR/staticdata/resourcepacks/vanilla.zip"
RELEASE_ARCH="linux-arm64"

mkdir -p "$SOLACE_DIR"

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

start_server() {
    if is_process_alive; then
        return
    fi

    cd "$SOLACE_DIR" || exit 1

    export DOTNET_ROOT=$HOME/.dotnet
    export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools
    export COMPlus_gcServer=0

    nohup setsid pwsh run_launcher.ps1 > "$DB" 2>&1 < /dev/null &
    PID=$!
    disown

    echo "$PID" > "$PID_FILE"
    date +%s > "$TIME_FILE"

    clear
    echo -e "\033[1;34m"
    echo "   _____       __"
    echo "  / ___/____  / /___ _________"
    echo "  \__ \/ __ \/ / __ \`/ ___/ _ \\"
    echo " ___/ / /_/ / / /_/ / /__/  __/"
    echo "/____/\____/_/\__,_/\___/\___/"
    echo -e "\033[0m"
    echo ""
    echo "[Solace] server is now running."
    echo ""
    echo "Admin Panel: http://127.0.0.1:5000"
    sleep 1
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

    clear
    echo -e "\033[1;34m"
    echo "   _____       __"
    echo "  / ___/____  / /___ _________"
    echo "  \__ \/ __ \/ / __ \`/ ___/ _ \\"
    echo " ___/ / /_/ / / /_/ / /__/  __/"
    echo "/____/\____/_/\__,_/\___/\___/"
    echo -e "\033[0m"
    echo ""
    echo "[Solace] server stopped."
    sleep 1
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
        echo "======================================="
        echo ""
        printf "Back\n" | fzf \
            --height=15% \
            --reverse \
            --border \
            --prompt="Resource Packs > " >/dev/null
        return 1
    fi

    if [ ! -f "$EULA_FILE" ]; then
        clear
        echo "======================================="
        echo "           FIRST TIME SETUP"
        echo "======================================="
        echo ""
        echo "After startup, create an account"
        echo "using the admin panel and follow"
        echo "steps 4-6 in the manual server"
        echo "setup instructions on the"
        echo "Solace GitHub repository."
        echo ""
        echo 'Use: "earth eula" after the setup.'
        echo ""
        echo "======================================="
        echo ""
        CHOICE=$(printf "Continue\nBack" | fzf \
            --height=15% \
            --reverse \
            --border \
            --prompt="Continue Startup > ")
        [ "$CHOICE" = "Continue" ] || return 1
        return 0
    fi

    if grep -q "eula=false" "$EULA_FILE"; then
        clear
        echo "======================================="
        echo "            EULA REQUIRED"
        echo "======================================="
        echo ""
        echo "Please run:"
        echo ""
        echo "earth eula"
        echo ""
        echo "to accept the Minecraft EULA"
        echo "before starting the server."
        echo ""
        echo "======================================="
        echo ""
        printf "Back\n" | fzf \
            --height=15% \
            --reverse \
            --border \
            --prompt="EULA > " >/dev/null
        return 1
    fi

    return 0
}

toggle_server() {
    if is_process_alive; then
        CH=$(printf "Yes\nNo" | fzf \
            --height=20% \
            --reverse \
            --border \
            --prompt="Stop server? > ")
        [ "$CH" = "Yes" ] && stop_server
    else
        first_start_checks || return
        start_server
    fi
}

process_viewer() {
while true; do

clear

echo -e "\033[1;34m"
echo "   _____       __"
echo "  / ___/____  / /___ _________"
echo "  \__ \/ __ \/ / __ \`/ ___/ _ \\"
echo " ___/ / /_/ / / /_/ / /__/  __/"
echo "/____/\____/_/\__,_/\___/\___/"
echo -e "\033[0m"

PID=$(get_pid)

if is_running && [ -f "$TIME_FILE" ]; then
    NOW=$(date +%s)
    START=$(cat "$TIME_FILE")
    UPTIME_SEC=$((NOW - START))

    DAYS=$((UPTIME_SEC/86400))
    HOURS=$(((UPTIME_SEC%86400)/3600))
    MINS=$(((UPTIME_SEC%3600)/60))

    UPTIME_TEXT="${DAYS}d ${HOURS}h ${MINS}m"
else
    UPTIME_TEXT="--"
fi

LOAD=$(uptime 2>/dev/null | awk -F'load average:' '{print $2}' | cut -c2-)

MEM_TOTAL=$(grep MemTotal /proc/meminfo 2>/dev/null | awk '{print $2}')
MEM_AVAIL=$(grep MemAvailable /proc/meminfo 2>/dev/null | awk '{print $2}')

if [ -n "$MEM_TOTAL" ] && [ -n "$MEM_AVAIL" ]; then
    MEM_USED=$((MEM_TOTAL - MEM_AVAIL))
    MEM_PCT=$(awk "BEGIN {printf \"%.1f\", ($MEM_USED/$MEM_TOTAL)*100}")
else
    MEM_PCT="?"
fi

PROC_COUNT=$(ps -eo cmd 2>/dev/null | grep -E "pwsh|Launcher|ApiServer|EventBus|ObjectStore|TileRenderer|BuildplateLauncher" | grep -v grep | wc -l)

if is_running; then
    echo "Solace [RUNNING] http://127.0.0.1:5000"
    printf "Uptime: %s | RAM: %s%% | Processes: %s\n" \
    "$UPTIME_TEXT" "$MEM_PCT" "$PROC_COUNT"
else
    echo "Solace [STOPPED]"
fi

echo "-----------------------------------------------"
printf "Load: %s\n" "$LOAD"
echo "-----------------------------------------------"
echo ""

SELECT=$(
{
echo "Back to Main Menu"

if [ -n "$PID" ]; then
    ps -eo pid,ppid,cmd --no-headers 2>/dev/null | \
    grep -E "pwsh|Launcher|ApiServer|EventBus|ObjectStore|TileRenderer|BuildplateLauncher" | \
    grep -v grep
fi
} | fzf --height=50% --reverse --border --prompt="Process > "
)

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

    CH=$(printf "Refresh\nBack" | fzf \
        --height=10% \
        --reverse \
        --border \
        --prompt="Log > ")

    [ "$CH" = "Back" ] && break
done

done
}

# ─── UPDATE ───────────────────────────────────────────────────

update_solace() {
    while true; do
        clear

        echo "================================================"
        echo "               UPDATE SOLACE"
        echo "================================================"
        echo ""

        CURRENT_VERSION="unknown"
        [ -f "$SOLACE_DIR/version.txt" ] && CURRENT_VERSION=$(cat "$SOLACE_DIR/version.txt")

        CHOICE=$(printf "Stable (recommended)\nDev Build (not recommended - may break)\nNo" | fzf \
            --height=20% \
            --reverse \
            --border \
            --prompt="Select update branch > ")

        [ "$CHOICE" = "No" ] && return

        if echo "$CHOICE" | grep -q "Dev"; then
            clear
            echo "================================================"
            echo "  WARNING: Dev builds are unstable and may"
            echo "  break your server. Only use for testing."
            echo ""
            echo "  Updating from a stable build to a dev build"
            echo "  can cause database corruption or data loss."
            echo "================================================"
            echo ""
            CONFIRM=$(printf "No, go back\nYes, continue anyway" | fzf \
                --height=15% \
                --reverse \
                --border \
                --prompt="Are you sure? > ")
            [ "$CONFIRM" != "Yes, continue anyway" ] && continue

            TAG="dev-build"
            ARTIFACT_PREFIX="Solace-Dev"
            DISPLAY_TAG="dev-build"
        else
            RELEASE_JSON=$(curl -s https://api.github.com/repos/Earth-Restored/Solace/releases)
            ALL_TAGS=$(echo "$RELEASE_JSON" | grep '"tag_name"' | cut -d '"' -f4)
            LATEST_TAG=$(echo "$ALL_TAGS" | grep -v "^dev-build$" | head -n1)

            if [ -z "$LATEST_TAG" ]; then
                echo "[ERROR] Failed to get latest stable version."
                sleep 2
                return
            fi

            TAG="$LATEST_TAG"
            ARTIFACT_PREFIX="Solace"
            DISPLAY_TAG="$TAG"
        fi

        echo ""
        echo "Current version: $CURRENT_VERSION"
        echo "Selected: $DISPLAY_TAG"
        echo ""

        CONFIRM=$(printf "Cancel\nDownload" | fzf \
            --height=15% \
            --reverse \
            --border \
            --prompt="Confirm > ")
        [ "$CONFIRM" != "Download" ] && continue

        force_stop_server

        echo "[Solace] preparing download for $DISPLAY_TAG..."

        URL="https://github.com/Earth-Restored/Solace/releases/download/${TAG}/${ARTIFACT_PREFIX}-${RELEASE_ARCH}.zip"

        TMP_DIR="$(mktemp -d ~/Solace_update_XXXXXX)"
        cd "$TMP_DIR" || return

        echo "[Solace] downloading $DISPLAY_TAG..."
        curl -L --fail "$URL" -o update.zip
        unzip -o update.zip >/dev/null 2>&1

        echo "[Solace] applying update ($DISPLAY_TAG)..."
        cp -r . "$SOLACE_DIR"/
        echo "$DISPLAY_TAG" > "$SOLACE_DIR/version.txt"
        echo "[Solace] update complete ($DISPLAY_TAG)"

        rm -rf "$TMP_DIR"
        sleep 2
        return
    done
}

force_stop_server() {
    if is_running; then
        echo "[Solace] stopping server before update..."

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

# ─── UNINSTALL ────────────────────────────────────────────────

uninstall_solace() {
    clear
    echo "================================================"
    echo "            UNINSTALL SOLACE"
    echo "================================================"
    echo ""
    echo "This will permanently remove:"
    echo "  - All Solace server files"
    echo "  - Databases and configuration"
    echo "  - The earth command"
    echo ""
    echo "This action cannot be undone."
    echo "================================================"
    echo ""

    CONFIRM=$(printf "No, cancel\nYes, remove everything" | fzf \
        --height=15% \
        --reverse \
        --border \
        --prompt="Uninstall Solace? > ")

    [ "$CONFIRM" != "Yes, remove everything" ] && return

    force_stop_server

    echo "[Solace] Removing Solace files..."
    rm -rf "$SOLACE_DIR"

    echo "[Solace] Solace has been uninstalled."
    echo "The earth command will be removed once you exit."
    sleep 2

    rm -f /data/data/com.termux/files/usr/bin/earth
    exit 0
}

# ─── INFORMATION ──────────────────────────────────────────────

info_panel() {
while true; do

clear

echo "======================================="
echo " INFORMATION"
echo "======================================="
echo
echo "Resourcepack:"
echo "- Check the server log on the admin panel or ask for help on the Discord server"
echo "- Location: ~/Solace/staticdata/resourcepacks/vanilla.zip"
echo "- This can be accessed using the proot-distro command referred below"
echo
echo "Solace Storage:"
echo "- Files are stored inside Ubuntu using proot-distro"
echo "- Enter Ubuntu with: proot-distro login ubuntu"
echo
echo "Admin Panel Configuration:"
echo "- If you are running a patched Minecraft Earth APK on the same device:"
echo "  Use IP: 127.0.0.1"
echo
echo "MapTiler Setup:"
echo "- Create an API key at: https://cloud.maptiler.com/account/keys/"
echo "- Add the API key inside the server admin panel settings"
echo
echo "APK:"
echo "- Patch your own LEGALLY obtained Minecraft Earth app"
echo "- and set the IP to 127.0.0.1 if you are using it on the same device"
echo
echo "Notes:"
echo "- This setup is intended for local device use only"
echo "- You can change the IP if you want to host it for multiple devices"
echo "- Make sure your APK is patched to match the server IP"
echo
echo "======================================="
echo ""

CHOICE=$(printf "Back\n" | fzf \
    --height=20% \
    --reverse \
    --border \
    --prompt="Info > ")

[ "$CHOICE" = "Back" ] && return

done
}

open_admin_panel() {
    termux-open-url "http://127.0.0.1:5000" 2>/dev/null || \
    echo "Open: http://127.0.0.1:5000"

    sleep 2
}

# ─── BANNER / UI HELPERS ──────────────────────────────────────

show_banner() {
    echo -e "\033[1;34m"
    echo "   _____       __"
    echo "  / ___/____  / /___ _________"
    echo "  \__ \/ __ \/ / __ \`/ ___/ _ \\"
    echo " ___/ / /_/ / / /_/ / /__/  __/"
    echo "/____/\____/_/\__,_/\___/\___/"
    echo -e "\033[0m"
    echo ""
}

# ─── MAIN MENU LOOP ───────────────────────────────────────────

while true; do

clear

show_banner

if is_process_alive; then
    TITLE="Solace [RUNNING] http://127.0.0.1:5000"
else
    TITLE="Solace [STOPPED]"
fi

OPTIONS=(
"Start/Stop Server"
"Process Explorer"
"Open Admin Panel"
"Update Solace"
"Uninstall Solace"
"Information"
"Exit"
)

CHOICE=$(printf "%s\n" "${OPTIONS[@]}" | fzf \
    --height=40% \
    --reverse \
    --border \
    --prompt="$TITLE > " \
    --no-multi \
    --ansi)

case "$CHOICE" in
    "Start/Stop Server")
        toggle_server
        ;;
    "Process Explorer")
        process_viewer
        ;;
    "Open Admin Panel")
        open_admin_panel
        ;;
    "Update Solace")
        update_solace
        ;;
    "Uninstall Solace")
        uninstall_solace
        ;;
    "Information")
        info_panel
        ;;
    "Exit")
        break
        ;;
esac

done

EOF
