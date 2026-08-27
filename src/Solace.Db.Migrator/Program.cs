using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Solace.Db.Earth;
using Solace.Db.Earth.Models;
using Solace.Db.Earth.Models.Common;
using Solace.Db.Earth.Models.Global;
using Solace.Db.Earth.Models.Player;
using Solace.Db.Earth.Models.Player.Workshop;
using Solace.Db.Earth.Utils;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Data;
using Solace.WebPortal.Utils;
using Spectre.Console;

namespace Solace.Db.Migrator;

internal static class Program
{
    private static readonly Dictionary<Guid, long> _guidToLong = [];

    private static async Task Main()
    {
        var migratorVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        var supportedVersion = typeof(Solace.Db.Earth.EarthDbContext).Assembly.GetName().Version!;

        AnsiConsole.Write(
            new Rule($"[bold blue]Solace Database Migrator[/] v{migratorVersion.ToString(3)}")
                .RuleStyle("grey")
                .LeftJustified());
        AnsiConsole.WriteLine();

        AnsiConsole.Write(
            new Panel(
                new Markup(
                    $"""
                    1. Stop any running instance of the old Solace version ([bold cyan]v0.X.X[/]).
                    2. Run the new Solace version ([bold cyan]v{supportedVersion.ToString(3)}[/]) normally at least once to initialize the database schema.
                    3. 
                        a) If using Docker, start only [bold cyan]postgres[/] and [bold cyan]object-store[/].
                        b) If using AppHost, run [bold white on gray]dotnet run -- --migration-mode=true[/].
                    """
                )
            )
            {
                Header = new PanelHeader("[bold yellow] Prerequisites [/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow),
                Padding = new Padding(1, 0, 1, 0)
            });
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        Console.ReadKey(intercept: true);

        AnsiConsole.WriteLine();

        var oldPath = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]Enter path to old installation (folder with components, data, launcher, staticdata):[/]")
                .Validate(path => Directory.Exists(path)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Directory does not exist![/]")));

        var postgresHost = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]PostgreSQL Host:[/]")
                .DefaultValue("localhost"));

        var postgresPort = AnsiConsole.Prompt(
            new TextPrompt<int>("[yellow]PostgreSQL Port:[/]"));

        var postgresUser = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]PostgreSQL User:[/]")
                .DefaultValue("postgres"));

        var postgresPassword = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]PostgreSQL Password:[/]")
                .Secret());

        var objectStoreEndpoint = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]Object Store Endpoint (http://localhost:XXXX/):[/]"));

        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("[bold green]Start migration with these settings?[/]", defaultValue: true))
        {
            AnsiConsole.MarkupLine("[bold red]Migration canceled by user.[/]");
            return;
        }

        AnsiConsole.WriteLine();

        try
        {
            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(
                [
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn(),
                ])
                .StartAsync(async ctx =>
                {
                    var connectTask = ctx.AddTask("[bold cyan]Connecting to databases & services[/]", maxValue: 100);
                    connectTask.IsIndeterminate = true;

                    await using var earthOld = Old.Earth.EarthDbContext.CreateFromPath(Path.Combine(oldPath, "data", "earth.db"));
                    await using var webOld = Old.Web.ApplicationDbContext.CreateFromPath(Path.Combine(oldPath, "launcher", "Data", "app.db"));

                    await using var earthNew = EarthDbContext.CreateFromConnection($"Host={postgresHost};Port={postgresPort};Username={postgresUser};Password={postgresPassword};Database=EarthDb");
                    await using var webNew = ApplicationDbContext.CreateFromConnection($"Host={postgresHost};Port={postgresPort};Username={postgresUser};Password={postgresPassword};Database=WebPortalDb");

                    await using var objectStore = await ObjectStoreClient.ConnectAsync(objectStoreEndpoint, NullLogger.Instance);

                    await using var earthTransaction = await earthNew.Database.BeginTransactionAsync();
                    await using var webTransaction = await webNew.Database.BeginTransactionAsync();

                    connectTask.Value = 100;
                    connectTask.Description = "[bold green]Connected to databases & services[/]";

                    var webTask = ctx.AddTask("[bold cyan]Migrating Web Database[/]", maxValue: 100);
                    webTask.IsIndeterminate = true;
                    await MigrateWeb(webOld, webNew);
                    webTask.Value = 100;
                    webTask.Description = "[bold green]Migrated Web Database[/]";

                    var earthTask = ctx.AddTask("[bold cyan]Migrating Earth Database[/]", maxValue: 100);
                    earthTask.IsIndeterminate = true;
                    await MigrateEarth(earthOld, earthNew, webOld);
                    earthTask.Value = 100;
                    earthTask.Description = "[bold green]Migrated Earth Database[/]";

                    var objectStorePath = Path.Combine(oldPath, "data", "object_store");
                    var files = Directory.Exists(objectStorePath)
                        ? Directory.GetFiles(objectStorePath, "*", SearchOption.AllDirectories)
                        : [];

                    var storeTask = ctx.AddTask("[bold cyan]Migrating Object Store Files[/]", maxValue: Math.Max(1, files.Length));

                    if (files.Length == 0)
                    {
                        storeTask.Value = 1;
                        storeTask.Description = "[yellow]No object store files found to migrate[/]";
                    }
                    else
                    {
                        foreach (var file in files)
                        {
                            if (Guid.TryParse(Path.GetFileNameWithoutExtension(file.AsSpan()), out var id))
                            {
                                await using var fileStream = File.OpenRead(file);
                                await objectStore.UpdateAsync(id, fileStream);
                            }

                            storeTask.Increment(1);
                        }

                        storeTask.Description = "[bold green]Migrated Object Store Files[/]";
                    }

                    var commitTask = ctx.AddTask("[bold cyan]Committing Database Transactions[/]", maxValue: 100);
                    commitTask.IsIndeterminate = true;

                    await earthTransaction.CommitAsync();
                    await webTransaction.CommitAsync();

                    commitTask.Value = 100;
                    commitTask.Description = "[bold green]Committed Database Transactions[/]";
                });

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(new Markup("""
                [bold green]Migration completed successfully![/]
                [bold gray]Note: user roles need to be migrated manually[/]
                """))
            {
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 1, 1, 1),
            });
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold red]Migration Failed[/]").RuleStyle("red"));
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything | ExceptionFormats.ShowLinks);
        }
    }

    private static async Task MigrateEarth(Old.Earth.EarthDbContext oldDb, EarthDbContext newDb, Old.Web.ApplicationDbContext webOld, CancellationToken cancellationToken = default)
    {
        var existingTemplateIds = await newDb.TemplateBuildplates.Select(x => x.Id).ToHashSetAsync(cancellationToken);
        var templateIds = new HashSet<Guid>(existingTemplateIds);

        await foreach (var template in oldDb.TemplateBuildplates
            .AsNoTracking()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            templateIds.Add(template.Id);

            if (existingTemplateIds.Contains(template.Id))
            {
                continue;
            }

            newDb.TemplateBuildplates.Add(new TemplateBuildplateEF()
            {
                Id = template.Id,
                Name = template.Name,
                Size = template.Size,
                Offset = template.Offset,
                BlocksPerMeter = template.Scale,
                Night = template.Night,
                ServerDataObjectId = Guid.Parse(template.ServerDataObjectId),
                PreviewObjectId = Guid.Parse(template.PreviewObjectId),
            });
            existingTemplateIds.Add(template.Id);
        }

        await newDb.SaveChangesAsync(cancellationToken);

        var existingTileIds = await newDb.Tiles.Select(x => x.Id).ToHashSetAsync(cancellationToken);
        await foreach (var tile in oldDb.Tiles
            .AsNoTracking()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            var tileId = unchecked((long)tile.Id);
            if (existingTileIds.Contains(tileId))
            {
                continue;
            }

            newDb.Tiles.Add(new Tile()
            {
                Id = tileId,
                ObjectStoreId = Guid.Parse(tile.ObjectStoreId),
            });
            existingTileIds.Add(tileId);
        }

        var existingSecretIds = await newDb.Secrets.Select(x => x.Id).ToHashSetAsync(StringComparer.Ordinal, cancellationToken);
        await foreach (var secret in oldDb.Secrets
            .AsNoTracking()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            if (existingSecretIds.Contains(secret.Id))
            {
                continue;
            }

            newDb.Secrets.Add(new Secret()
            {
                Id = secret.Id,
                Value = secret.Value,
            });
            existingSecretIds.Add(secret.Id);
        }

        await newDb.SaveChangesAsync(cancellationToken);

        var existingProfileIds = await newDb.Profiles.Select(x => x.Id).ToHashSetAsync(cancellationToken);
        var existingPlayerBuildplateIds = await newDb.PlayerBuildplates.Select(x => x.Id).ToHashSetAsync(cancellationToken);
        var existingStackableKeys = await newDb.StackableItems.AsAsyncEnumerable().Select(x => (x.ProfileId, x.ItemId)).ToHashSetAsync(cancellationToken: cancellationToken);
        var existingNonStackableIds = await newDb.NonStackableItems.Select(x => x.InstanceId).ToHashSetAsync(cancellationToken);
        var existingJournalKeys = await newDb.JournalEntries.AsAsyncEnumerable().Select(x => (x.ProfileId, x.ItemId)).ToHashSetAsync(cancellationToken: cancellationToken);
        var existingTappableKeys = await newDb.RedeemedTappables.AsAsyncEnumerable().Select(x => (x.ProfileId, x.TappableId)).ToHashSetAsync(cancellationToken: cancellationToken);
        var existingTokenIds = await newDb.Tokens.Select(x => x.TokenId).ToHashSetAsync(cancellationToken);
        var existingSharedBuildplateIds = await newDb.SharedBuildplates.Select(x => x.Id).ToHashSetAsync(cancellationToken);

        await foreach (var account in oldDb.Accounts
            .AsNoTracking()
            .Include(account => account.Profile)
            .Include(account => account.Boosts)
            .Include(account => account.CraftingSlots)
            .Include(account => account.SmeltingSlots)
            .Include(account => account.Hotbar)
            .Include(account => account.ActivityLog)
            .Include(account => account.Buildplates)
            .Include(account => account.Inventory)
            .Include(account => account.Journal)
            .Include(account => account.RedeemedTappables)
            .Include(account => account.Tokens)
            .Include(account => account.SharedBuildplates)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            Debug.Assert(account.Profile is not null);
            Debug.Assert(account.Boosts is not null);
            Debug.Assert(account.Hotbar is not null);
            Debug.Assert(account.CraftingSlots is not null);
            Debug.Assert(account.SmeltingSlots is not null);
            Debug.Assert(account.ActivityLog is not null);
            Debug.Assert(account.Buildplates is not null);
            Debug.Assert(account.Inventory is not null);
            Debug.Assert(account.Journal is not null);
            Debug.Assert(account.RedeemedTappables is not null);
            Debug.Assert(account.Tokens is not null);
            Debug.Assert(account.SharedBuildplates is not null);

            var profileIsNew = !existingProfileIds.Contains(account.Id);

            if (profileIsNew)
            {
                var owner = await webOld.Users
                    .AsNoTracking()
                    .Where(user => user.LinkedInGameAccounts.Contains(account.Id))
                    .Select(user => user.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                var profile = new Earth.Models.ProfileEF()
                {
                    Id = account.Id,
                    WebPortalAccountId = owner is null ? null : MapGuidToLong(Guid.Parse(owner)),
                    CreatedDate = DateTimeOffset.FromUnixTimeSeconds(account.CreatedDate),
                    Username = account.Username ?? "new profile",
                    ProfilePictureUrl = account.ProfilePictureUrl is ProfileEF.DefaultPictureUrl ? null : account.ProfilePictureUrl,
                    Health = account.Profile.Health,
                    Experience = account.Profile.Experience,
                    Level = account.Profile.Level,
                    Rubies = new Rubies(account.Profile.Rubies.Purchased, account.Profile.Rubies.Earned),
                    Boosts = new BoostsEF()
                    {
                        Id = account.Id,
                    },
                    Hotbar = new HotbarEF()
                    {
                        Id = account.Id,
                    },
                    CraftingSlots = new CraftingSlotsEF()
                    {
                        Id = account.Id,
                    },
                    SmeltingSlots = new SmeltingSlotsEF()
                    {
                        Id = account.Id,
                    },
                };

                for (var i = 0; i < account.Boosts.ActiveBoosts.Length; i++)
                {
                    var boost = account.Boosts.ActiveBoosts[i];

                    if (boost is null)
                    {
                        continue;
                    }

                    profile.Boosts.ActiveBoosts[i] = new BoostsEF.ActiveBoost(Guid.Parse(boost.InstanceId), Guid.Parse(boost.ItemId), DateTimeOffset.FromUnixTimeMilliseconds(boost.StartTime), TimeSpan.FromMilliseconds(boost.Duration));
                }

                for (var i = 0; i < account.Hotbar.Items.Length; i++)
                {
                    var item = account.Hotbar.Items[i];

                    if (item is null)
                    {
                        continue;
                    }

                    profile.Hotbar.Items[i] = new HotbarEF.Item(Guid.Parse(item.Uuid), item.Count, item.InstanceId is { } instanceId ? Guid.Parse(instanceId) : null);
                }

                for (var i = 0; i < account.CraftingSlots.Slots.Length; i++)
                {
                    var slot = account.CraftingSlots.Slots[i];

                    profile.CraftingSlots.Slots[i] = new CraftingSlotEF()
                    {
                        ActiveJob = slot.ActiveJob is { } activeJob
                            ? new CraftingSlotEF.ActiveCraftingJob(
                                activeJob.SessionId,
                                Guid.Parse(activeJob.RecipeId),
                                DateTimeOffset.FromUnixTimeMilliseconds(activeJob.StartTime),
                                [
                                    .. activeJob.Input.Select(input => new CraftingSlotEF.InputRow(
                                        [
                                            .. input.Items.Select(item => new InputItem(
                                                Guid.Parse(item.Id),
                                                item.Count,
                                                [
                                                    .. item.Instances.Select(instance => new NonStackableItemInstance(
                                                        instance.InstanceId,
                                                        instance.Wear
                                                    )),
                                                ]
                                            )),
                                        ]
                                    )),
                                ],
                                activeJob.TotalRounds,
                                activeJob.CollectedRounds,
                                activeJob.FinishedEarly
                            )
                            : null,
                        Locked = slot.Locked,
                    };
                }

                for (var i = 0; i < account.SmeltingSlots.Slots.Length; i++)
                {
                    var slot = account.SmeltingSlots.Slots[i];

                    profile.SmeltingSlots.Slots[i] = new SmeltingSlotEF()
                    {
                        ActiveJob = slot.ActiveJob is { } activeJob
                            ? new SmeltingSlotEF.ActiveSmeltingJob(
                                activeJob.SessionId,
                                Guid.Parse(activeJob.RecipeId),
                                DateTimeOffset.FromUnixTimeMilliseconds(activeJob.StartTime),
                                new InputItem(
                                    Guid.Parse(activeJob.Input.Id),
                                    activeJob.Input.Count,
                                    [
                                        .. activeJob.Input.Instances.Select(instance => new NonStackableItemInstance(
                                            instance.InstanceId,
                                            instance.Wear
                                        )),
                                    ]
                                ),
                                activeJob.AddedFuel is { } addedFuel
                                    ? new SmeltingSlotEF.Fuel(
                                        new InputItem(
                                            Guid.Parse(addedFuel.Item.Id),
                                            addedFuel.Item.Count,
                                            [
                                                .. addedFuel.Item.Instances.Select(instance => new NonStackableItemInstance(
                                                    instance.InstanceId,
                                                    instance.Wear
                                                )),
                                            ]
                                        ),
                                        TimeSpan.FromSeconds(addedFuel.BurnDuration),
                                        addedFuel.HeatPerSecond
                                    )
                                    : null,
                                activeJob.TotalRounds,
                                activeJob.CollectedRounds,
                                activeJob.FinishedEarly
                            )
                            : null,
                        Locked = slot.Locked,
                    };
                }

                newDb.Profiles.Add(profile);
                await newDb.SaveChangesAsync(cancellationToken);
                existingProfileIds.Add(account.Id);

                foreach (var activityLog in account.ActivityLog.Entries)
                {
                    newDb.ActivityLogs.Add(activityLog switch
                    {
                        Old.Earth.Models.Player.ActivityLogEF.LevelUpEntry levelUp => new LevelUpEntryEF(account.Id, DateTimeOffset.FromUnixTimeMilliseconds(levelUp.Timestamp), levelUp.Level),
                        Old.Earth.Models.Player.ActivityLogEF.TappableEntry tappable => new TappableEntryEF(account.Id, DateTimeOffset.FromUnixTimeMilliseconds(tappable.Timestamp), MapRewards(tappable.Rewards)),
                        Old.Earth.Models.Player.ActivityLogEF.JournalItemUnlockedEntry journalUnlocked => new JournalItemUnlockedEntryEF(account.Id, DateTimeOffset.FromUnixTimeMilliseconds(journalUnlocked.Timestamp), Guid.Parse(journalUnlocked.ItemId)),
                        Old.Earth.Models.Player.ActivityLogEF.CraftingCompletedEntry craftingCompleted => new CraftingCompletedEntryEF(account.Id, DateTimeOffset.FromUnixTimeMilliseconds(craftingCompleted.Timestamp), MapRewards(craftingCompleted.Rewards)),
                        Old.Earth.Models.Player.ActivityLogEF.SmeltingCompletedEntry smeltingCompleted => new SmeltingCompletedEntryEF(account.Id, DateTimeOffset.FromUnixTimeMilliseconds(smeltingCompleted.Timestamp), MapRewards(smeltingCompleted.Rewards)),
                        Old.Earth.Models.Player.ActivityLogEF.BoostActivatedEntry boostActivated => new BoostActivatedEntryEF(account.Id, DateTimeOffset.FromUnixTimeMilliseconds(boostActivated.Timestamp), Guid.Parse(boostActivated.ItemId)),
                        _ => throw new UnreachableException(),
                    });
                }

                await newDb.SaveChangesAsync(cancellationToken);
            }

            foreach (var buildplate in account.Buildplates)
            {
                if (existingPlayerBuildplateIds.Contains(buildplate.Id))
                {
                    continue;
                }

                newDb.PlayerBuildplates.Add(new PlayerBuildplateEF()
                {
                    Id = buildplate.Id,
                    ProfileId = account.Id,
                    TemplateId = buildplate.TemplateId is not null && templateIds.Contains(buildplate.TemplateId.Value)
                        ? buildplate.TemplateId
                        : null,
                    Name = buildplate.Name,
                    Size = buildplate.Size,
                    Offset = buildplate.Offset,
                    BlocksPerMeter = buildplate.Scale,
                    Night = buildplate.Night,
                    LastModified = DateTimeOffset.FromUnixTimeMilliseconds(buildplate.LastModified),
                    ServerDataObjectId = Guid.Parse(buildplate.ServerDataObjectId),
                    PreviewObjectId = Guid.Parse(buildplate.PreviewObjectId),
                });
                existingPlayerBuildplateIds.Add(buildplate.Id);
            }

            await newDb.SaveChangesAsync(cancellationToken);

            foreach (var item in account.Inventory.StackableItemsData)
            {
                var itemId = Guid.Parse(item.Key);
                var key = (account.Id, itemId);
                if (existingStackableKeys.Contains(key))
                {
                    continue;
                }

                newDb.StackableItems.Add(new StackableItemEF(account.Id, itemId, item.Value));
                existingStackableKeys.Add(key);
            }

            await newDb.SaveChangesAsync(cancellationToken);

            foreach (var item in account.Inventory.NonStackableItemsData)
            {
                foreach (var instance in item.Value.Values)
                {
                    var instanceId = Guid.Parse(instance.InstanceId);
                    if (existingNonStackableIds.Contains(instanceId))
                    {
                        continue;
                    }

                    newDb.NonStackableItems.Add(new NonStackableItemInstanceEF(account.Id, Guid.Parse(item.Key), instanceId, instance.Wear));
                    existingNonStackableIds.Add(instanceId);
                }
            }

            await newDb.SaveChangesAsync(cancellationToken);

            foreach (var journalEntry in account.Journal.Items)
            {
                var itemId = Guid.Parse(journalEntry.Key);
                var key = (account.Id, itemId);
                if (existingJournalKeys.Contains(key))
                {
                    continue;
                }

                newDb.JournalEntries.Add(new ItemJournalEntryEF()
                {
                    ProfileId = account.Id,
                    ItemId = itemId,
                    FirstSeen = DateTimeOffset.FromUnixTimeMilliseconds(journalEntry.Value.FirstSeen),
                    LastSeen = DateTimeOffset.FromUnixTimeMilliseconds(journalEntry.Value.LastSeen),
                    AmountCollected = journalEntry.Value.AmountCollected,
                });
                existingJournalKeys.Add(key);
            }

            await newDb.SaveChangesAsync(cancellationToken);

            foreach (var tappable in account.RedeemedTappables.Tappables)
            {
                var key = (account.Id, tappable.Key);
                if (existingTappableKeys.Contains(key))
                {
                    continue;
                }

                newDb.RedeemedTappables.Add(new RedeemedTappableEF()
                {
                    ProfileId = account.Id,
                    TappableId = tappable.Key,
                    ExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(tappable.Value),
                });
                existingTappableKeys.Add(key);
            }

            await newDb.SaveChangesAsync(cancellationToken);

            foreach (var (tokenIdStr, token) in account.Tokens.Tokens)
            {
                if (token is not (Old.Earth.Models.Player.TokensEF.LevelUpToken or Old.Earth.Models.Player.TokensEF.JournalItemUnlockedToken))
                {
                    continue;
                }

                var tokenId = Guid.Parse(tokenIdStr);
                if (existingTokenIds.Contains(tokenId))
                {
                    continue;
                }

                newDb.Tokens.Add(token switch
                {
                    Old.Earth.Models.Player.TokensEF.LevelUpToken levelUp => new LevelUpTokenEF(account.Id, levelUp.Level, MapRewards(levelUp.Rewards)) { TokenId = tokenId, },
                    Old.Earth.Models.Player.TokensEF.JournalItemUnlockedToken itemUnlocked => new JournalItemUnlockedTokenEF(account.Id, Guid.Parse(itemUnlocked.ItemId)) { TokenId = tokenId, },
                    _ => throw new UnreachableException(),
                });
                existingTokenIds.Add(tokenId);
            }

            await newDb.SaveChangesAsync(cancellationToken);

            foreach (var buildplate in account.SharedBuildplates)
            {
                if (existingSharedBuildplateIds.Contains(buildplate.Id))
                {
                    continue;
                }

                newDb.SharedBuildplates.Add(new SharedBuildplateEF()
                {
                    Id = buildplate.Id,
                    ProfileId = account.Id,
                    Size = buildplate.Size,
                    Offset = buildplate.Offset,
                    BlocksPerMeter = buildplate.Scale,
                    Night = buildplate.Night,
                    Created = DateTimeOffset.FromUnixTimeMilliseconds(buildplate.Created),
                    BuildplateLastModifed = DateTimeOffset.FromUnixTimeMilliseconds(buildplate.BuildplateLastModifed),
                    LastViewed = DateTimeOffset.FromUnixTimeMilliseconds(buildplate.LastViewed),
                    NumberOfTimesViewed = buildplate.NumberOfTimesViewed,
                    Hotbar = [.. buildplate.Hotbar.Select(item => item is null
                        ? null
                        : new SharedBuildplateEF.HotbarItem(
                            Guid.Parse(item.Uuid),
                            item.Count,
                            item.InstanceId is null ? null : Guid.Parse(item.InstanceId),
                            item.Wear
                        )
                    )],
                    ServerDataObjectId = Guid.Parse(buildplate.ServerDataObjectId),
                });
                existingSharedBuildplateIds.Add(buildplate.Id);
            }

            await newDb.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task MigrateWeb(Old.Web.ApplicationDbContext oldDb, ApplicationDbContext newDb, CancellationToken cancellationToken = default)
    {
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning disable CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
        var existingUsers = await newDb.Users
            .AsNoTracking()
            .ToDictionaryAsync(user => user.NormalizedUserName ?? user.UserName, StringComparer.Ordinal, cancellationToken);
#pragma warning restore CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
#pragma warning restore CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
        var existingUserIds = existingUsers.Values.Select(u => u.Id).ToHashSet();

        await foreach (var oldUser in oldDb.Users
            .AsNoTracking()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            var guid = Guid.Parse(oldUser.Id);
            var userKey = oldUser.NormalizedUserName ?? oldUser.UserName;

            if (existingUsers.TryGetValue(userKey, out var existingUser))
            {
                _guidToLong[guid] = existingUser.Id;
                continue;
            }

            var userId = MapGuidToLong(guid);
            if (existingUserIds.Contains(userId))
            {
                continue;
            }

            newDb.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = oldUser.UserName,
                NormalizedUserName = oldUser.NormalizedUserName,
                Email = oldUser.Email,
                NormalizedEmail = oldUser.NormalizedEmail,
                EmailConfirmed = oldUser.EmailConfirmed,
                PasswordHash = oldUser.PasswordHash,
                SecurityStamp = oldUser.SecurityStamp,
                ConcurrencyStamp = oldUser.ConcurrencyStamp,
                PhoneNumber = oldUser.PhoneNumber,
                PhoneNumberConfirmed = oldUser.PhoneNumberConfirmed,
                TwoFactorEnabled = oldUser.TwoFactorEnabled,
                LockoutEnd = oldUser.LockoutEnd,
                LockoutEnabled = oldUser.LockoutEnabled,
                AccessFailedCount = oldUser.AccessFailedCount
            });
            existingUserIds.Add(userId);
        }

        await newDb.SaveChangesAsync(cancellationToken);

        await foreach (var passkey in oldDb.UserPasskeys
            .AsNoTracking()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            try
            {
                newDb.UserPasskeys.Add(new IdentityUserPasskey<long>()
                {
                    CredentialId = passkey.CredentialId,
                    UserId = MapGuidToLong(Guid.Parse(passkey.UserId)),
                    Data = passkey.Data,
                });

                await newDb.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation)
            {
            }
        }

        await ResetSequenceAsync(newDb, "AspNetUsers", cancellationToken: cancellationToken);
    }

    private static Rewards MapRewards(Old.Earth.Models.Common.Rewards rewards)
        => new(rewards.Rubies, rewards.ExperiencePoints, rewards.Level, rewards.Items.ToDictionary(item => Guid.Parse(item.Key), item => item.Value ?? 0), [.. rewards.Buildplates.Select(Guid.Parse)], [.. rewards.Challenges.Select(Guid.Parse)]);

    private static long MapGuidToLong(Guid id)
    {
        if (!_guidToLong.TryGetValue(id, out var longId))
        {
            longId = LongIdGenerator.NextId();

            _guidToLong.Add(id, longId);
        }

        return longId;
    }

    private static async Task ResetSequenceAsync(DbContext dbContext, string tableName, string columnName = "Id", CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT setval(pg_get_serial_sequence('\"{tableName}\"', '{columnName}'), COALESCE(MAX(\"{columnName}\"), 1)) FROM \"{tableName}\";";
        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}