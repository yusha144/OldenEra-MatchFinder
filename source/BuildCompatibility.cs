using System.Security.Cryptography;

namespace OldenEra.MatchFinder;

internal static class BuildCompatibility
{
    internal const string SteamBuild = "26428102";
    internal const string AssemblyHash = "D5C932FC0E22BE3049204773CF2A9CC57F6A84D08540921D99506D53B96386E2";
    internal const string MetadataHash = "C40298DE4A952A0CE5158BF07C392335EEA1F6B39B544101FF04B974794D1105";

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
