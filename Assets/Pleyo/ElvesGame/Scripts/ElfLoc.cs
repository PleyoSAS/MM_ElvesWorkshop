using System.Collections.Generic;
using PleyoDLL.Interface;

/// <summary>FR / EN texts, language from the config file ("Lang"). Add a language by adding a table.</summary>
public static class ElfLoc
{
    static readonly Dictionary<string, string> _fr = new Dictionary<string, string>
    {
        { "TITLE", "L'atelier des lutins" },
        { "JUMP_TO_PLAY", "Sautez pour jouer !" },
        { "TUTO_1", "Sautez pour rejoindre la chaîne !" },
        { "TUTO_2", "Atterrissez ensemble quand le jouet passe sous votre machine" },
        { "TUTO_3", "Jouet raté = jouet inachevé : ne le chargez pas dans le traîneau !" },
        { "GO", "C'est parti !" },
        { "STEP_0", "Sculpter" }, { "STEP_1", "Peindre" }, { "STEP_2", "Emballer" }, { "STEP_3", "Charger" },
        { "GIFTS", "Cadeaux livrés" },
        { "UNFINISHED", "Cadeaux inachevés chargés" },
        { "TEAMS", "Équipes" },
        { "SYNC_OK", "Synchro !" },
        { "SYNC_KO", "Ensemble !" },
        { "MISSED", "Raté !" },
        { "TIME_UP", "Temps écoulé !" },
        { "RESULTS", "Bravo les lutins !" },
        { "BEST_TEAM", "Meilleure équipe" },
        { "FUN", "Le Père Noël va avoir des surprises..." },
        { "ROBOT", "Robot" },
    };

    static readonly Dictionary<string, string> _en = new Dictionary<string, string>
    {
        { "TITLE", "Elves' Workshop" },
        { "JUMP_TO_PLAY", "Jump to play!" },
        { "TUTO_1", "Jump to join the assembly line!" },
        { "TUTO_2", "Land together when the toy passes under your machine" },
        { "TUTO_3", "Missed toy = unfinished toy: don't load it on the sleigh!" },
        { "GO", "Let's go!" },
        { "STEP_0", "Carve" }, { "STEP_1", "Paint" }, { "STEP_2", "Wrap" }, { "STEP_3", "Load" },
        { "GIFTS", "Gifts delivered" },
        { "UNFINISHED", "Unfinished gifts loaded" },
        { "TEAMS", "Teams" },
        { "SYNC_OK", "In sync!" },
        { "SYNC_KO", "Together!" },
        { "MISSED", "Missed!" },
        { "TIME_UP", "Time's up!" },
        { "RESULTS", "Well done, elves!" },
        { "BEST_TEAM", "Best team" },
        { "FUN", "Santa is in for a few surprises..." },
        { "ROBOT", "Robot" },
    };

    static bool? _isFrench;

    public static bool IsFrench()
    {
        if (_isFrench == null)
        {
            PleyoDLL_ConfigurationFileExtended config = PleyoDLL_ConfigurationFileExtended.LoadConfigurationData();
            _isFrench = config != null && !string.IsNullOrEmpty(config.Lang) && config.Lang.ToUpperInvariant().StartsWith("FR");
        }
        return _isFrench.Value;
    }

    public static string Get(string key)
    {
        Dictionary<string, string> table = IsFrench() ? _fr : _en;
        return table.TryGetValue(key, out string text) ? text : key;
    }
}
