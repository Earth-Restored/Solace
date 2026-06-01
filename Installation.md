# Installation

## System Requirements

### Hardware & OS

* **PC:** Windows or Linux (macOS is currently untested).
* **Mobile:** High-end Android devices via Termux.

### Software Dependencies

Installed automatically if using the [Semi-automatic installation method](#semi-automatic-linux-macos-termux-only)

* [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
* Java 17 (JRE or JDK), newer versions may not work
* On linux, [powershell](https://learn.microsoft.com/en-us/powershell/scripting/install/linux-overview) to run the build script

## Setup Prerequisites

* Before you start, you'll need to know the IP address of your PC
* Windows
  * Type `ipconfig` and press enter
  * Look for either `Wireless LAN adapter Wi-Fi` if you use WiFi or `Ethernet adapter Ethernet` if you use ethernet
  * Under it, there should be `IPv4 Address`
* Linux
  * Use a command such as `ip address`, `hostname -I` or `ifconfig -a`
* The address will usually (but not always) be in the format `192.168.XXX.XXX`

## Installation Methods

### Semi-automatic (Linux, macOS, Termux only)

* Open your terminal and run the following command:

```bash
curl -sSl https://raw.githubusercontent.com/Earth-Restored/Solace/main/install.sh | bash
```

* Wait until the command finishes
* Continue following the guide from the 4th point of the "Server" part in the Manual instructions

### Manual

#### Server

1. Clone the repository by running the following command on your terminal:

    ```shell
    git clone https://github.com/Earth-Restored/Solace.git
    ```

2. `cd` to the Solace directory, then run `publish.ps1 -profiles framework-dependent-{os}-{arch}`, replace `{os}` with you os (win, linux, osx) and `{arch}` with the cpu architecture (x64, x86, arm64, arm32), e.g. `framework-dependent-win-x64`
3. Run "run_launcher.ps1"
4. Now on the same device open http://localhost:5000, create an account, make sure you confirm your email on the page that opens, if you fail to do this, you need to [Delete account db (Option B)](README.md#i-cannot-see-the-start-server-button-when-logged-in), and login
5. Under "Server Options", set "Network/IPv4 Address" to your PC's IP address and either disable "Map/Enable Tile Rendering" or set the "Map/MapTiler API Key" (it can be found [here](https://cloud.maptiler.com/account/keys/) when logged in)
6. Under "Server Status", click "Start"
7. Accept the Minecraft Server's EULA when prompted in the Launcher's logs
8. Download and move the "resourcepack" file as described in the Launcher's logs

#### Client

* For iOS you can use [this patcher](https://github.com/catdogmat/ProjectEarthiOSPatcher), but it is not officially supported. Installation methods other than AltStore may not work

| Feature | Project Earth patcher | MCE Patcher |
| ------- | --------------------- | ----------- |
| Target Device | Android | Android |
| Patcher Runs On | Android | Windows, Linux, macOS |
| Login | Microsoft account only | Microsoft or custom |
| Shop | Requires that you have played the game before it shut down using the microsoft account | Always works if you use custom login |

##### Project Earth patcher

1. Download [the patcher](https://archive.org/download/dev.projectearth.patcher-1.0/dev.projectearth.patcher-1.0.apk)
2. Install the patcher on your device
3. Make sure you have a LEGAL copy of Minecraft Earth installed on that same device
4. Open the patcher, press on the 3 dots then go to Settings
5. Under Locator Server, set the following: `http://{ip}:8080`, replace `{ip}` with your PC's ip or hostnamr, **make sure you have http:// instead of https://**
6. Now go back and start patching
7. Once that's done, congratulations! You can now open the newly installed app and play Minecraft Earth!

##### MCE patcher

1. Download [the patcher](https://github.com/Earth-Restored/Minecraft_Earth_Patcher/releases) (UI is highly recommended) or build it from source
2. Acquire a Minecraft Earth apk, such as by dumping in from you phone.
3. Run the patcher.
4. Select the downloaded APK file.
5. Change locator Hostname/IP to `{ip}:8080`, replace `{ip}` with your PC's ip or hostname
    * If you want to use non microsoft login, change the options like so (enabled by default in simple UI mode. Use the same ip/hostname as the locator):
    ![Correct options for replacement server](https://github.com/Earth-Restored/Solace/blob/main/images/patcher-login-server-options.png?raw=true)
6. Click patch
7. Move the patched apk to your phone and install it
8. Once that's done, congratulations! You can now open the newly installed app and play Minecraft Earth!

### Launcher Buildplate Preview

1. To enable the buildplate preview, you must first obtain the Minecraft 1.20.4 resource pack.
2. The simplest method is to extract the files directly from the game's JAR:
    * Locate and open '1.20.4.jar' in your Minecraft installation folder using an archive viewer (like 7-Zip).
    * Navigate to the 'assets/minecraft/' directory.
3. Copy all folders from 'assets/minecraft/' and paste them into:
    * 'staticdata/resourcepacks/java/minecraft/'
4. Finally, toggle 'Enable Buildplate Preview in Launcher' within ServerOptions/Data Handling.
