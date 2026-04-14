using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using SkiaSharp;

namespace CfaDatabaseEditor.Services;

/// <summary>
/// Port of draw_card_text.gml + draw_card_text_word.gml + supporting scripts.
/// Renders card text to an SKCanvas with icons, keyword highlights, and styled text.
/// GameMaker uses $BBGGRR color format - all colors here are converted to standard RGB.
/// </summary>
public class CardTextRenderer
{
    // Colors converted from GameMaker $BBGGRR to standard RGB
    private static readonly SKColor AbilityModeColor = new(0xD1, 0x23, 0x2A);   // GML $2a23d1
    private static readonly SKColor ActColor = new(0x2E, 0x30, 0x92);            // GML $92302E
    private static readonly SKColor AutoColor = new(0x00, 0x8C, 0x4B);           // GML $4b8c00
    private static readonly SKColor ContColor = new(0xD1, 0x23, 0x2A);           // GML $2a23d1
    private static readonly SKColor TurnColor = new(0xB2, 0x0B, 0x7F);           // GML $7f0bb2
    private static readonly SKColor SoulguardColor = new(0xD1, 0x23, 0x2A);      // GML $2a23d1
    private static readonly SKColor CBlastCircle = new(0x35, 0x38, 0x6E);        // GML $6e3835
    private static readonly SKColor SBlastCircle = new(0xAF, 0x23, 0x21);        // GML $2123af
    private static readonly SKColor EBlastCircle = new(0x06, 0x67, 0x3E);        // GML $3e6706
    private static readonly SKColor DoubleAtkColor = new(0xD1, 0x23, 0x2A);      // GML $2a23d1
    private static readonly SKColor CallCostColor = new(0x40, 0x40, 0x40);       // GML $404040

    private readonly Dictionary<string, SKBitmap> _icons = new();
    private SKBitmap? _zatemnenie;
    private SKTypeface? _fontRegular;
    private SKTypeface? _fontItalic;
    private SKTypeface? _fontTitle;
    private float FontSize = 12.79f; // Scaled from GML's 10pt to look good at 300px width

    public void SetFontSize(float size) => FontSize = size;
    private const float TitleFontSize = 16f; // Oswald 12pt scaled for display
    private const float TitleLineHeight = 18f; // GML draw_text_ext sep=18
    private const float TitleSkewAngle = -0.2f; // GM faux italic skew
    private const float CostFontSize = 10f;
    private const float MaxWidth = 280f;
    private const float TextureWidth = 300f;
    private const float TextureHeight = 428f;
    private const float TitleY = 12f; // Title starts 12px below top of texture
    private const float TextY = 54f;  // Card text starts 54px below top of texture

    public bool IsLoaded => _fontRegular != null;

    public void LoadResources(string? assetsPath = null)
    {
        // Try loading icons from embedded Avalonia resources first
        var iconsBaseUri = new Uri("avares://CfaDatabaseEditor/Assets/Icons");
        try
        {
            foreach (var assetUri in AssetLoader.GetAssets(iconsBaseUri, null))
            {
                var path = assetUri.AbsolutePath;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                var name = Path.GetFileNameWithoutExtension(path);
                using var stream = AssetLoader.Open(assetUri);
                var bitmap = SKBitmap.Decode(stream);
                if (bitmap != null)
                    _icons[name] = bitmap;
            }
        }
        catch
        {
            // Fall back to filesystem loading
            if (assetsPath != null)
            {
                var iconsPath = Path.Combine(assetsPath, "Icons");
                if (Directory.Exists(iconsPath))
                {
                    foreach (var file in Directory.GetFiles(iconsPath, "*.png"))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        var bitmap = SKBitmap.Decode(file);
                        if (bitmap != null)
                            _icons[name] = bitmap;
                    }
                }
            }
        }

        // Load Zatemnenie background texture (light mode = 0.png)
        if (_icons.TryGetValue("Zatemnenie_0", out var zat))
            _zatemnenie = zat;

        // Try to load Georgia Pro, fall back to Georgia, then default serif
        _fontRegular = SKTypeface.FromFamilyName("Georgia Pro")
                    ?? SKTypeface.FromFamilyName("Georgia")
                    ?? SKTypeface.FromFamilyName("serif");
        _fontItalic = SKTypeface.FromFamilyName("Georgia Pro", SKFontStyleWeight.Light, SKFontStyleWidth.Normal, SKFontStyleSlant.Italic)
                   ?? SKTypeface.FromFamilyName("Georgia", SKFontStyle.Italic)
                   ?? SKTypeface.FromFamilyName("serif", SKFontStyle.Italic);

        // Oswald bold for card title (GM uses bold+italic but Oswald has no italic, so we skew)
        _fontTitle = SKTypeface.FromFamilyName("Oswald", SKFontStyle.Bold)
                  ?? SKTypeface.FromFamilyName("Oswald")
                  ?? SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold);
    }

    /// <summary>
    /// Renders the full card text panel: Zatemnenie background, title, and card text with icons.
    /// </summary>
    public float RenderFull(SKCanvas canvas, string cardName, string rawCardText, bool extendedTextBox)
    {
        if (_fontRegular == null) return 0;

        float bgHeight = TextureHeight;
        float yScale = extendedTextBox ? 1.2f : 1f;
        bgHeight *= yScale;

        // Draw Zatemnenie background
        if (_zatemnenie != null)
        {
            var destRect = SKRect.Create(0, 0, TextureWidth, bgHeight);
            canvas.DrawBitmap(_zatemnenie, destRect);
        }
        else
        {
            // Fallback: dark semi-transparent rectangle
            using var bgPaint = new SKPaint { Color = new SKColor(0, 0, 0, 180) };
            canvas.DrawRect(0, 0, TextureWidth, bgHeight, bgPaint);
        }

        // Draw title - centered, Oswald bold, faux italic (skewed)
        if (!string.IsNullOrEmpty(cardName))
        {
            DrawTitle(canvas, UnescapeHash(cardName.Trim()), TitleY);
        }

        // Draw card text
        if (!string.IsNullOrEmpty(rawCardText))
        {
            Render(canvas, UnescapeHash(rawCardText), 10, TextY);
        }

        return bgHeight;
    }

    private void DrawTitle(SKCanvas canvas, string title, float y)
    {
        using var titlePaint = new SKPaint
        {
            IsAntialias = true,
            SubpixelText = true,
            HintingLevel = SKPaintHinting.Slight,
            TextSize = TitleFontSize,
            Typeface = _fontTitle,
            Color = SKColors.Black,
            TextAlign = SKTextAlign.Center
        };

        // Faux italic: apply skew matrix (GameMaker skews fonts without true italic)
        canvas.Save();
        var skewMatrix = SKMatrix.CreateSkew(TitleSkewAngle, 0);
        var translateToCenter = SKMatrix.CreateTranslation(TextureWidth / 2, 0);
        canvas.SetMatrix(canvas.TotalMatrix.PostConcat(translateToCenter).PostConcat(skewMatrix));

        // Word-wrap title to 280px width, line sep = 18
        var words = title.Split(' ');
        float cx = 0;
        float cy = y + TitleFontSize;
        var line = "";

        foreach (var word in words)
        {
            var test = string.IsNullOrEmpty(line) ? word : line + " " + word;
            if (titlePaint.MeasureText(test) > MaxWidth && !string.IsNullOrEmpty(line))
            {
                canvas.DrawText(line, 0, cy, titlePaint);
                cy += TitleLineHeight;
                line = word;
            }
            else
            {
                line = test;
            }
        }
        if (!string.IsNullOrEmpty(line))
            canvas.DrawText(line, 0, cy, titlePaint);

        canvas.Restore();
    }

    /// <summary>
    /// Renders preprocessed card text to an SKCanvas.
    /// Returns the total height used.
    /// </summary>
    public float Render(SKCanvas canvas, string rawCardText, float startX, float startY)
    {
        if (_fontRegular == null) return 0;

        var preprocessed = TextPreprocessor.Preformat(rawCardText)
            .Replace("\r\n", "\n").Replace("\r", "\n");
        var words = preprocessed.Split(' ');

        // State machine (matching GML globals)
        bool abilityMode = false;
        bool costMode = false;
        bool helpMode = false;
        bool abilityLegionMode = false;
        bool firstCostWord = false;
        int quotesCounter = 0;

        float cx = 0;
        float cy = 0;
        float lineHeight = GetLineHeight();

        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            SubpixelText = true,
            HintingLevel = SKPaintHinting.Slight,
            TextSize = FontSize,
            Typeface = _fontRegular,
            Color = SKColors.Black
        };

        using var costPaint = new SKPaint
        {
            IsAntialias = true,
            SubpixelText = true,
            HintingLevel = SKPaintHinting.Slight,
            TextSize = CostFontSize,
            Typeface = _fontRegular,
            Color = SKColors.White
        };

        for (int i = 0; i < words.Length; i++)
        {
            var wordOriginal = words[i] + (i < words.Length - 1 ? " " : "");
            var prevWord = i > 0 ? words[i - 1] : "";

            // Set base color
            textPaint.Color = abilityMode ? AbilityModeColor : SKColors.Black;
            textPaint.Typeface = helpMode ? _fontItalic : _fontRegular;

            // Check for COST mode entry
            if (wordOriginal.StartsWith(TextPreprocessor.RS_COST.ToString()) ||
                (wordOriginal.Contains('[') && prevWord.Contains("Call|Cost")))
            {
                costMode = true;
                firstCostWord = true;
            }

            // Handle newlines
            if (wordOriginal.Contains('\n'))
            {
                var parts = wordOriginal.Split('\n');
                for (int p = 0; p < parts.Length; p++)
                {
                    if (p > 0)
                    {
                        cy += lineHeight;
                        cx = 0;
                        firstCostWord = true;
                        helpMode = false;
                        costMode = false;
                        abilityMode = false;
                    }
                    if (!string.IsNullOrEmpty(parts[p]))
                    {
                        RenderWord(canvas, parts[p], startX + cx, startY + cy,
                            textPaint, costPaint, ref abilityMode, ref costMode,
                            ref helpMode, ref firstCostWord);
                        cx += MeasureWord(parts[p], textPaint);
                    }
                }
            }
            else
            {
                // Render word
                RenderWord(canvas, wordOriginal, startX + cx, startY + cy,
                    textPaint, costPaint, ref abilityMode, ref costMode,
                    ref helpMode, ref firstCostWord);
                cx += MeasureWord(wordOriginal, textPaint);

                // Check if next word would overflow
                if (i + 1 < words.Length)
                {
                    var nextWord = words[i + 1].Split('\n')[0];
                    if (cx + MeasureWord(nextWord, textPaint) > MaxWidth)
                    {
                        cy += lineHeight;
                        cx = 0;
                        firstCostWord = true;
                    }
                }
            }

            // Track ability mode (quoted ability text turns blue/red)
            var wordAfterChar2 = wordOriginal.Length > 1 ? wordOriginal.Substring(1) : "";

            if (abilityMode && wordOriginal.StartsWith('"'))
                quotesCounter++;
            if (wordAfterChar2.Contains('"'))
            {
                if (abilityMode)
                {
                    quotesCounter -= CountChar(wordAfterChar2, '"');
                    if (quotesCounter <= 0)
                        abilityMode = false;
                }
            }

            // Ability mode detection — ported from draw_card_text.gml
            if (i < words.Length - 1 && i > 0)
            {
                var nextWord = words[i + 1];

                if (wordOriginal.StartsWith("LG|__2"))
                    abilityLegionMode = true;

                bool nextQuote = nextWord.StartsWith('\"');
                bool enters =
                    // gets/get/gets,/get, + quote
                    (wordOriginal is "gets " or "get " or "gets, " or "get, " && nextQuote) ||
                    // loses/lose + quote
                    (wordOriginal is "loses " or "lose " && nextQuote) ||
                    // pay + quote
                    (wordOriginal == "pay " && nextQuote) ||
                    // and + specific quoted keywords
                    (wordOriginal == "and " && (
                        nextWord.StartsWith("\"Boost") || nextWord.StartsWith("\"Intercept") ||
                        nextWord.StartsWith("\"AUTO") || nextWord.StartsWith("\"CONT") ||
                        nextWord.StartsWith("\"ACT") || nextWord.StartsWith("\"Shadowstitch") ||
                        nextWord.StartsWith("\"Rescue") || nextWord.StartsWith("\"Time") ||
                        nextWord.StartsWith("\"Draw"))) ||
                    // or + quoted CCharg
                    (wordOriginal == "or " && nextWord.StartsWith("\"CCharg")) ||
                    // may + quote
                    (wordOriginal == "may " && nextQuote) ||
                    // perform/performs + quote
                    (wordOriginal is "perform " or "performs " && nextQuote) ||
                    // play (after may) + quote
                    (wordOriginal == "play " && prevWord == "may" && nextQuote) ||
                    // of (after instead) + quote
                    (wordOriginal == "of " && prevWord == "instead" && nextQuote) ||
                    // with (after paid/ride, or before Boost/Intercept) + quote
                    (wordOriginal == "with " && (prevWord == "paid" || prevWord == "ride" ||
                        nextWord.StartsWith("\"Boost") || nextWord.StartsWith("\"Intercept")) && nextQuote) ||
                    // to (after changes/change/check) + quote
                    (wordOriginal == "to " && (prevWord == "changes" || prevWord == "change" || prevWord == "check") && nextQuote) ||
                    // vanguard's/vanguards/Vanguards + quote
                    (wordOriginal is "vanguard\u2019s " or "vanguards " or "Vanguards " && nextQuote) ||
                    // trigger quotes: "t_0: / "t_1: / "t_2: / "t_3:
                    (nextWord.StartsWith("\"t_0:") || nextWord.StartsWith("\"t_1:") ||
                     nextWord.StartsWith("\"t_2:") || nextWord.StartsWith("\"t_3:")) ||
                    // legion mode + quote
                    (abilityLegionMode && nextQuote);

                if (enters)
                {
                    abilityMode = true;
                    quotesCounter = 0;
                }
            }

            // Cost mode exit
            if (wordAfterChar2.Contains(']'))
                costMode = false;

            // Help mode exit
            if (wordAfterChar2.Contains(')'))
                helpMode = false;
        }

        return cy + lineHeight;
    }

    private void RenderWord(SKCanvas canvas, string word, float x, float y,
        SKPaint textPaint, SKPaint costPaint,
        ref bool abilityMode, ref bool costMode, ref bool helpMode, ref bool firstCostWord)
    {
        // Cost underline
        if (costMode)
        {
            float lineY = y + GetLineHeight();
            using var linePaint = new SKPaint
            {
                Color = abilityMode ? AbilityModeColor : SKColors.Black,
                StrokeWidth = 1,
                IsAntialias = true
            };
            float startX = firstCostWord ? x : x - 7;
            canvas.DrawLine(startX, lineY, x + textPaint.MeasureText(word) - 4, lineY, linePaint);
            firstCostWord = false;
        }

        // Help mode entry
        if (word.StartsWith("("))
            helpMode = true;

        // Check for keyword highlights
        var remaining = word;
        float curX = x;

        // Process keyword highlights and icons from the word
        curX = TryDrawHighlight(canvas, ref remaining, curX, y, textPaint, "ACT", ActColor);
        curX = TryDrawHighlight(canvas, ref remaining, curX, y, textPaint, "AUTO", AutoColor);
        curX = TryDrawHighlight(canvas, ref remaining, curX, y, textPaint, "CONT", ContColor);
        curX = TryDrawHighlight(canvas, ref remaining, curX, y, textPaint, "1/Turn", TurnColor);
        curX = TryDrawHighlight(canvas, ref remaining, curX, y, textPaint, "2/Turn", TurnColor);
        curX = TryDrawHighlight(canvas, ref remaining, curX, y, textPaint, "3/Turn", TurnColor);
        curX = TryDrawHighlight(canvas, ref remaining, curX, y, textPaint, "4/Turn", TurnColor);
        curX = TryDrawHighlight(canvas, ref remaining, curX, y, textPaint, "5/Turn", TurnColor);
        curX = TryDrawHighlight(canvas, ref remaining, curX, y, textPaint, "Soulguard", SoulguardColor);

        // Icons
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, TextPreprocessor.RS_COST + "\u00A0", "Cost_line_spr");
        curX = TryDrawCostIcon(canvas, ref remaining, curX, y, textPaint, costPaint, TextPreprocessor.RS_CBLAST, "C_blast_line_spr", CBlastCircle);
        curX = TryDrawCostIcon(canvas, ref remaining, curX, y, textPaint, costPaint, TextPreprocessor.RS_CCHARGE, "C_charge_line_spr", CBlastCircle);
        curX = TryDrawCostIcon(canvas, ref remaining, curX, y, textPaint, costPaint, TextPreprocessor.RS_SBLAST, "S_blast_line_spr", SBlastCircle);
        curX = TryDrawCostIcon(canvas, ref remaining, curX, y, textPaint, costPaint, TextPreprocessor.RS_SCHARGE, "S_charge_line_spr", SBlastCircle);
        curX = TryDrawCostIcon(canvas, ref remaining, curX, y, textPaint, costPaint, TextPreprocessor.RS_EBLAST, "E_blast_line_spr", EBlastCircle);
        curX = TryDrawCostIcon(canvas, ref remaining, curX, y, textPaint, costPaint, TextPreprocessor.RS_ECHARGE, "E_charge_line_spr", EBlastCircle);
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, TextPreprocessor.RS_POWER.ToString(), "Pow_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, TextPreprocessor.RS_SHIELD.ToString(), "Sld_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, TextPreprocessor.RS_CRIT.ToString(), "Crit_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "t_0", "trigger_critical_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "t_1", "trigger_draw_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "t_2", "trigger_heal_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "t_3", "trigger_stand_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "t_4", "trigger_front_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "t_5", "trigger_over_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "SN", "Stand_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "RT", "Rest_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "VC", "VC_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "RC", "RC_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "GC", "GC_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "G_guardian", "G_guard_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "Ult_Stride_", "Ult_stride_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "Stride/", "Stride_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "LG|__20000", "Lgn_pow_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "LG|__21000", "Lgn_pow_line_spr_1");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "LG|__22000", "Lgn_pow_line_spr_2");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "LGN", "Lgn_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "LB_3", "LB_line_spr_2");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "LB_4", "LB_line_spr_3");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "LB_5", "LB_line_spr_4");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "GB_1", "GB_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "GB_2", "GB_line_spr_1");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "GB_3", "GB_line_spr_2");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "GB_4", "GB_line_spr_3");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "GB_8", "GB_line_spr_7");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, TextPreprocessor.RS_OSOUL.ToString(), "oversoul_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, TextPreprocessor.RS_ODRESS.ToString(), "overdress_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, TextPreprocessor.RS_DBOOST.ToString(), "dboost_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, TextPreprocessor.RS_MELODY.ToString(), "melody_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "Glitter_", "glitter_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "Volund|", "volund_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "RevolDress", "revoldress_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "XverDress", "xoverdress_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "RegalisPiece", "regalis_piece_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "[UnisonDress]", "unison_dress_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "DivineSkill", "divine_skill_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "[Malwyrm]", "malwyrm_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "[MateUnit]", "mate_unit_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "Mental_Pol", "mental_pollution_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "Ace__Unit", "ace_unit_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "UniqueSkill", "unique_skill_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "[_Deal_]", "deal_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "[Oversized!]", "oversized_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "DangerSkil", "dangerous_skill_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "ExploGrowth", "explosive_growth_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "[__Retry__", "retry_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "VersionUp", "version_up_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "[Rewrite]", "rewrite_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "ArmaArms", "arma_arms_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "HappyToys", "happy_toys_line_spr");
        curX = TryDrawIcon(canvas, ref remaining, curX, y, textPaint, "DLegion", "d_legion_line_spr");

        // Draw any remaining text
        if (!string.IsNullOrEmpty(remaining))
        {
            textPaint.Color = abilityMode ? AbilityModeColor : SKColors.Black;
            textPaint.Typeface = helpMode ? _fontItalic : _fontRegular;
            canvas.DrawText(remaining, curX, y + FontSize, textPaint);
        }
    }

    private float TryDrawHighlight(SKCanvas canvas, ref string word, float x, float y,
        SKPaint textPaint, string keyword, SKColor bgColor)
    {
        int pos = word.IndexOf(keyword, StringComparison.Ordinal);
        if (pos < 0) return x;

        // Draw text before keyword
        if (pos > 0)
        {
            var before = word.Substring(0, pos);
            canvas.DrawText(before, x, y + FontSize, textPaint);
            x += textPaint.MeasureText(before);
        }

        // Measure keyword
        float kwWidth = textPaint.MeasureText(keyword);

        // Draw colored background rectangle (rounded corners)
        using var bgPaint = new SKPaint { Color = bgColor, IsAntialias = true };
        canvas.DrawRoundRect(x, y + 1, kwWidth + 2, GetLineHeight(), 2, 2, bgPaint);

        // Draw keyword in white
        using var kwPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = FontSize,
            Typeface = _fontRegular,
            IsAntialias = true,
            SubpixelText = true,
            HintingLevel = SKPaintHinting.Slight
        };
        canvas.DrawText(keyword, x + 1, y + FontSize, kwPaint);
        x += kwWidth + 3;

        // Update remaining word
        word = word.Substring(pos + keyword.Length);
        return x;
    }

    private float TryDrawIcon(SKCanvas canvas, ref string word, float x, float y,
        SKPaint textPaint, string token, string iconName)
    {
        int pos = word.IndexOf(token, StringComparison.Ordinal);
        if (pos < 0) return x;

        // Draw text before icon
        if (pos > 0)
        {
            var before = word.Substring(0, pos);
            canvas.DrawText(before, x, y + FontSize, textPaint);
            x += textPaint.MeasureText(before);
        }

        // Draw icon
        if (_icons.TryGetValue(iconName, out var bitmap))
        {
            float drawWidth = bitmap.Width;
            float drawHeight = bitmap.Height;
            canvas.DrawBitmap(bitmap, SKRect.Create(x, y, drawWidth, drawHeight));
            x += drawWidth;
        }

        // Update remaining word
        word = word.Substring(pos + token.Length);
        return x;
    }

    private float TryDrawCostIcon(SKCanvas canvas, ref string word, float x, float y,
        SKPaint textPaint, SKPaint costPaint, string token, string iconName, SKColor circleColor)
    {
        int pos = word.IndexOf(token, StringComparison.Ordinal);
        if (pos < 0) return x;

        // Draw text before icon
        if (pos > 0)
        {
            var before = word.Substring(0, pos);
            canvas.DrawText(before, x, y + FontSize, textPaint);
            x += textPaint.MeasureText(before);
        }

        // Draw the cost icon sprite
        if (_icons.TryGetValue(iconName, out var bitmap))
        {
            float drawWidth = bitmap.Width;
            float drawHeight = bitmap.Height;
            canvas.DrawBitmap(bitmap, SKRect.Create(x, y, drawWidth, drawHeight));
            x += drawWidth;
        }

        // Extract and draw the number after em-dash
        var rest = word.Substring(pos + token.Length);
        if (rest.StartsWith("\u2014"))
        {
            rest = rest.Substring(1);
            string digits = "";
            while (rest.Length > 0 && char.IsDigit(rest[0]))
            {
                digits += rest[0];
                rest = rest.Substring(1);
            }

            if (digits.Length > 0)
            {
                float circleSize = GetLineHeight() - 2;
                using var circlePaint = new SKPaint { Color = circleColor, IsAntialias = true };
                canvas.DrawOval(x + circleSize / 2 + 2, y + GetLineHeight() / 2, circleSize / 2, circleSize / 2, circlePaint);

                costPaint.TextAlign = SKTextAlign.Center;
                canvas.DrawText(digits, x + circleSize / 2 + 2, y + FontSize - 1, costPaint);
                costPaint.TextAlign = SKTextAlign.Left;
                x += circleSize + 4;
            }
        }

        word = rest;
        return x;
    }

    private float MeasureWord(string word, SKPaint paint)
    {
        // Token strings in TextPreprocessor are chosen so their pixel width in
        // Georgia Pro matches the corresponding icon sprite width (same approach
        // as the GML client's string_width), so plain MeasureText is correct.
        return paint.MeasureText(word);
    }

    private float GetLineHeight() => 16f;

    /// <summary>
    /// In GameMaker 8.1, # is a line break and \# escapes it to a literal #.
    /// </summary>
    private static string UnescapeHash(string s) => s.Replace("\\#", "#");

    private static int CountChar(string s, char c)
    {
        int count = 0;
        foreach (var ch in s)
            if (ch == c) count++;
        return count;
    }
}
