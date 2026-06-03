<p align="center">
  <img src="images/solace_branding.png" alt="Solace" width="80%">
</p>

<p align="center">
  <a href="./LICENSE"><img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="License"></a>
  <img src="https://img.shields.io/github/stars/Earth-Restored/Solace?style=flat-square" alt="GitHub Stars">
  <img src="https://img.shields.io/github/downloads/Earth-Restored/Solace/total?style=flat-square" alt="GitHub Downloads">
</p>

Replacement server for Minecraft Earth™, based on [Vienna](https://github.com/Project-Genoa/Vienna)

> [!WARNING]
> **Work In Progress (WIP):** This project is currently under active development. Some features may be incomplete, and you may encounter bugs or breaking changes. Use at your own risk!

## Disclaimer

**Solace** is an independent, community-driven project and is **not affiliated with, authorized, maintained, endorsed, or sponsored** by Microsoft Corporation, Mojang Studios, or any of their affiliates or subsidiaries.

* *Minecraft Earth™* is a trademark of Microsoft Corporation. All trademarks and registered trademarks are the property of their respective owners.
* This project does not distribute, host, or provide access to original game assets, proprietary binaries, or resource packs. Users are responsible for providing their own legally obtained assets.
* This software is provided solely for educational, research, and archival purposes to restore functionality to a discontinued service.
* This project is provided "as-is" without any warranty of any kind, express or implied. In no event shall the authors be held liable for any claim, damages, or other liability.

## Features

| Feature       | Status             | Notes                                                                                    |
|---------------|--------------------|------------------------------------------------------------------------------------------|
| Map           | :white_check_mark: |                                                                                          |
| Profile       | :construction:     | Loads, can view activity log/settings, cannot chage skin, statistics are not implemented |
| Journal       | :white_check_mark: |                                                                                          |
| Activity Log  | :white_check_mark: |                                                                                          |
| Inventory     | :white_check_mark: |                                                                                          |
| Crafting      | :white_check_mark: |                                                                                          |
| Smelting      | :white_check_mark: |                                                                                          |
| Boosts        | :white_check_mark: |                                                                                          |
| Boost Minis   | :construction:     | List loads, activation does not work                                                     |
| Tappables     | :white_check_mark: | Map is not used for generation, e.g. tappables can spawn on water                        |
| Buildplates   | :white_check_mark: |                                                                                          |
| Store         | :white_check_mark: | Tab titles do not load                                                                   |
| Challenges    | :construction:     |                                                                                          |
| Seasons       | :construction:     |                                                                                          |
| Adventures    | :construction:     |                                                                                          |
| Tutorial      | :x:                |                                                                                          |
| Daily rewards | :x:                |                                                                                          |

:white_check_mark: - Complete

:construction: - Under Development

:x: - Not Working

## Installation

For installation instructions, refer to [Installation.md](Installation.md)

## Common Errors & Troubleshooting

### I cannot see the "Start Server" button when logged in

**Cause:** Only the very first account created on the launcher is granted full administrative permissions by default. Subsequent accounts lack the necessary privileges to manage the server.

**Solutions:**

* **Option A (Grant Permissions):** Log into the original (first) account and use the Manage Users/Roles page to grant server permissions to your second account.
* **Option B (Reset Database):** If you have lost access to the first account and need to start fresh, you can reset the user database.
  * Navigate to: `launcher/Data/`
  * **Delete** the `app.db` file.
  * *Note: This will remove all existing accounts and allow you to register a new primary admin account.*

### When I open the app, I get "Cannot connect to the network! ..."

**Possible causes**:

* Server is not running
* Incorrect PC IP address
* Firewall blocks the server
* PC and phone are not on the same network

### The app closes when I join a buildplate

**Possible causes**:

* The server took too long to start - quickly open the app and join the same buildplate again
* You do not have Java 17 installed - check that the `JAVA_HOME` environment variable is set to Java 17, or that `java --version` prints java 17
