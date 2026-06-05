#!/usr/bin/env bash

REMOTE_URL="https://raw.githubusercontent.com/Earth-Restored/Solace/refs/heads/main/distros/Linux.sh"
SELF_PATH="$(realpath "$0")"

RED='\033[1;31m'
GRN='\033[1;32m'
YLW='\033[1;33m'
CYN='\033[1;36m'
RST='\033[0m'

SERVICE="solace.service"
SOLACE_DIR="${SOLACE_DIR:-$HOME/solace-server/Solace}"
EULA_FILE="$SOLACE_DIR/staticdata/server_template_dir/eula.txt"
RESOURCEPACK="$SOLACE_DIR/staticdata/resourcepacks/vanilla.zip"

ARCH=$(uname -m)
case "$ARCH" in
    x86_64)  RELEASE_ARCH="linux-x64"   ;;
    aarch64|arm64) RELEASE_ARCH="linux-arm64" ;;
    *)
        echo "Unsupported architecture: $ARCH"
        exit 1
        ;;
esac

mkdir -p "$SOLACE_DIR"

echo "Checking for updates..."

update_self() {
    command -v curl >/dev/null 2>&1 || return

    TMP_PATH="$(mktemp /tmp/.earth_update_XXXXXX)"

    curl -fsSL --max-time 5 "$REMOTE_URL" -o "$TMP_PATH" 2>/dev/null

    if [ -s "$TMP_PATH" ]; then
        chmod +x "$TMP_PATH"

        if ! cmp -s "$TMP_PATH" "$SELF_PATH"; then
            mv "$TMP_PATH" "$SELF_PATH"
            echo "[Solace] updated"
            echo "[Solace] restarting..."
            exec "$SELF_PATH" "$@"
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

    exit 0
fi

# ─── UNINSTALL SUBCOMMAND ─────────────────────────────────----

if [ "$1" = "uninstall" ]; then
    echo "[Solace] Stopping and disabling service..."
    sudo systemctl stop "$SERVICE" 2>/dev/null
    sudo systemctl disable "$SERVICE" 2>/dev/null
    sudo rm -f /etc/systemd/system/"$SERVICE"
    sudo systemctl daemon-reload 2>/dev/null

    echo "[Solace] Removing Solace files..."
    rm -rf "$SOLACE_DIR"

    echo "[Solace] Removing earth command..."
    sudo rm -f "$SELF_PATH"

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
    echo "Platform: Linux - systemd service"
    echo ""
    exit 0
fi

# ─── CORE FUNCTIONS ───────────────────────────────────────────

is_running() {
    systemctl is-active --quiet "$SERVICE" 2>/dev/null
}

start_server() {
    if is_running; then
        return
    fi

    echo "[Solace] Starting server..."
    sudo systemctl start "$SERVICE" 2>/dev/null || {
        echo "[Solace] Failed to start service. Try running as root or ensure the service exists."
        sleep 2
        return
    }

    clear
    echo "[Solace] server is now running."
    echo ""
    echo "Admin Panel:"
    echo "http://127.0.0.1:5000"
    sleep 1
}

stop_server() {
    if ! is_running; then
        return
    fi

    echo "[Solace] Stopping server..."
    sudo systemctl stop "$SERVICE" 2>/dev/null

    clear
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
        echo "It seems that resource packs"
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
    if is_running; then
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

# ─── STATUS / LOGS ────────────────────────────────────────────

status_viewer() {
    while true; do
        clear
        echo "==== SERVICE STATUS ===="
        sudo systemctl status "$SERVICE" 2>/dev/null || echo "[Solace] Service not found"
        echo ""

        CH=$(printf "Logs (last 50)\nLogs (follow)\nBack" | fzf \
            --height=20% \
            --reverse \
            --border \
            --prompt="Status > ")

        case "$CH" in
            "Logs (last 50)")
                clear
                sudo journalctl -u "$SERVICE" -n 50 --no-pager 2>/dev/null
                echo ""
                printf "Press ENTER to return..."
                read -r
                ;;
            "Logs (follow)")
                sudo journalctl -u "$SERVICE" -f
                ;;
            "Back")
                return
                ;;
        esac
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

        TMP_DIR="$(mktemp -d /tmp/Solace_update_XXXXXX)"
        cd "$TMP_DIR" || return

        echo "[Solace] downloading $DISPLAY_TAG..."
        curl -L --fail "$URL" -o update.zip
        unzip -o update.zip >/dev/null 2>&1

        echo "[Solace] applying update ($DISPLAY_TAG)..."
        cp -r ./* "$SOLACE_DIR"/ 2>/dev/null || true
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
        sudo systemctl stop "$SERVICE" 2>/dev/null
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
    echo "  - The systemd service file"
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

    echo "[Solace] Stopping and disabling service..."
    sudo systemctl stop "$SERVICE" 2>/dev/null
    sudo systemctl disable "$SERVICE" 2>/dev/null
    sudo rm -f /etc/systemd/system/"$SERVICE"
    sudo systemctl daemon-reload 2>/dev/null

    echo "[Solace] Removing Solace files..."
    sudo rm -rf "${SOLACE_DIR%/Solace}"

    echo "[Solace] Removing earth command..."
    sudo rm -f "$SELF_PATH"

    echo "[Solace] Solace has been uninstalled."
    sleep 2
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
        echo "- Location: $SOLACE_DIR/staticdata/resourcepacks/vanilla.zip"
        echo
        echo "Solace Storage:"
        echo "- Files are stored at: $SOLACE_DIR"
        echo
        echo "Admin Panel Configuration:"
        echo "- If running on the same machine, use IP: 127.0.0.1"
        echo
        echo "MapTiler Setup:"
        echo "- Create an API key at: https://cloud.maptiler.com/account/keys/"
        echo "- Add the API key inside the server admin panel settings"
        echo
        echo "Service Management:"
        echo "- Status:  systemctl status solace.service"
        echo "- Start:   sudo systemctl start solace.service"
        echo "- Stop:    sudo systemctl stop solace.service"
        echo "- Logs:    journalctl -u solace.service -f"
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

# ─── ADMIN PANEL ──────────────────────────────────────────────

open_admin_panel() {
    if command -v xdg-open &>/dev/null; then
        xdg-open "http://127.0.0.1:5000" 2>/dev/null
    elif command -v sensible-browser &>/dev/null; then
        sensible-browser "http://127.0.0.1:5000" 2>/dev/null
    else
        echo "Open: http://127.0.0.1:5000"
    fi
    sleep 2
}

# ─── MAIN MENU LOOP ───────────────────────────────────────────

while true; do
    clear

    if is_running; then
        TITLE="Solace [RUNNING] http://127.0.0.1:5000"
    else
        TITLE="Solace [STOPPED]"
    fi

    OPTIONS=(
        "Start/Stop Server"
        "Status & Logs"
        "Open Admin Panel"
        "Update Solace"
        "Uninstall Solace"
        "Information"
        "Exit"
    )

    CHOICE=$(printf "%s\n" "${OPTIONS[@]}" | fzf \
        --height=30% \
        --reverse \
        --border \
        --prompt="$TITLE > " \
        --no-multi \
        --ansi)

    case "$CHOICE" in
        "Start/Stop Server")
            toggle_server
            ;;
        "Status & Logs")
            status_viewer
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
