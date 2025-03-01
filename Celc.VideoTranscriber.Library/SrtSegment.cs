namespace CeLC.VideoTranscriber.Library;

public class SrtSegment
{
    public string Text { get; set; }
    public TimeSpan End { get; set; }
    public TimeSpan Start { get; set; }
    public int Index { get; set; }
    public string Text2 { get; set; }

    public static SrtSegment From(string text, TimeSpan start, TimeSpan end, int index)
    {
        text = ProcessText(text);
        return new SrtSegment
        {
            Text = text,
            Text2 = "",
            Start = start,
            End = end,
            Index = index
        };
    }

    public static SrtSegment From(string text, string text2, TimeSpan start, TimeSpan end, int index)
    {
        text = ProcessText(text);
        text2 = ProcessText(text2);
        return new SrtSegment
        {
            Text = text,
            Text2 = text2,
            Start = start,
            End = end,
            Index = index
        };
    }

    private static string ProcessText(string text)
    {
        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .ToArray();

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmedLine = lines[i].Trim();
            sb.Append(trimmedLine);
            if (i < lines.Length - 1)
            {
                // Dodajemy separator, tylko jeśli linia nie kończy się na kropkę lub przecinek
                if (!(trimmedLine.EndsWith(".") || trimmedLine.EndsWith(",")))
                {
                    sb.Append(", ");
                }
                else
                {
                    sb.Append(" ");
                }
            }
        }
        return sb.ToString();
    }
}
