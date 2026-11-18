using CargoPlan.Core.ShipData;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.ShipData;

/// <summary>What the ship data check came to (T12.3, D112).</summary>
public abstract record ShipDataVerdict;

/// <summary>A newer data release the app reads and the player hasn't skipped: the line with Update and Skip.</summary>
public sealed record ShipDataOffer(ShipDataVersion Version) : ShipDataVerdict;

/// <summary>A newer data release of a ship JSON schema this app doesn't read: a plain notice, no download (P6).</summary>
public sealed record ShipDataNeedsApp(ShipDataVersion Version, int SchemaVersion) : ShipDataVerdict;

/// <summary>Nothing to offer; <paramref name="Reason"/> is the check's log line.</summary>
/// <param name="Skipped">The newer release the player skipped, for Check now's status; null for every other reason.</param>
public sealed record NoShipDataUpdate(string Reason, ShipDataVersion? Skipped = null) : ShipDataVerdict;

/// <summary>The ship data check's decisions without the network, like <see cref="Updates.UpdateGate"/>.</summary>
public static class ShipDataGate
{
    /// <summary>
    /// Off in settings.json: no request. A developer's build asks only with <c>--ship-data-source</c> (P4): data releases
    /// don't depend on the app's version, but a <c>dotnet run</c> shouldn't ask GitHub and download into the profile.
    /// </summary>
    public static bool ShouldCheck(bool checkOnStartup, bool isDevBuild, bool hasSource) => checkOnStartup && (!isDevBuild || hasSource);

    /// <param name="active">The data in use (<see cref="ShipDataStore.Active"/>), never older than the baseline.</param>
    /// <param name="found">The latest release's tag.</param>
    /// <param name="schemaVersion">Its ship JSON schema.</param>
    /// <param name="skipped"><c>shipData.skippedVersion</c>: exactly this release is not offered, a newer one is.</param>
    public static ShipDataVerdict Offer(ShipDataVersion active, ShipDataVersion found, int schemaVersion, string? skipped)
    {
        if (found <= active)
        {
            return new NoShipDataUpdate($"nothing newer than {active}");
        }

        // Before the skip: that line has no Skip, and the app update that reads it is what helps.
        if (!ShipLoader.IsReadable(schemaVersion))
        {
            return new ShipDataNeedsApp(found, schemaVersion);
        }

        if (ShipDataVersion.TryParse(skipped, out var skip) && skip == found)
        {
            return new NoShipDataUpdate($"{found} skipped (shipData.skippedVersion)", found);
        }

        return new ShipDataOffer(found);
    }

    /// <summary>The notice for <see cref="ShipDataNeedsApp"/>, and Check now's status for it.</summary>
    public static string NeedsAppText(ShipDataVersion version) => $"Ship data {version.Display} needs an app update";
}
