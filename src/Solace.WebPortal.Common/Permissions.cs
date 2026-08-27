using System.Collections.Immutable;
using System.Reflection;

namespace Solace.WebPortal.Common;

public static class Permissions
{
    // DO NOT CHANGE VALUES, ok to rename and chage PermissionInfo
    [PermissionInfo("Profile", "Create in-game profile")]
    public const string CreateProfile = "profile.create";

    [PermissionInfo("Users", "Manage roles - add, edit, delete")]
    public const string EditRoles = "user.role.edit";
    [PermissionInfo("Users", "View all users accounts")]
    public const string ViewUsers = "user.view";
    [PermissionInfo("Users", "Assign and remove roles to/from users")]
    public const string AssignRoles = "user.role.assign";
    [PermissionInfo("Users", "Delete user accounts")]
    public const string DeleteUsers = "user.delete";
    [PermissionInfo("Users", "Edit user account info")]
    public const string EditAcountInfo = "user.edit";

    [PermissionInfo("Players", "View all player accounts")]
    public const string ViewPlayers = "player.view";
    [PermissionInfo("Players", "Manage player accounts - edit, delete")]
    public const string ManagePlayers = "player.manage";

    [PermissionInfo("Buildplate Templates", "View the imported buildplate templates")]
    public const string ViewBuildplates = "buildplate.view";
    [PermissionInfo("Buildplate Templates", "Manage buildplate templates - import, edit, delete")]
    public const string ManageBuildplates = "buildplate.manage";

    [PermissionInfo("Data", "View server data - space usage")]
    public const string ViewData = "data.view";
    [PermissionInfo("Data", "Export server data")]
    public const string ExportData = "data.export";
    [PermissionInfo("Data", "Upload and delete all server data")]
    public const string EditData = "data.edit";

    [PermissionInfo("Store", "View store layout and items")]
    public const string ViewStore = "store.view";

    [PermissionInfo("Store", "Edit store layout and items")]
    public const string EditStore = "store.edit";

    public static ImmutableArray<string> All { get; }

    public static ImmutableArray<PermissionDescriptor> AllWithInfo { get; }

    static Permissions()
    {
        var fields = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false, } && f.FieldType == typeof(string));

        All = [.. fields.Select(f => (string)f.GetRawConstantValue()!)];

        AllWithInfo = [.. fields.Select(f =>
        {
            var attr = f.GetCustomAttribute<PermissionInfoAttribute>();

            return new PermissionDescriptor(
                (string)f.GetRawConstantValue()!,
                attr?.Category ?? "Other",
                attr?.Description ?? ""
            );
        })];
    }

    public static bool Exists(string name)
        => All.Contains(name, StringComparer.Ordinal);

    public readonly record struct PermissionDescriptor(
        string Name,
        string Category,
        string Description
    );

    [AttributeUsage(AttributeTargets.Field)]
    private sealed class PermissionInfoAttribute : Attribute
    {
        public PermissionInfoAttribute(string category, string description)
        {
            Category = category;
            Description = description;
        }

        public string Category { get; }

        public string Description { get; }
    }
}
