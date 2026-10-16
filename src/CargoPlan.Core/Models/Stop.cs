namespace CargoPlan.Core.Models;

/// <summary>A destination on the route. Lower <see cref="Order"/> is visited first.</summary>
public sealed record Stop(string Destination, int Order, IReadOnlyList<CargoItem> Items)
{
    public int TotalScu => Items.Sum(i => i.Scu);
}
