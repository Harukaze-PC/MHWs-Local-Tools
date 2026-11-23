using System;
using System.IO;
using System.Reflection;

internal static class ReadOnlyAudit
{
    const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    static void CheckAbsent(bool absent, string name)
    {
        if (!absent) throw new InvalidDataException("Unexpected item in application: " + name);
    }

    static void CheckMembers(Assembly app, string typeName, params string[] names)
    {
        var type = app.GetType(typeName, true);
        foreach (string name in names)
            CheckAbsent(type.GetMember(name, Members).Length == 0, typeName + "." + name);
    }

    static int Main(string[] args)
    {
        try {
            if (args.Length != 1) throw new ArgumentException("Usage: ReadOnlyAudit.exe <application.exe>");
            var app = Assembly.LoadFile(Path.GetFullPath(args[0]));
            foreach (string name in new[] {
                "PrivateCharmFavorites", "CityHash64", "FavoriteBatchAudit",
                "LoadoutSolver", "PlannerInventory", "PlannerCatalog", "GogForgeGuide"
            })
                CheckAbsent(app.GetType(name) == null, name);

            CheckMembers(app, "OfflineReader", "WithFavoriteCharms");
            CheckMembers(app, "CharmPreview", "AddFavoriteControls");
            CheckMembers(app, "Inventory", "SourceSavePath", "SourceSaveDigest", "planner");
            CheckMembers(app, "OfflineTalisman", "FavoriteOffset");

            var resources = app.GetManifestResourceNames();
            foreach (string name in new[] {
                "plannerCatalog", "weaponPlaybooks", "encounterPlaybooks",
                "forgePlaybooks", "plannerThumbnails", "plannerThumbnailIndex"
            })
                CheckAbsent(Array.IndexOf(resources, name) < 0, name);

            Console.WriteLine("PASS: application excludes save-writing features and the retired loadout implementation");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
