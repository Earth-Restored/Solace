# Building

## OS

* Linux or Windows (wsl is recommended)

## Dependencies

* [.NET 11](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)
* [Aspire](https://aspire.dev/get-started/install-cli/)
* Java 21
* Native AOT Publishing Toolchains:
  * **Linux Host:**
    * Base tools: `clang` or `gcc`, `zlib1g-dev`, `build-essential`
    * Targeting `linux-x64`: `gcc-x86-64-linux-gnu` (required when building on ARM64 host)
    * Targeting `linux-arm64`: `gcc-aarch64-linux-gnu` (required when building on x64 host)
    * Targeting `linux-arm` (arm32): `gcc-arm-linux-gnueabihf`
    * *Ubuntu/Debian setup:*

    ```bash
    sudo apt-get update && sudo apt-get install -y \
        build-essential zlib1g-dev clang \
        gcc-x86-64-linux-gnu g++-x86-64-linux-gnu binutils-x86-64-linux-gnu \
        gcc-aarch64-linux-gnu g++-aarch64-linux-gnu binutils-aarch64-linux-gnu \
        gcc-arm-linux-gnueabihf g++-arm-linux-gnueabihf binutils-arm-linux-gnueabihf
    ```

  * **Windows Host:**
    * [Visual Studio 2022 C++ Build Tools](https://visualstudio.microsoft.com/visual-cpp-build-tools/) with the **"Desktop development with C++"** workload installed.

## Testing

For local testing:

1) If you have not cloned with submodules, make sure to run `git submodule update --init --recursive`
2) Obtain the resourcepack from <https://cdn.mceserv.net/availableresourcepack/resourcepacks/dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35>, using a tool such as wayback machine
3) Rename it to `vanilla.zip` and put it into `staticdata/resourcepacks`
4) Navigate to `src/Solace.AppHost`
5) Modify `appsettings.Development.json`
    * **Required**
    * Set `Shared/AcceptMinecraftEula` to `true`
    * Change `Shared/PublicEndpoints`, e.g. (Replace `PC_ENDPOINT` with an ip or hostname, note the PC must be able to reach itself through the endpoint, otherwise in-game sign in will not work)

    ``` json
    "PublicEndpoints": {
        "WebPortal": "http://PC_ENDPOINT:5000",
        "Locator": "http://PC_ENDPOINT:8080",
        "AuthServer": "http://PC_ENDPOINT:8088",
        "ApiServer": "http://PC_ENDPOINT:8089",
        "Cdn": "http://PC_ENDPOINT:8090"
    },
    ```

    * Change `BuildplateLauncher/PublicEndPoint` to an ip or hostname, without protocol or port
    * **Optional**
    * Change `Shared/Captcha`options to enable captcha on account creation/sign in
6) Run `dotnet run`
7) The admin account email and password for web portal will be shown in the web portal aspire logs, if you ever forget it, you can reset it by setting `WebPortal/AdminAccountPassword` in `appsettings.Development.json`

## Publishing

1) Navigate to `scripts`
2) Run `upload-docker-registry.ps1` (powershell is required), specify your username and optionally, the image registry, which projects/architectures to upload
3) Navigate to `src/Solace.AppHost`
4) Run `aspire publish`, under aspire-output, `docker-compose` and `.env` will be created
5) Set XXX_IMAGE to your images and fill out the other variables, for more info, find the corresponding entry in appsettings.json
6) Copy the staticdata folder to the directory with docker-compose
