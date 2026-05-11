using System.ComponentModel.DataAnnotations;

namespace Solace.DB.Models.Player;

public sealed class ProfileEF : IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public int Health { get; set; } = 20;

    public int Experience { get; set; }

    public int Level { get; set; } = 1;

    public Rubies Rubies { get; set; } = new Rubies();

    public sealed class Legacy : IEquatable<Legacy>
    {
        public int Health { get; set; }
        public int Experience { get; set; }
        public int Level { get; set; }
        public Rubies.Legacy Rubies { get; set; }

        public Legacy()
        {
            Health = 20;
            Experience = 0;
            Level = 1;
            Rubies = new Rubies.Legacy();
        }

        public bool Equals(Legacy? other)
            => other is not null && Health == other.Health && Experience == other.Experience && Level == other.Level && Rubies.Equals(other.Rubies);

        public override bool Equals(object? obj)
            => Equals(obj as Legacy);

        public override int GetHashCode()
            => HashCode.Combine(Health, Experience, Level, Rubies);
    }
}