using System.Security.Cryptography;

namespace OldenEra.MatchFinder;

internal static class BuildCompatibility
{
    internal const string SteamBuild = "25672315";
    internal const string AssemblyHash = "D9972B0E7F7DD1C17758808E2F1B80E0A728946416D9A41DEAA99BAD744DE53A";
    internal const string MetadataHash = "99117DC93F4B427967A2FCA49378E1B39B3202401B15234A98328FAA7D489817";

    internal static bool IsSupported(string gameRoot) =>
        Matches(Path.Combine(gameRoot, "GameAssembly.dll"), AssemblyHash) &&
        Matches(Path.Combine(gameRoot, "HeroesOldenEra_Data", "il2cpp_data", "Metadata", "global-metadata.dat"), MetadataHash);

    internal static bool Matches(string path, string expectedHash)
    {
        using var stream = File.OpenRead(path);
        using var hash = SHA256.Create();
        return string.Equals(Convert.ToHexString(hash.ComputeHash(stream)), expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
