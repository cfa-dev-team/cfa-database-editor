using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CfaDatabaseEditor.Models;

namespace CfaDatabaseEditor.Converters;

public class NationIdToColorConverter : IValueConverter
{
    public static readonly NationIdToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int nationId)
        {
            var nation = ClanRegistry.GetNationById(nationId);
            if (nation != null)
                return new SolidColorBrush(nation.DisplayColor);
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class NullableIntToStringConverter : IValueConverter
{
    public static readonly NullableIntToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i) return i.ToString();
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && int.TryParse(s, out var i)) return i;
        if (string.IsNullOrWhiteSpace(value as string)) return null;
        return null;
    }
}

public class BoolToStringConverter : IValueConverter
{
    public static readonly BoolToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "Yes" : "No";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class CardToColorBrushConverter : IValueConverter
{
    public static readonly CardToColorBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CfaDatabaseEditor.Models.Card card)
        {
            // Check clan first - it's more specific (but skip Orders, which is just a filter tag)
            if (card.CardInClan.HasValue && card.CardInClan.Value > 0
                && card.CardInClan.Value != CfaDatabaseEditor.Models.ClanRegistry.OrderFilter.Id)
            {
                var clan = CfaDatabaseEditor.Models.ClanRegistry.GetClanById(card.CardInClan.Value);
                if (clan != null)
                    return new SolidColorBrush(clan.DisplayColor);
            }

            // Fall back to nation
            if (card.DCards > 0)
            {
                var nation = CfaDatabaseEditor.Models.ClanRegistry.GetNationById(card.DCards);
                if (nation != null)
                    return new SolidColorBrush(nation.DisplayColor);
            }
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ColorToBrushConverter : IValueConverter
{
    public static readonly ColorToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color c)
            return new SolidColorBrush(c);
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
