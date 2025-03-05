using System.Data;
using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Newtonsoft.Json;
using Noggog;
using SynPatcher.Types;
using SynPatcher.Types.Ini;

namespace SynPatcher;

public class Program
{
    static Lazy<Settings> lazySettings = new();
    static HashSet<ModKey> ignoredMods => lazySettings.Value.ignoredMods;
    public static async Task<int> Main(string[] args)
    {
        return await SynthesisPipeline.Instance
            .SetAutogeneratedSettings("HVLSettings", "HVLFramework.json", out lazySettings)
            .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
            .SetTypicalOpen(GameRelease.SkyrimSE, "SynWeaponKeywords.esp")
            .Run(args);
    }
    public static void PatchRace(Race newVL, IRaceGetter vanillaVL, IRaceGetter vampireRace)
    {
        newVL.DeepCopyIn(vampireRace, new Race.TranslationMask(true)
        {
            EditorID = false,
            FormKey = false
        });
        newVL.DeepCopyIn(vanillaVL, new Race.TranslationMask(false)
        {
            AccelerationRate = true,
            ActorEffect = true,
            AimAngleTolerance = true,
            AngularAccelerationRate = true,
            AngularTolerance = true,
            AttackRace = true,
            Attacks = true,
            BaseCarryWeight = true,
            BaseMass = true,
            BaseMovementDefaultFly = true,
            BaseMovementDefaultRun = true,
            BaseMovementDefaultSneak = true,
            BaseMovementDefaultSprint = true,
            BaseMovementDefaultSwim = true,
            BaseMovementDefaultWalk = true,
            BehaviorGraph = new GenderedItem<Model.TranslationMask>(new Model.TranslationMask(true), new Model.TranslationMask(true)),
            CloseLootSound = true,
            DATADataTypeState = true,
            DecelerationRate = true,
            Description = true,
            FlightRadius = true,
            ImpactDataSet = true,
            Keywords = true,
            MovementTypeNames = true,
            MovementTypes = true,
            Name = true,
            Regen = true,
            ShieldBipedObject = true,
            Starting = true,
            UnarmedDamage = true,
            UnarmedEquipSlot = true,
            UnarmedReach = true,
            EquipmentFlags = true,
        });
        //Manual Fixes
        newVL.AttackRace.SetTo(vanillaVL);
        if (vampireRace.HeadData != null)
        {
            newVL.HeadData = new GenderedItem<HeadData?>(vampireRace.HeadData.Male?.DeepCopy() ?? null, vampireRace.HeadData.Female?.DeepCopy() ?? null);
        }
        if (vampireRace.DecapitateArmors != null)
        {
            newVL.DecapitateArmors = new GenderedItem<IFormLinkGetter<IArmorGetter>>(vampireRace.DecapitateArmors.Male, vampireRace.DecapitateArmors.Female);
        }
        if (vampireRace.DefaultHairColors != null)
        {
            newVL.DefaultHairColors = new GenderedItem<IFormLinkGetter<IColorRecordGetter>>(vampireRace.DefaultHairColors.Male, vampireRace.DefaultHairColors.Female);
        }
        // Manual Fixes
        if (vampireRace.Eyes != null)
        {
            newVL.Eyes = [];
            newVL.Eyes.SetTo(vampireRace.Eyes);
        }
        newVL.ArmorRace.SetTo(vampireRace.ArmorRace);
        newVL.MorphRace.SetTo(vampireRace.MorphRace);
        newVL.SkeletalModel!.Female = vampireRace.SkeletalModel!.Female!.DeepCopy();
        newVL.SkeletalModel!.Male = vampireRace.SkeletalModel!.Male!.DeepCopy();
        newVL.Height.Female = vampireRace.Height.Female;
        newVL.Height.Male = vampireRace.Height.Female;
        newVL.BehaviorGraph.Female = vanillaVL.BehaviorGraph?.Female?.DeepCopy();
        newVL.BehaviorGraph.Male = vanillaVL.BehaviorGraph?.Male?.DeepCopy();
        newVL.Flags |= Race.Flag.NoKnockdowns;
    }

    public static void UpdateInis(string dfp, List<RaceConf> races)
    {
        var facemorphpath = Path.Join(dfp, "meshes", "actors", "character", "facegenmorphs");
        foreach (var file in Directory.EnumerateDirectories(facemorphpath))
        {
            if (file.EndsWith("esp", StringComparison.InvariantCultureIgnoreCase) || file.EndsWith("esl", StringComparison.InvariantCultureIgnoreCase) || file.EndsWith("esm", StringComparison.InvariantCultureIgnoreCase))
            {
                var raceIni = Path.Join(file, "races.ini");
                Console.WriteLine($"Updating ini {raceIni}");
                if (File.Exists(raceIni))
                {
                    using var ini = new IniFile(raceIni);
                    foreach (var rc in races)
                    {
                        if (ini.KeyExists(rc.VampireRace))
                        {
                            ini.Write(rc.VLRace, ini.Read(rc.VampireRace));
                        }
                    }
                }
            }
        }
    }

    public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
    {
        List<RaceConf> races = [];
        var VLController = FormKey.Factory("000010:VLRP.esp");
        var vanillaVL = Dawnguard.Race.DLC1VampireBeastRace.Resolve(state.LinkCache);
        var vampireRaces = state.LoadOrder.PriorityOrder.Race().WinningOverrides().Where(x => x.EditorID != "DLC1VampireBeastRace" && !x.EditorID!.Contains("VampireLord") && !ignoredMods.Contains(x.FormKey.ModKey)).Where(x => x.HasKeyword("Vampire", state.LinkCache)).ToHashSet();
        foreach (var vampireRace in vampireRaces)
        {
            if (!vampireRaces.Where(x => x.EditorID == $"{vampireRace.EditorID}Lord").Any())
            {
                Console.WriteLine($"Generating Vampire Lord Race for {vampireRace.EditorID}");
                races.Add(new()
                {
                    VampireRace = vampireRace.EditorID!,
                    VLRace = $"{vampireRace.EditorID!}Lord"
                });
                var newVL = state.PatchMod.Races.AddNew($"{vampireRace.EditorID}Lord");
                PatchRace(newVL, vanillaVL, vampireRace);
                newVL.ActorEffect!.Add(VLController);
            }
        }
        var txt = JsonConvert.SerializeObject(races);
        var path = Path.Join(state.DataFolderPath, "SKSE", "Plugins", "VLRP");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            Console.WriteLine("[WARN] Directory created");
        }
        File.WriteAllText(Path.Join(path, "Synthesis.json"), txt);
        var vanillapath = Path.Join(state.DataFolderPath, "SKSE", "Plugins", "VLRP", "IncludedRaces.json");
        if (File.Exists(vanillapath))
        {
            var vraces = JsonConvert.DeserializeObject<List<RaceConf>>(File.ReadAllText(vanillapath));
            if (vraces != null)
            {
                races.AddRange(vraces);
            }
        }
        else
        {
            Console.WriteLine("[WARN] IncludedRaces.json not found");
        }
        races = [.. races.DistinctBy(x => x.VampireRace)];
        UpdateInis(state.DataFolderPath, races);
        var UpdateRaces = state.LoadOrder.PriorityOrder.Race().WinningOverrides().Where(x => x.FormKey.ModKey == "VLRP.esp").ToHashSet();
        foreach (var race in UpdateRaces)
        {
            Console.WriteLine($"Updating {race.EditorID}");
            var newVL = state.PatchMod.Races.GetOrAddAsOverride(race);
            var vampireRace = state.LoadOrder.PriorityOrder.Race().WinningOverrides().Where(x => x.EditorID == newVL.EditorID!.Replace("Lord", "")).First();
            PatchRace(newVL, vanillaVL, vampireRace);
            newVL.ActorEffect!.Add(VLController);
        }
    }
}
