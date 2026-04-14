using System.Text;

namespace CfaDatabaseEditor.Models;

public class CardFile
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public List<Card> Cards { get; set; } = new();
    public List<string> RawLines { get; set; } = new();
    public bool IsModified { get; set; }

    /// <summary>
    /// The encoding used to read/write this file.
    /// Built-in files use UTF-8, custom files depend on CustomFactionUTF8 flag.
    /// </summary>
    public Encoding FileEncoding { get; set; } = Encoding.UTF8;
}
