using Mutagen.Bethesda.Plugins;

namespace SynPatcher.Types;
public struct RaceConf
{
    public string VampireRace;
    public string VLRace;
}
public class Settings
{
    public HashSet<ModKey> ignoredMods = [
        "Skyrim.esm",
        "VLRP.esp"
    ];
}