namespace CfaDatabaseEditor.Models;

public class CardFile
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public List<Card> Cards { get; set; } = new();
    public List<string> RawLines { get; set; } = new();
    public bool IsModified { get; set; }
}
