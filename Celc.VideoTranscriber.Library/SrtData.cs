namespace Celc.VideoTranscriber.Library;

public class SrtData
{
    public List<SrtSegment> Segments { get; set; } = new List<SrtSegment>();

    public void SaveTo(string outputSrtPath)
    {
        using (StreamWriter writer = new StreamWriter(outputSrtPath))
            foreach (var segment in Segments)
            {
                writer.WriteLine(segment.Index);
                writer.WriteLine($"{FormatTime(segment.Start)} --> {FormatTime(segment.End)}");
                writer.WriteLine(segment.Text);
                writer.WriteLine();
            }
    }

    string FormatTime(TimeSpan time)
    {
        return string.Format("{0:D2}:{1:D2}:{2:D2},{3:D3}",
            time.Hours, time.Minutes, time.Seconds, time.Milliseconds);
    }
}