namespace Celc.VideoTranscriber.Library;

public class SrtSegment
{
    public string Text { get; set; }
    public TimeSpan End { get; set; }
    public TimeSpan Start { get; set; }
    public int Index { get; set; }
}