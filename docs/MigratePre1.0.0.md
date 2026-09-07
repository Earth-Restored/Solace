# Migrate Data from a 0.X.X Version

1) Make sure your old installation is at least on version 0.0.7.
2) Stop the old version if it is currently running.
3) Initialize the database (skip if you have already ran the new version):
    * Run `dotnet setup.cs`
    * Run `up.ps1` (Windows) or `up.sh` (Linux/Mac).
    * Run `docker compose logs -f` and wait until new logs appear.
    * Press `Ctrl+C` to exit the log view.
    * Run `down.ps1` or `down.sh`.
4) Run `migrate-data.ps1` or `migrate-data.sh`, then enter the path to your old installation when prompted.
5) Manually recreate and reassign any custom roles you created previously.
