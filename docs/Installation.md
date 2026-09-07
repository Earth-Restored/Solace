# Installation

## Dependencies

* [Docker](https://www.docker.com/products/docker-desktop/)
* [.NET 11](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)

## Setup Prerequisites

* Before you start, you'll need to know the IP address of your PC
* Windows
  * Open Settings > Network & Internet > Wi-Fi, select Wi-Fi properties
  * Find "IPv4 address"
* Linux
  * Use a command such as `ip address`, `hostname -I` or `ifconfig -a`
* The address will usually (but not always) be in the format `192.168.XXX.XXX`

## Server

1. Download the latest 1.0.0+ release zip from [releases](https://github.com/Earth-Restored/Solace/releases)
2. Extract the zip (on windows, make sure it is not a OneDrive backed up folder)
3. Open terminal in the directory
4. Run the setup script `dotnet setup.cs`
5. Run either `.\up.ps1` or `./up.sh` depending on your OS
6. 

To stop the server, run `.\down.ps1` or `./down.sh`

## Client

1. Download [the patcher](https://github.com/Earth-Restored/Minecraft_Earth_Patcher/releases) (UI is highly recommended) or build it from source
2. Extract the zip
3.
    * Android - Acquire a Minecraft Earth .apk, such as by dumping in from you phone.
    * iOS - Acquire a Minecraft Earth .ipa file
4. Run the patcher.
5. Select your OS.
6. Select the downloaded apk/ipa file.
7. Enter the information as shown on the home page of web portal. (On iOS, **do not enable Use Custom Auth Server**, it is not yet properly impemented)
8. Click patch
9. Move the patched apk/ipa to your phone and install it
10. Once that's done, congratulations! You can now open the newly installed app and play Minecraft Earth!

### Launcher Buildplate Preview

1. To enable the buildplate preview, you must first obtain the Minecraft 1.20.4 resource pack.
2. The simplest method is to extract the files directly from the game's JAR:
    * Locate and open '1.20.4.jar' in your Minecraft installation folder using an archive viewer (like 7-Zip).
    * Navigate to the 'assets/minecraft/' directory.
3. Copy all folders from 'assets/minecraft/' and paste them into:
    * 'staticdata/resourcepacks/java/minecraft/'
4. Finally, toggle 'Enable Buildplate Preview in Launcher' within ServerOptions/Data Handling.
