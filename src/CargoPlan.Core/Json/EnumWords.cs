using System.Text.Json;

namespace CargoPlan.Core.Json;

/// <summary>
/// An enum value as the files spell it: camelCase as in docs/hauling-data-format.md ("xs", "redWind", "aToB"). One
/// vocabulary for routes.json and the hauling data (T13.2); <c>RoutesFileTests</c> pins the words, so a renamed member shows.
/// </summary>
public static class EnumWords
{
    public static string Word<T>(T value) where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    public static bool TryParse<T>(string word, out T value) where T : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<T>())
        {
            if (Word(candidate) == word)
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <exception cref="ArgumentException">The word is no member's.</exception>
    public static T Parse<T>(string word) where T : struct, Enum =>
        TryParse<T>(word, out var value) ? value : throw new ArgumentException(Unknown<T>(word));

    /// <summary>"HaulRank 'boss' is unknown, use "trainee", …".</summary>
    public static string Unknown<T>(string word) where T : struct, Enum =>
        $"{typeof(T).Name} '{word}' is unknown, use {string.Join(", ", Enum.GetValues<T>().Select(v => $"\"{Word(v)}\""))}.";
}
