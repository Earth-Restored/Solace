#!/usr/bin/env bash

RED='\033[1;31m'
GRN='\033[1;32m'
YLW='\033[1;33m'
ORG='\033[38;5;208m'
CYN='\033[1;36m'
BLU='\033[1;34m'
RST='\033[0m'

banner() {
    echo -e "${BLU}"
    echo "     _____       __"
    echo "    / ___/____  / /___ _________"
    echo "    \__ \/ __ \/ / __ \`/ ___/ _ \\"
    echo "   ___/ / /_/ / / /_/ / /__/  __/"
    echo "  /____/\____/_/\__,_/\___/\___/"
    echo -e "${RST}"
}

help_text() {
    echo ""
    echo -e "${CYN}Usage:${RST} install.sh [OPTIONS]"
    echo ""
    echo "Install Solace - a Minecraft Earth replacement server."
    echo ""
    echo -e "${CYN}Options:${RST}"
    echo "  -h, --help     Show this help message"
    echo ""
    echo -e "${CYN}Platforms:${RST}"
    echo "  Termux (Android)   Auto-detected, uses proot-distro"
    echo "  Linux              Auto-detected, uses systemd"
    echo "  macOS              Auto-detected, uses launchd"
    echo ""
    echo "After installation, run: earth"
    exit 0
}

for arg in "$@"; do
    case "$arg" in
        -h|--help) help_text ;;
    esac
done

print_step() {
    echo ""
    echo -e "${CYN}========================================${RST}"
    echo -e "${CYN}  $1${RST}"
    echo -e "${CYN}========================================${RST}"
}

print_sub() {
    echo -e "  ${BLU}>${RST} $1"
}

ok()   { echo -e "${GRN}[OK] $1${RST}"; }
skip() { echo -e "${YLW}[SKIP] $1${RST}"; }
err()  { echo -e "${RED}[ERROR] $1${RST}"; exit 1; }

GITHUB_REPO="Earth-Restored/Solace"
GITHUB_URL="https://github.com/$GITHUB_REPO.git"

clear && banner

# ─────────────────────────────────────────
#  TERMUX BRANCH
# ─────────────────────────────────────────
if [ -n "$TERMUX_VERSION" ] || echo "$PREFIX" | grep -q "com.termux"; then
    export DEBIAN_FRONTEND=noninteractive
    dpkg --configure -a >/dev/null 2>&1 || true

    print_step "1. CHECKING PROOT-DISTRO"
    if ! command -v proot-distro >/dev/null 2>&1; then
        pkg update -y
        pkg install -y -o Dpkg::Options::="--force-confnew" proot-distro || {
            dpkg --configure -a
            pkg install -y -o Dpkg::Options::="--force-confnew" proot-distro
        }
        hash -r
        command -v proot-distro >/dev/null || err "proot-distro install failed"
        ok "Installed proot-distro"
    else
        skip "Already installed"
    fi

    print_step "2. CHECKING UBUNTU"
    if proot-distro login ubuntu -- true 2>/dev/null; then
        skip "Ubuntu already installed"
    else
        proot-distro install ubuntu
        ok "Ubuntu installed"
    fi

    clear && banner
    print_step "SELECT BRANCH"
    echo ""
    echo -e "${CYN}Select branch:${RST}"
    echo ""
    echo -e "  ${GRN}[1] Main (stable - recommended)${RST}"
    echo -e "  ${YLW}[2] Dev (unstable - may break)${RST}"
    echo ""
    printf "Choice [1/2] > "
    read -r BRANCH_CHOICE < /dev/tty
    BRANCH_CHOICE="$(echo "$BRANCH_CHOICE" | tr -d '\r\n')"

    ARTIFACT_PREFIX="Solace"
    INSTALL_BRANCH="main"
    SELECTED_TAG=""
    case "$BRANCH_CHOICE" in
        2|dev|Dev)
            ARTIFACT_PREFIX="Solace-Dev"
            INSTALL_BRANCH="dev"
            SELECTED_TAG="dev-build"
            echo -e "${YLW}[INFO] Using Dev build${RST}"
            ;;
        *)
            echo "[INFO] Fetching releases..."
            RELEASE_JSON=$(curl -s "https://api.github.com/repos/$GITHUB_REPO/releases?per_page=100")
            SELECTED_TAG=$(echo "$RELEASE_JSON" | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"//;s/"//' | grep -v "^dev-build$" | head -n1)
            [ -z "$SELECTED_TAG" ] && err "No releases found."
            echo "[INFO] Latest main release: $SELECTED_TAG"
            ;;
    esac

    ZIP_NAME="${ARTIFACT_PREFIX}-linux-arm64.zip"
    URL="https://github.com/$GITHUB_REPO/releases/download/${SELECTED_TAG}/${ZIP_NAME}"

    print_step "3. CONFIGURING UBUNTU"
    proot-distro login ubuntu -- bash << EOF 2>/dev/null
echo "[1] System update"
apt update -y

echo "[2] Installing dependencies"
apt install -y wget fzf curl unzip gnupg software-properties-common \
    apt-transport-https ca-certificates openjdk-21-jre libicu-dev

if ! command -v pwsh >/dev/null 2>&1; then
    echo "[3] Installing PowerShell"
    mkdir -p /opt/microsoft/powershell/7
    cd /opt/microsoft/powershell/7
    wget -q https://github.com/PowerShell/PowerShell/releases/download/v7.6.1/powershell-7.6.1-linux-arm64.tar.gz
    tar zxf powershell-7.6.1-linux-arm64.tar.gz
    chmod +x pwsh
    ln -sf /opt/microsoft/powershell/7/pwsh /usr/local/bin/pwsh
fi

if [ ! -d "$HOME/.dotnet" ] || ! "$HOME/.dotnet/dotnet" --list-sdks 2>/dev/null | grep -q "^10\."; then
    echo "[4] Installing .NET 10"
    cd ~
    wget -q https://dot.net/v1/dotnet-install.sh
    chmod +x dotnet-install.sh
    ./dotnet-install.sh --channel 10.0
fi

grep -q DOTNET_ROOT ~/.bashrc || echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
grep -q ".dotnet/tools" ~/.bashrc || echo 'export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools' >> ~/.bashrc
grep -q COMPlus_gcServer ~/.bashrc || {
    echo 'export COMPlus_gcServer=0'         >> ~/.bashrc
    echo 'export COMPlus_gcConcurrent=1'     >> ~/.bashrc
    echo 'export DOTNET_GCHeapHardLimit=268435456' >> ~/.bashrc
}

mkdir -p ~/Solace

echo "[5] Downloading pre-compiled server"
cd ~

if [ -z "$SELECTED_TAG" ]; then
    echo "[ERROR] No release tag found"
    exit 1
fi

echo "[INFO] Downloading ${SELECTED_TAG}..."
curl -L --progress-bar -o "$ZIP_NAME" "$URL" || { echo "[ERROR] Download failed"; exit 1; }
echo -e "  ${GRN}✔${RST} Download complete"
echo -ne "  ${BLU}>${RST} Extracting... "
unzip -o "$ZIP_NAME" >/dev/null 2>&1 && echo -e "${GRN}done${RST}" || { echo -e "${RED}failed${RST}"; exit 1; }
rm -rf ~/Solace/*
echo "$SELECTED_TAG" > ~/Solace/version.txt

if [ -d Solace-linux-arm64 ]; then
    mv Solace-linux-arm64/* ~/Solace/
    rm -rf Solace-linux-arm64
else
    mv run_launcher.ps1 ~/Solace/ 2>/dev/null || true
    mv components       ~/Solace/ 2>/dev/null || true
    mv launcher         ~/Solace/ 2>/dev/null || true
    mv staticdata       ~/Solace/ 2>/dev/null || true
fi

chmod -R +x ~/Solace/components/ 2>/dev/null || true

cat > ~/Solace/settings.json << JSONEOF
{
  "installMode": "prebuilt",
  "branch": "$INSTALL_BRANCH",
  "version": "$SELECTED_TAG",
  "updatedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
JSONEOF

echo "[6] Cleaning installer leftovers"
rm -f ~/dotnet-install.sh
rm -f ~/Solace-linux-arm64.zip

echo "[DONE]"
EOF

    ok "Ubuntu configured"

print_step "4. CREATING EARTH COMMAND"
mkdir -p "$PREFIX/bin"
curl -fsSL "https://raw.githubusercontent.com/$GITHUB_REPO/refs/heads/main/distros/Termux.sh" -o "$PREFIX/bin/earth"
chmod +x "$PREFIX/bin/earth"
ok "earth command installed"

echo ""
echo -e "${GRN}========================================${RST}"
echo -e "${ORG}           INSTALL COMPLETE             ${RST}"
echo -e "${GRN}========================================${RST}"
echo ""
echo -e "  ${CYN}User:${RST}    $(whoami)"
echo -e "  ${CYN}OS:${RST}      Termux (proot-distro ubuntu)"
echo -e "  ${CYN}Arch:${RST}    $(uname -m)"
echo -e "  ${CYN}Mode:${RST}    prebuilt"
echo -e "  ${CYN}Branch:${RST}  $INSTALL_BRANCH"
echo -e "  ${CYN}Server:${RST}  ~/Solace"
echo ""
echo -e "${CYN}Next steps:${RST}"
echo "  1. Download the resource packs (refer to Discord for the commands)"
echo "  2. Run: earth"
echo "  3. Open http://127.0.0.1:5000 and create your admin account"
echo "  4. Under 'Server Options', set Network/IPv4 Address to your PC's IP"
echo "  5. Get a MapTiler API key: https://cloud.maptiler.com/account/keys/"
echo "  6. Under 'Server Status', click Start"
echo "  7. Accept the Minecraft EULA when prompted in the logs"
echo ""
echo -e "${CYN}Useful commands:${RST}"
echo "  earth              TUI menu"
echo "  earth uninstall    remove Solace completely"
echo ""
exit 0
fi

# ─────────────────────────────────────────
#  LINUX / MACOS BRANCH
# ─────────────────────────────────────────

if [ -n "$SUDO_USER" ]; then
    CURRENT_USER="$SUDO_USER"
else
    CURRENT_USER=$(whoami)
fi

HOME_DIR=$(eval echo "~$CURRENT_USER")
SOLACE_DIR="$HOME_DIR/solace"
SERVER_DIR="$SOLACE_DIR/solace-server"
SOURCE_DIR="$SOLACE_DIR/solace-source"
SERVICE_FILE="/etc/systemd/system/solace.service"
SETTINGS_FILE="$SOLACE_DIR/settings.json"
VERSION_FILE="$SOLACE_DIR/version.txt"

OS=$(uname -s)
case $(uname -m) in
    x86_64)        ARCH_PROFILE="x64"   ; JAVA_ARCH="amd64" ;;
    aarch64|arm64) ARCH_PROFILE="arm64" ; JAVA_ARCH="arm64" ;;
    *) err "Unsupported architecture: $(uname -m)" ;;
esac

if [ "$OS" = "Darwin" ]; then
    PROFILE="framework-dependent-osx-$ARCH_PROFILE"
else
    PROFILE="framework-dependent-linux-$ARCH_PROFILE"
fi

export DOTNET_ROOT="$HOME_DIR/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

detect_pkg_manager() {
    if [ "$OS" = "Darwin" ]; then
        PKG_MANAGER="brew"
    elif command -v apt-get &>/dev/null; then
        PKG_MANAGER="apt"
    elif command -v dnf &>/dev/null; then
        PKG_MANAGER="dnf"
    elif command -v pacman &>/dev/null; then
        PKG_MANAGER="pacman"
    elif command -v zypper &>/dev/null; then
        PKG_MANAGER="zypper"
    else
        err "No supported package manager found (apt, dnf, pacman, zypper, brew)."
    fi
    ok "Detected package manager: $PKG_MANAGER"
}

pkg_install() {
    case $PKG_MANAGER in
        apt)    apt-get install -y "$@" ;;
        dnf)    dnf install -y "$@" ;;
        pacman) pacman -S --noconfirm "$@" ;;
        zypper) zypper install -y "$@" ;;
        brew)   sudo -u "$CURRENT_USER" brew install "$@" ;;
    esac
}

pkg_update() {
    case $PKG_MANAGER in
        apt)    apt-get update -qq ;;
        dnf)    dnf check-update -q || true ;;
        pacman) pacman -Sy --noconfirm ;;
        zypper) zypper refresh ;;
        brew)   sudo -u "$CURRENT_USER" brew update ;;
    esac
}

install_java() {
    print_sub "Installing Java 17..."
    case $PKG_MANAGER in
        apt)    pkg_install openjdk-17-jre ;;
        dnf)    pkg_install java-17-openjdk ;;
        pacman) pkg_install jre17-openjdk ;;
        zypper) pkg_install java-17-openjdk ;;
        brew)   pkg_install openjdk@17 ;;
    esac
}

install_pwsh() {
    print_sub "Installing PowerShell..."
    case $PKG_MANAGER in
        apt)
            wget -q "https://packages.microsoft.com/config/$(. /etc/os-release && echo "$ID")/$(. /etc/os-release && echo "$VERSION_ID")/packages-microsoft-prod.deb" \
                -O /tmp/packages-microsoft-prod.deb 2>/dev/null \
            || wget -q "https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb" \
                -O /tmp/packages-microsoft-prod.deb
            dpkg -i /tmp/packages-microsoft-prod.deb 2>/dev/null || true
            apt-get update -qq
            pkg_install powershell
            ;;
        dnf)
            rpm --import https://packages.microsoft.com/keys/microsoft.asc
            dnf install -y "https://packages.microsoft.com/rhel/9/prod/packages-microsoft-prod.rpm" 2>/dev/null || true
            pkg_install powershell
            ;;
        pacman)
            sudo -u "$CURRENT_USER" bash -c "
                git clone https://aur.archlinux.org/powershell-bin.git /tmp/powershell-bin 2>/dev/null || true
                cd /tmp/powershell-bin && makepkg -si --noconfirm 2>/dev/null || true
            " 2>/dev/null || pkg_install powershell-bin 2>/dev/null || pkg_install powershell 2>/dev/null || true
            ;;
        zypper)
            rpm --import https://packages.microsoft.com/keys/microsoft.asc
            zypper addrepo https://packages.microsoft.com/rhel/9/prod/ microsoft 2>/dev/null || true
            pkg_install powershell
            ;;
        brew)
            pkg_install powershell
            ;;
    esac
}

# ─── STEP 1: ROOT CHECK ────────────────────────────────────

print_step "PRE-FLIGHT CHECK"
if [ "$OS" != "Darwin" ] && [ "$EUID" -ne 0 ]; then
    err "Please run the script as root (sudo)!"
fi
detect_pkg_manager

# ─── STEP 2: DEPENDENCY CHECK ──────────────────────────────

MISSING_DEPS=()

check_dep() {
    if ! command -v "$1" >/dev/null 2>&1; then
        MISSING_DEPS+=("$1 ($2)")
    else
        skip "$1 already installed"
    fi
}

check_dep "java"   "Java 17+ JRE"
check_dep "pwsh"   "PowerShell 7+"
check_dep "curl"   "curl"
check_dep "unzip"  "unzip"
check_dep "git"    "git"
check_dep "fzf"    "fzf"

DOTNET_MISSING=false
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks 2>/dev/null | grep -q "^10\."; then
    DOTNET_MISSING=true
    MISSING_DEPS+=("dotnet (.NET 10 SDK)")
else
    skip ".NET 10 already installed"
fi

if [ ${#MISSING_DEPS[@]} -gt 0 ]; then
    echo ""
    echo -e "${YLW}Missing dependencies:${RST}"
    for dep in "${MISSING_DEPS[@]}"; do
        echo -e "  ${RED}✗${RST} $dep"
    done
    echo ""
    echo -e "${CYN}Install missing dependencies now?${RST}"
    echo ""
    printf "Install now? [Y/n] > "
    read -r INSTALL_DEPS < /dev/tty
    INSTALL_DEPS="$(echo "$INSTALL_DEPS" | tr -d '\r\n')"

    if [ "$INSTALL_DEPS" = "n" ] || [ "$INSTALL_DEPS" = "N" ] || [ "$INSTALL_DEPS" = "no" ] || [ "$INSTALL_DEPS" = "No" ]; then
        err "Cannot continue without dependencies. Install them and try again."
    fi

    pkg_update

    for dep in "${MISSING_DEPS[@]}"; do
        case "$dep" in
            java*)   install_java ;;
            pwsh*)   install_pwsh ;;
            curl*)   pkg_install curl ;;
            unzip*)  pkg_install unzip ;;
            git*)    pkg_install git ;;
            fzf*)    pkg_install fzf ;;
            dotnet*) ;;
        esac
    done

    if [ "$DOTNET_MISSING" = "true" ]; then
        print_sub "Installing .NET 10 SDK..."
        wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
        chmod +x /tmp/dotnet-install.sh
        sudo -u "$CURRENT_USER" bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME_DIR/.dotnet" >/dev/null 2>&1
        ok ".NET 10 installed"
    fi

    ok "All dependencies installed"
else
    ok "All dependencies already present"
fi

# ─── STEP 3: INSTALL METHOD CHOICE ─────────────────────────

clear && banner

print_step "INSTALL METHOD"
echo ""
echo -e "${CYN}How would you like to install Solace?${RST}"
echo ""
echo -e "  ${GRN}[1] Prebuilt${RST}     - Download a pre-compiled binary (faster)"
echo -e "  ${YLW}[2] Build from Source${RST} - Clone and compile from source"
echo ""
printf "Choice [1/2] > "
read -r METHOD_CHOICE < /dev/tty
METHOD_CHOICE="$(echo "$METHOD_CHOICE" | tr -d '\r\n')"

case "$METHOD_CHOICE" in
    2|source|Source)
        INSTALL_MODE="source"
        echo -e "${YLW}[INFO] Selected Build from Source${RST}"
        ;;
    *)
        INSTALL_MODE="prebuilt"
        echo -e "${GRN}[INFO] Selected Prebuilt${RST}"
        ;;
esac
echo ""

sudo -u "$CURRENT_USER" mkdir -p "$SOLACE_DIR" 2>/dev/null || mkdir -p "$SOLACE_DIR"

# ─── STEP 4A: PREBUILT PATH ────────────────────────────────

if [ "$INSTALL_MODE" = "prebuilt" ]; then
    clear && banner
    print_step "PREBUILT INSTALL"

    echo ""
    echo -e "${CYN}Select branch:${RST}"
    echo ""
    echo -e "  ${GRN}[1] Main (stable - recommended)${RST}"
    echo -e "  ${YLW}[2] Dev (unstable - may break)${RST}"
    echo ""
    printf "Choice [1/2] > "
    read -r BRANCH_CHOICE < /dev/tty
    BRANCH_CHOICE="$(echo "$BRANCH_CHOICE" | tr -d '\r\n')"

    INSTALL_BRANCH="main"
    case "$BRANCH_CHOICE" in
        2|dev|Dev) INSTALL_BRANCH="dev" ;;
    esac

    if [ "$INSTALL_BRANCH" = "main" ]; then
        print_sub "Fetching available releases..."
        RELEASE_JSON=$(curl -s "https://api.github.com/repos/$GITHUB_REPO/releases?per_page=100")
        ALL_TAGS=$(echo "$RELEASE_JSON" | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"//;s/"//' | grep -v "^dev-build$")
        LATEST_TAG=$(echo "$ALL_TAGS" | head -n1)

        if [ -z "$LATEST_TAG" ]; then
            err "No releases found."
        fi
        SELECTED_TAG="$LATEST_TAG"
        echo -e "${GRN}Latest version: $SELECTED_TAG${RST}"

        ARTIFACT_PREFIX="Solace"
        DISPLAY_TAG="$SELECTED_TAG"
    else
        SELECTED_TAG="dev-build"
        ARTIFACT_PREFIX="Solace-Dev"
        DISPLAY_TAG="dev-build"
        echo -e "${YLW}[INFO] Using Dev build${RST}"
    fi

    echo "[INFO] Downloading $DISPLAY_TAG..."

    ZIP_NAME="${ARTIFACT_PREFIX}-linux-${ARCH_PROFILE}.zip"
    if [ "$OS" = "Darwin" ]; then
        ZIP_NAME="${ARTIFACT_PREFIX}-osx-${ARCH_PROFILE}.zip"
    fi

    URL="https://github.com/$GITHUB_REPO/releases/download/${SELECTED_TAG}/${ZIP_NAME}"

    TMP_DIR=$(mktemp -d "/tmp/solace_install_XXXXXX")
    cd "$TMP_DIR"

    if ! curl -L --progress-bar -o server.zip "$URL"; then
        err "Download failed — check your internet or the release URL"
    fi
    echo -e "  ${GRN}✔${RST} Download complete"

    print_sub "Extracting..."
    if ! command -v unzip &>/dev/null; then
        err "unzip is not installed — run the installer again to auto-install it"
    fi
    if ! unzip -o server.zip >/dev/null 2>&1; then
        err "Extraction failed — downloaded file may be corrupted"
    fi

    mkdir -p "$SERVER_DIR"
    extracted=false
    if [ -d "Solace-linux-${ARCH_PROFILE}" ]; then
        mv "Solace-linux-${ARCH_PROFILE}/"* "$SERVER_DIR/" 2>/dev/null && extracted=true
    elif [ -d "Solace-osx-${ARCH_PROFILE}" ]; then
        mv "Solace-osx-${ARCH_PROFILE}/"* "$SERVER_DIR/" 2>/dev/null && extracted=true
    fi
    if ! $extracted; then
        find . -maxdepth 1 -not -name 'server.zip' -not -name '.' -exec mv {} "$SERVER_DIR/" \; 2>/dev/null
    fi

    chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true

    echo "$DISPLAY_TAG" > "$VERSION_FILE"
    cat > "$SETTINGS_FILE" << JSONEOF
{
  "installMode": "prebuilt",
  "branch": "$INSTALL_BRANCH",
  "version": "$DISPLAY_TAG",
  "updatedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
JSONEOF

    cd /
    rm -rf "$TMP_DIR"
    ok "Solace $DISPLAY_TAG downloaded to $SERVER_DIR"
fi

# ─── STEP 4B: BUILD FROM SOURCE PATH ───────────────────────

if [ "$INSTALL_MODE" = "source" ]; then
    clear && banner
    print_step "BUILD FROM SOURCE"
    echo ""
    echo -e "${CYN}Select branch:${RST}"
    echo ""
    echo -e "  ${GRN}[1] Main (stable - recommended)${RST}"
    echo -e "  ${YLW}[2] Dev (not recommended - may break)${RST}"
    echo ""
    printf "Choice [1/2] > "
    read -r BRANCH_CHOICE < /dev/tty
    BRANCH_CHOICE="$(echo "$BRANCH_CHOICE" | tr -d '\r\n')"

    INSTALL_BRANCH="main"
    case "$BRANCH_CHOICE" in
        2|dev|Dev) INSTALL_BRANCH="dev" ;;
    esac

    command -v git >/dev/null 2>&1 || pkg_install git

    print_sub "Cloning $INSTALL_BRANCH..."
    if [ -d "$SOURCE_DIR/.git" ]; then
        cd "$SOURCE_DIR"
        git remote set-url origin "$GITHUB_URL"
        git fetch origin "$INSTALL_BRANCH"
        git reset --hard "origin/$INSTALL_BRANCH"
        git submodule update --init --recursive
        ok "Repository updated ($INSTALL_BRANCH)"
    else
        rm -rf "$SOURCE_DIR"
        sudo -u "$CURRENT_USER" mkdir -p "$SOURCE_DIR"
        sudo -u "$CURRENT_USER" git clone --recurse-submodules -b "$INSTALL_BRANCH" "$GITHUB_URL" "$SOURCE_DIR"
        cd "$SOURCE_DIR"
        ok "Repository cloned ($INSTALL_BRANCH)"
    fi

    BUILD_DIR="$SOURCE_DIR/build/Release/$PROFILE"

    print_step "BUILDING SOLACE"
    sudo -u "$CURRENT_USER" env \
        DOTNET_ROOT="$HOME_DIR/.dotnet" \
        PATH="$HOME_DIR/.dotnet:$PATH" \
        pwsh ./publish.ps1 --profiles "$PROFILE"
    ok "Build complete"

    print_sub "Copying build output..."
    mkdir -p "$SERVER_DIR"
    cp -r "$BUILD_DIR/"* "$SERVER_DIR/" 2>/dev/null || true
    cp "$BUILD_DIR"/../*.json "$SERVER_DIR/components/" 2>/dev/null || true
    chmod -R +x "$SERVER_DIR/components/" 2>/dev/null || true

    SELECTED_TAG="$INSTALL_BRANCH"
    echo "$SELECTED_TAG" > "$VERSION_FILE"
    cat > "$SETTINGS_FILE" << JSONEOF
{
  "installMode": "source",
  "branch": "$INSTALL_BRANCH",
  "version": "$SELECTED_TAG",
  "updatedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
JSONEOF
    ok "Solace built from $INSTALL_BRANCH"
fi

# ─── STEP 5: PREPARE SERVER ENVIRONMENT ────────────────────

print_step "PREPARING SERVER ENVIRONMENT"
cd "$SERVER_DIR"
cp *.json components/ 2>/dev/null || true
mkdir -p logs/EventBusServer logs/ObjectStoreServer logs/ApiServer logs/TileRenderer
ok "Server environment ready"

chown -R "$CURRENT_USER" "$SOLACE_DIR" 2>/dev/null || true


# ─── STEP 6: INSTALL SERVICE ───────────────────────────────

print_step "INSTALLING SERVICE"

install_service() {
    if [ "$OS" = "Darwin" ]; then
        PLIST="$HOME_DIR/Library/LaunchAgents/com.solace.server.plist"
        PWSH_PATH=$(command -v pwsh)
        cat > "$PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.solace.server</string>
    <key>ProgramArguments</key>
    <array>
        <string>$PWSH_PATH</string>
        <string>./run_launcher.ps1</string>
    </array>
    <key>WorkingDirectory</key>
    <string>$SERVER_DIR</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>DOTNET_ROOT</key>
        <string>$HOME_DIR/.dotnet</string>
        <key>PATH</key>
        <string>$HOME_DIR/.dotnet:/usr/local/bin:/usr/bin:/bin</string>
        <key>DOTNET_SYSTEM_NET_DISABLEIPV6</key>
        <string>1</string>
    </dict>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>$SERVER_DIR/logs/solace.log</string>
    <key>StandardErrorPath</key>
    <string>$SERVER_DIR/logs/solace.err</string>
</dict>
</plist>
EOF
        sudo -u "$CURRENT_USER" launchctl unload "$PLIST" 2>/dev/null || true
        sudo -u "$CURRENT_USER" launchctl load "$PLIST"
        ok "Launchd service installed"
    else
        sudo tee "$SERVICE_FILE" > /dev/null <<EOF
[Unit]
Description=Solace Server Launcher
After=network.target

[Service]
Type=simple
User=$CURRENT_USER
WorkingDirectory=$SERVER_DIR
Environment=TERM=xterm-256color
Environment=DOTNET_ROOT=$HOME_DIR/.dotnet
Environment=PATH=$HOME_DIR/.dotnet:$HOME_DIR/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
Environment=DOTNET_SYSTEM_NET_DISABLEIPV6=1
ExecStart=/usr/bin/pwsh ./run_launcher.ps1
StandardInput=null
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF
        sudo systemctl daemon-reload || skip "systemctl daemon-reload failed"
        sudo systemctl enable solace.service || skip "systemctl enable failed"
        ok "Systemd service installed"
    fi
}

install_service

# ─── STEP 7: INSTALL EARTH COMMAND ─────────────────────────

print_step "INSTALLING EARTH COMMAND"

if [ "$OS" = "Darwin" ]; then
    curl -fsSL "https://raw.githubusercontent.com/$GITHUB_REPO/refs/heads/main/distros/macOS.sh" \
        -o /tmp/earth && sudo mv /tmp/earth /usr/local/bin/earth || err "Failed to download earth command"
else
    curl -fsSL "https://raw.githubusercontent.com/$GITHUB_REPO/refs/heads/main/distros/Linux.sh" \
        -o /tmp/earth && sudo mv /tmp/earth /usr/local/bin/earth || err "Failed to download earth command"
fi
sudo chmod +x /usr/local/bin/earth
ok "earth command installed (/usr/local/bin/earth)"

# ─── COMPLETE ──────────────────────────────────────────────

echo ""
echo -e "${GRN}========================================${RST}"
echo -e "${ORG}           INSTALL COMPLETE             ${RST}"
echo -e "${GRN}========================================${RST}"
echo ""
echo -e "  ${CYN}User:${RST}    $CURRENT_USER"
echo -e "  ${CYN}OS:${RST}      $OS ($PKG_MANAGER)"
echo -e "  ${CYN}Arch:${RST}    $PROFILE"
echo -e "  ${CYN}Mode:${RST}    $INSTALL_MODE"
echo -e "  ${CYN}Branch:${RST}  $INSTALL_BRANCH"
echo -e "  ${CYN}Server:${RST}  $SERVER_DIR"
if [ "$INSTALL_MODE" = "source" ]; then
    echo -e "  ${CYN}Source:${RST}  $SOURCE_DIR"
fi
echo ""
echo -e "${CYN}Next steps:${RST}"
echo "  1. Download the resource packs (refer to Discord for the commands)"
echo "  2. Run: earth"
echo "  3. Open http://127.0.0.1:5000 and create your admin account"
echo "  4. Under 'Server Options', set Network/IPv4 Address to your PC's IP"
echo "  5. Get a MapTiler API key: https://cloud.maptiler.com/account/keys/"
echo "  6. Under 'Server Status', click Start"
echo "  7. Accept the Minecraft EULA when prompted in the logs"
echo ""
echo -e "${CYN}Useful commands:${RST}"
echo "  earth              TUI menu"
echo "  earth uninstall    remove Solace completely"
if [ "$OS" = "Darwin" ]; then
    echo "  tail -f $SERVER_DIR/logs/solace.log       live logs"
else
    echo "  journalctl -u solace.service -f          live logs"
    echo "  systemctl status solace.service          status"
fi
echo ""
