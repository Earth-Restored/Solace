using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Solace.DB.Models;

namespace Solace.DB;

public sealed class ResultsEF
{
    [DisallowNull]
    public int? Profile { get => field; set => field = field is null || value > field ? value : field; }

    [DisallowNull]
    public int? Inventory { get => field; set => field = field is null || value > field ? value : field; }

    [DisallowNull]
    public int? Crafting { get => field; set => field = field is null || value > field ? value : field; }

    [DisallowNull]
    public int? Smelting { get => field; set => field = field is null || value > field ? value : field; }

    [DisallowNull]
    public int? Boosts { get => field; set => field = field is null || value > field ? value : field; }

    [DisallowNull]
    public int? Buildplates { get => field; set => field = field is null || value > field ? value : field; }

    [DisallowNull]
    public int? Journal { get => field; set => field = field is null || value > field ? value : field; }

    [DisallowNull]
    public int? Challenges { get => field; set => field = field is null || value > field ? value : field; }

    [DisallowNull]
    public int? Tokens { get => field; set => field = field is null || value > field ? value : field; }

    public sealed class Builder
    {
        private bool _profile;
        private bool _inventory;
        private bool _crafting;
        private bool _smelting;
        private bool _boosts;
        private bool _buildplates;
        private bool _journal;
        private bool _challenges;
        private bool _tokens;

        public static Builder Null { get; } = new Builder();

        public Builder Profile(bool updated = true)
        {
            _profile |= updated;
            return this;
        }

        public Builder Inventory(bool updated = true)
        {
            _inventory |= updated;
            return this;
        }

        public Builder Crafting(bool updated = true)
        {
            _crafting |= updated;
            return this;
        }

        public Builder Smelting(bool updated = true)
        {
            _smelting |= updated;
            return this;
        }

        public Builder Boosts(bool updated = true)
        {
            _boosts |= updated;
            return this;
        }

        public Builder Buildplates(bool updated = true)
        {
            _buildplates |= updated;
            return this;
        }

        public Builder Journal(bool updated = true)
        {
            _journal |= updated;
            return this;
        }

        public Builder Challenges(bool updated = true)
        {
            _challenges |= updated;
            return this;
        }

        public Builder Tokens(bool updated = true)
        {
            _tokens |= updated;
            return this;
        }

        public async Task<ResultsEF> BuildAsync(EarthDbContext earthDb, Guid accountId, CancellationToken cancellationToken = default)
        {
            // todo: bug - needed for compiled queries - https://github.com/dotnet/efcore/issues/35887
            var earthDbL = earthDb;
            var accountIdL = accountId;
            var cancellationTokenL = cancellationToken;

            var versions = await earthDbL.AccountVersions
                .AsNoTracking()
                .FirstAsync(versions => versions.Id == accountIdL, cancellationTokenL);

            return Build(versions);
        }

        public ResultsEF Build(AccountVersions versions)
            => new ResultsEF
            {
                Profile = _profile ? versions.Profile : null,
                Inventory = _inventory ? versions.Inventory : null,
                Crafting = _crafting ? versions.Crafting : null,
                Smelting = _smelting ? versions.Smelting : null,
                Boosts = _boosts ? versions.Boosts : null,
                Buildplates = _buildplates ? versions.Buildplates : null,
                Journal = _journal ? versions.Journal : null,
                Challenges = _challenges ? versions.Challenges : null,
                Tokens = _tokens ? versions.Tokens : null,
            };
    }
}
