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

    public Card? MatchedCard { get; set; }
    public double Confidence { get; set; }

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
