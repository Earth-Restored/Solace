# Building

## OS

* Linux or Windows (WSL is recommended)

## Dependencies

* [.NET 11](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)
* [Aspire](https://aspire.dev/get-started/install-cli/)
* Java 21
* Docker or Podman (set `ASPIRE_CONTAINER_RUNTIME=podman`)

## Local Setup

1) If you have not cloned with submodules, make sure to run `git submodule update --init --recursive`
2) Obtain the resourcepack from [this URL](https://cdn.mceserv.net/availableresourcepack/resourcepacks/dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35), using Wayback Machine
3) Rename it to `vanilla.zip` and put it into `staticdata/resourcepacks`
4) Navigate to `src/Solace.AppHost`
5) Modify `appsettings.Development.json`
    * **Required**
    * Set `Shared/AcceptMinecraftEula` to `true`
    * Change `Shared/PublicEndpoints`. Replace `PC_ENDPOINT` with an IP or hostname. *(Note the PC must be able to reach itself through the endpoint, otherwise in-game sign in will fail.)*

    ``` json
    "PublicEndpoints": {
        "WebPortal": "http://PC_ENDPOINT:5000",
        "Locator": "http://PC_ENDPOINT:8080",
        "AuthServer": "http://PC_ENDPOINT:8088",
        "ApiServer": "http://PC_ENDPOINT:8089",
        "Cdn": "http://PC_ENDPOINT:8090"
    },
    ```

    * Change `BuildplateLauncher/PublicEndpoint` to an IP or hostname, without protocol or port
6) Run `dotnet run`
7) The admin account email and password for web portal will be shown in the web portal logs. If forgotten, reset them by setting `WebPortal/AdminAccountPassword` in `appsettings.Development.json`

## Publishing

1) Navigate to `scripts`
2) Run `upload-docker-registry.ps1`, specify your username and optionally, the image registry and which projects/architectures to upload
3) Navigate to `src/Solace.AppHost`
4) Run `aspire publish`, this generates `docker-compose.yml` and `.env` inside `aspire-output`.
5) Copy the `staticdata` folder to `src/Solace.AppHost/aspire-output`
6) Run `dotnet set-env-file-defaults.cs ./aspire-output/.env` *(Add -o to overwrite existing values.)*
7) Copy the contents of `template` to `aspire-output`
8) Publish Solace.Db.Migrator, copy outputs to aspire-output/migrator

### Running published containers

1) Run the setup script `dotnet setup.cs`
2) Run either `.\up.ps1` or `./up.sh` depending on your OS
