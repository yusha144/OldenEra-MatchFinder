using System.Security.Cryptography;

namespace OldenEra.MatchFinder;

internal static class BuildCompatibility
{
    internal const string SteamBuild = "26321935";
    internal const string AssemblyHash = "20AD026C2E10ABFCB17320E8174B611551857B5D97D59BEBC29142B28CA45CB3";
    internal const string MetadataHash = "BB50ABC2B3CBFCFA61617674A723761A5542831C263A34C6A70486840863F846";

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
