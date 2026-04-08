namespace CfaDatabaseEditor.Services;

/// <summary>
/// Port of preformat_card_text.gml - converts raw card text into rendering tokens.
/// All replacements must happen in the exact order as the GML source.
/// </summary>
public static class TextPreprocessor
{
    // Token constants matching GML's rs_* variables.
    // These strings are chosen so their pixel width in Georgia Pro matches the icon sprite width,
    // which keeps word-wrapping correct when MeasureText is used.
    public const string RS_COST = "^";
    public const string RS_CBLAST = "CBlast";
    public const string RS_CCHARGE = "CCharg";
    public const string RS_SBLAST = "SBlast";
    public const string RS_SCHARGE = "SCharg";
    public const string RS_EBLAST = "EBlast";
    public const string RS_ECHARGE = "ECharg";
    public const string RS_POWER = "W\\";
    public const string RS_SHIELD = "SL|";
    public const string RS_CRIT = "cr_";
    public const string RS_ODRESS = "overDress";
    public const string RS_OSOUL = "OvSo";
    public const string RS_MELODY = "MELODY";
    public const string RS_DBOOST = "DressBoost";
    private const char NBSP = '\u00A0';          // Non-breaking space

    public static string Preformat(string text)
    {
        var ct = text;

        // Skip Russian translations (langRu = 0 for this editor)

        // Protect specific words from later replacements
        ct = ct.Replace("the rest", "the RST");
        ct = ct.Replace("stand phase", "STND phase");
        ct = ct.Replace("Arrester", "ARRESTER");
        ct = ct.Replace("Crest", "CREST");
        ct = ct.Replace("crest", "cREST");
        ct = ct.Replace("Forest", "FOREST");
        ct = ct.Replace("stand up", "STAND UP");
        ct = ct.Replace("Glittering", "GLITERING");

        // Non-breaking spaces for Legion keywords
        ct = ct.Replace("perform Legion", "perform" + NBSP + "Legion");
        ct = ct.Replace("and Legion", "and" + NBSP + "Legion");
        ct = ct.Replace(" Legion Mate", NBSP + "Legion Mate");

        ct = ct.Replace("[Rest", "[rest");
        ct = ct.Replace("] Rest", "] rest");
        ct = ct.Replace("] Stand", "] stand");
        ct = ct.Replace("-card with", " -card with");

        // Convert ability keywords to tokens
        ct = ct.Replace("G guardian", "G_guardian");
        ct = ct.Replace("Stride", "Stride/");
        ct = ct.Replace("Ultimate Stride/", "Ult_Stride_");
        ct = ct.Replace("Legion", "LGN");
        ct = ct.Replace("LGN 2", "LG|__2");

        // Cost keywords
        ct = ct.Replace("Counter Blast", RS_CBLAST.ToString());
        ct = ct.Replace("Counter Charge", RS_CCHARGE.ToString());
        ct = ct.Replace("Soul Blast", RS_SBLAST.ToString());
        ct = ct.Replace("Soul Charge", RS_SCHARGE.ToString());
        ct = ct.Replace("Energy Charge", RS_ECHARGE.ToString());
        ct = ct.Replace("Energy Blast", RS_EBLAST.ToString());

        // Special abilities
        ct = ct.Replace("OverSoul", RS_OSOUL.ToString());
        ct = ct.Replace("X-overDress", "XverDress");
        ct = ct.Replace("overDress", RS_ODRESS.ToString());
        ct = ct.Replace("with Melody", "with " + RS_MELODY);
        ct = ct.Replace("Melody -", RS_MELODY + " -");
        ct = ct.Replace("Melody.", RS_MELODY + ".");
        ct = ct.Replace("Melody (", RS_MELODY + " (");
        ct = ct.Replace("[Event]", "Ev\u0435nt"); // Cyrillic 'е'
        ct = ct.Replace("[Pickup Gacha]", "Pickup|Gacha");
        ct = ct.Replace("Regalis Piece", "RegalisPiece");
        ct = ct.Replace("[Divine Skill]", "DivineSkill");
        ct = ct.Replace("[Mate Unit]", "[MateUnit]");
        ct = ct.Replace("Mental Pollution", "Mental_Pol");
        ct = ct.Replace("Call Cost", "Call|Cost");
        ct = ct.Replace("Double Attack", "Double|Attack");
        ct = ct.Replace("[Ace Unit]", "Ace__Unit");
        ct = ct.Replace("Unique Skill", "UniqueSkill");
        ct = ct.Replace("Dangerous Skill", "DangerSkil");
        ct = ct.Replace("[ExplosiveGrowth]", "ExploGrowth");
        ct = ct.Replace("[Deal]", "[_Deal_]");
        ct = ct.Replace("[Retry]", "[__Retry__");
        ct = ct.Replace("[Arma Arms]", "ArmaArms");
        ct = ct.Replace("Happy Toys", "HappyToys");
        ct = ct.Replace("D-LEGION", "DLegion");

        // Attach cost numbers with em-dash
        for (int i = 0; i < 15; i++)
        {
            var num = i.ToString();
            ct = ct.Replace(RS_CBLAST + " " + num, RS_CBLAST + "\u2014" + num);
            ct = ct.Replace(RS_CCHARGE + " " + num, RS_CCHARGE + "\u2014" + num);
            ct = ct.Replace(RS_SBLAST + " " + num, RS_SBLAST + "\u2014" + num);
            ct = ct.Replace(RS_SCHARGE + " " + num, RS_SCHARGE + "\u2014" + num);
            ct = ct.Replace(RS_EBLAST + " " + num, RS_EBLAST + "\u2014" + num);
            ct = ct.Replace(RS_ECHARGE + " " + num, RS_ECHARGE + "\u2014" + num);
            // Slash between costs
            ct = ct.Replace(num + "/" + RS_CBLAST, num + " / " + RS_CBLAST);
            ct = ct.Replace(num + "/" + RS_CCHARGE, num + " / " + RS_CCHARGE);
            ct = ct.Replace(num + "/" + RS_SBLAST, num + " / " + RS_SBLAST);
            ct = ct.Replace(num + "/" + RS_SCHARGE, num + " / " + RS_SCHARGE);
        }

        // Limit Break / Generation Break
        ct = ct.Replace("Limit Break ", "LB_");
        ct = ct.Replace("Generation Break 1", "GB_1");
        ct = ct.Replace("Generation Break 2", "GB_2");
        ct = ct.Replace("Generation Break 3", "GB_3");
        ct = ct.Replace("Generation Break 4", "GB_4");
        ct = ct.Replace("Generation Break 8", "GB_8");

        // Trigger names to tokens
        ct = ct.Replace("critical trigger", "t_0 trigger");
        ct = ct.Replace("draw trigger", "t_1 trigger");
        ct = ct.Replace("heal trigger", "t_2 trigger");
        ct = ct.Replace("stand trigger", "t_3 trigger");
        ct = ct.Replace("front trigger", "t_4 trigger");
        ct = ct.Replace("over trigger", "t_5 trigger");
        ct = ct.Replace("\u2022 Critical trigger", "\u2022 t_0 trigger");
        ct = ct.Replace("\u2022 Draw trigger", "\u2022 t_1 trigger");
        ct = ct.Replace("\u2022 Heal trigger", "\u2022 t_2 trigger");
        ct = ct.Replace("\u2022 Stand trigger", "\u2022 t_3 trigger");
        ct = ct.Replace("\u2022 Front trigger", "\u2022 t_4 trigger");
        ct = ct.Replace("\u2022 Over trigger", "\u2022 t_5 trigger");
        ct = ct.Replace("Critical:", "t_0:");
        ct = ct.Replace("Draw:", "t_1:");
        ct = ct.Replace("Heal:", "t_2:");
        ct = ct.Replace("Stand:", "t_3:");
        ct = ct.Replace("Front:", "t_4:");
        ct = ct.Replace("\"HEAL\"", "\"t_2HEAL\"");
        ct = ct.Replace("\"Heal\"", "\"t_2Heal\"");

        // Game terms to icon tokens
        ct = ct.Replace("power", RS_POWER.ToString());
        ct = ct.Replace("shield", RS_SHIELD.ToString());
        ct = ct.Replace("critical", RS_CRIT.ToString());
        ct = ct.Replace("stand", "SN");
        ct = ct.Replace("rest", "RT");
        ct = ct.Replace("COST", RS_COST.ToString());
        ct = ct.Replace(RS_COST + " ", RS_COST.ToString() + NBSP);
        ct = ct.Replace("Glitter", "Glitter_");
        ct = ct.Replace("Volundr", "Volund|");

        // Restore protected words
        ct = ct.Replace(NBSP + "RT", " rest");
        ct = ct.Replace("perform" + NBSP + "LGN", "perform Legion");
        ct = ct.Replace("and" + NBSP + "LGN", "and Legion");
        ct = ct.Replace(NBSP + "LGN Mate", " Legion Mate");

        ct = ct.Replace("RST", "rest");
        ct = ct.Replace("STND", "stand");
        ct = ct.Replace("ARRESTER", "Arrester");
        ct = ct.Replace("CREST", "Crest");
        ct = ct.Replace("cREST", "crest");
        ct = ct.Replace("FOREST", "Forest");
        ct = ct.Replace("STAND UP", "stand up");
        ct = ct.Replace("GLITERING", "Glittering");

        return ct;
    }
}
