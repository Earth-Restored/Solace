# Installation

## Dependencies

* [Docker](https://www.docker.com/products/docker-desktop/)
* [.NET 11](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)

## Setup Prerequisites

You will need your local IP address so the patched app can connect to your server.

* **Windows:** Settings > Network & Internet > Wi-Fi (or Ethernet) > Wi-Fi properties > Find **IPv4 address**
* **Linux:** Run `ip address`, `hostname -I`, or `ifconfig -a` in terminal
* **macOS:** System Settings > Network > select your active connection > Details > Find **IP Address** (or run `ipconfig getifaddr en0` in terminal)

*Note: Your local IP address will typically start with `192.168.x.x`, `10.x.x.x`, or `172.16.x.x`.*

## Server Setup

1. Download the latest release from [Releases](https://github.com/Earth-Restored/Solace/releases).
2. Extract the ZIP file. *(Note for Windows users: Avoid extracting to a OneDrive-backed folder).*
3. Open a terminal inside the extracted directory.
4. Run the setup script: `dotnet setup.cs`
5. Start the server by running `.\up.ps1` (Windows) or `./up.sh` (Linux/macOS).
6. Check logs with `docker compose logs web-portal`. Scroll up until you see `"SETUP: Initial owner account created!"` along with the admin email and password.
   * *If you forget these credentials later, reset them by setting `WEBPORTAL_ADMINACCOUNTPASSWORD` in your `.env` file.*
7. Open `http://localhost/` in a browser and log in with the admin credentials.
8. **Recommended:** Create a separate account for in-game sign-in. New accounts have no permissions by default; use your admin account to create roles and assign them to users.

To stop the server, run `.\down.ps1` (Windows) or `./down.sh` (Linux/macOS).

## Client Setup

1. Download the [Minecraft Earth Patcher](https://github.com/Earth-Restored/Minecraft_Earth_Patcher/releases) or build it from source.
2. Extract the downloaded archive.
3. Obtain the required game package for your device:
   * **Android:** Acquire a Minecraft Earth `.apk` file (e.g., dumped from your phone).
   * **iOS:** Acquire a Minecraft Earth `.ipa` file.
4. Launch the patcher and select your OS platform.
5. Select your `.apk` or `.ipa` file.
6. Enter the connection details displayed on the Web Portal home page (http://localhost/).
   * *iOS Note:* **Do not enable "Use Custom Auth Server"** as it is not yet fully implemented.
7. Click **Patch**.
8. Transfer the patched `.apk` or `.ipa` file to your mobile device and install it.
    * *iOS Note:* AltStore is recommended.
9. Once that's done, congratulations! You can now open the newly installed Solace app and play Minecraft Earth!
