using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace CfaDatabaseEditor.Models;

public class SyncResult : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static readonly Encoding Win1251 = Encoding.GetEncoding(1251);

    private string _enName = string.Empty;
    public string EnName
    {
        get => _enName;
        set
        {
            if (_enName == value) return;
            _enName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasEncodingIssue));
            OnPropertyChanged(nameof(EncodingIssueDetails));
        }
    }

    public string EnCardNo { get; set; } = string.Empty;
    public string EnImageUrl { get; set; } = string.Empty;
    public byte[]? EnImageData { get; set; }

    private Card? _matchedCard;
    public Card? MatchedCard
    {
        get => _matchedCard;
        set { if (_matchedCard == value) return; _matchedCard = value; OnPropertyChanged(); OnPropertyChanged(nameof(MatchedCardStat)); }
    }

    private double _confidence;
    public double Confidence
    {
        get => _confidence;
        set { if (_confidence == value) return; _confidence = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Editable CFA ID. Setting this fires a property change so the ViewModel
    /// can look up the corresponding card and update MatchedCard.
    /// </summary>
    private int? _matchedCardStat;
    public int? MatchedCardStat
    {
        get => _matchedCardStat ?? _matchedCard?.CardStat;
        set
        {
            if (_matchedCardStat == value) return;
            _matchedCardStat = value;
            OnPropertyChanged();
        }
    }

    private bool _isApproved;
    public bool IsApproved
    {
        get => _isApproved;
        set { if (_isApproved == value) return; _isApproved = value; OnPropertyChanged(); }
    }

    private bool _isApplied;
    public bool IsApplied
    {
        get => _isApplied;
        set { if (_isApplied == value) return; _isApplied = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// True if the name contains characters that cannot be represented in Windows-1251.
    /// </summary>
    public bool HasEncodingIssue => !string.IsNullOrEmpty(_enName) && !CanEncodeWin1251(_enName);

    /// <summary>
    /// Lists the specific problematic characters for the tooltip.
    /// </summary>
    public string EncodingIssueDetails
    {
        get
        {
            if (!HasEncodingIssue) return string.Empty;
            var bad = new List<char>();
            foreach (char c in _enName)
            {
                var bytes = Win1251.GetBytes(new[] { c });
                var roundtrip = Win1251.GetString(bytes);
                if (roundtrip[0] != c && !bad.Contains(c))
                    bad.Add(c);
            }
            return $"Characters not in Windows-1251: {string.Join(", ", bad.Select(c => $"'{c}' (U+{(int)c:X4})"))}";
        }
    }

    private static bool CanEncodeWin1251(string text)
    {
        var bytes = Win1251.GetBytes(text);
        var roundtrip = Win1251.GetString(bytes);
        return roundtrip == text;
    }
}
