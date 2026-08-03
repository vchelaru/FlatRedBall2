namespace RiftboundSample.Levels;

public static class MapRegistry
{
    public static MapData Get(string mapId) => mapId switch
    {
        "brasshollow" => Brasshollow.Map,
        "rustfields" => Rustfields.Map,
        "cogspire_academy" => CogspireAcademy.Map,
        "fort_ironmaw" => FortIronmaw.Map,
        "scorched_vents" => ScorchedVents.Map,
        "crystal_glade" => CrystalGlade.Map,
        "dreamspire_temple" => DreamspireTemple.Map,
        "shimmering_grotto" => ShimmeringGrotto.Map,
        "floating_isles" => FloatingIsles.Map,
        "luminous_spire" => LuminousSpire.Map,
        "data_core" => DataCore.Map,
        "pixel_bazaar" => PixelBazaar.Map,
        "firewall_fortress" => FirewallFortress.Map,
        "glitch_wastes" => GlitchWastes.Map,
        "nexus_spire" => NexusSpire.Map,
        "rift_entrance" => RiftEntrance.Map,
        "fractured_path" => FracturedPath.Map,
        "temporal_nexus" => TemporalNexus.Map,
        "final_sanctum" => FinalSanctum.Map,
        _ => throw new ArgumentException($"Unknown map: {mapId}"),
    };

    public static MapTheme GetTheme(string mapId) => mapId switch
    {
        "brasshollow" or "rustfields" or "cogspire_academy"
            or "fort_ironmaw" or "scorched_vents" => MapTheme.Overworld,
        "crystal_glade" or "dreamspire_temple" or "shimmering_grotto"
            or "floating_isles" or "luminous_spire" => MapTheme.Ethereal,
        "data_core" or "pixel_bazaar" or "firewall_fortress"
            or "glitch_wastes" or "nexus_spire" => MapTheme.Nexus,
        "rift_entrance" or "fractured_path" or "temporal_nexus"
            or "final_sanctum" => MapTheme.Fade,
        _ => MapTheme.Overworld,
    };
}
