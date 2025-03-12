using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CeLC.VideoTranscriber.Library
{
    public class SrtData
    {
        public List<SrtSegment> Segments { get; set; } = new List<SrtSegment>();

        public Boolean ContainsOriginalText =>
            Segments.Any(s => !string.IsNullOrEmpty(s.TextOriginal));

        public void SaveTo(string outputSrtPath, bool saveOriginalText = true)
        {
            using (StreamWriter writer = new StreamWriter(outputSrtPath))
            {
                foreach (var segment in Segments)
                {
                    writer.WriteLine(segment.Index);
                    writer.WriteLine($"{FormatTime(segment.Start)} --> {FormatTime(segment.End)}");
                    if (saveOriginalText && (!string.IsNullOrEmpty(segment.TextOriginal)))
                    {
                        writer.WriteLine(segment.TextOriginal);
                        writer.WriteLine("----");
                    }

                    writer.WriteLine(segment.Text);
                    writer.WriteLine();
                }
            }
        }

        private string FormatTime(TimeSpan time)
        {
            return string.Format("{0:D2}:{1:D2}:{2:D2},{3:D3}",
                time.Hours, time.Minutes, time.Seconds, time.Milliseconds);
        }

        private TimeSpan ParseTime(string timeStr)
        {
            return TimeSpan.ParseExact(timeStr, @"hh\:mm\:ss\,fff", null);
        }

        public void LoadFrom(string inputSrtPath)
        {
            if (!File.Exists(inputSrtPath))
            {
                throw new Exception("Plik z napisami nie został znaleziony.");
            }

            string content = File.ReadAllText(inputSrtPath);
            var segmentsBlocks =
                content.Split(new string[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var block in segmentsBlocks)
            {
                var lines = block.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (lines.Length < 2) continue;
                if (!int.TryParse(lines[0].Trim(), out int index))
                    continue;
                var timeParts = lines[1].Split(new string[] { " --> " }, StringSplitOptions.None);
                if (timeParts.Length != 2)
                    continue;
                TimeSpan start, end;
                try
                {
                    start = ParseTime(timeParts[0].Trim());
                    end = ParseTime(timeParts[1].Trim());
                }
                catch (Exception ex)
                {
                    throw new Exception($"Błąd podczas parsowania czasu: {ex.Message}");
                }

                string textOriginal = null;
                string text = null;
                if (lines.Length >= 3)
                {
                    if (lines.Length >= 4 && !string.IsNullOrWhiteSpace(lines[2]) && lines[3].Trim() == "----")
                    {
                        textOriginal = lines[2];
                        if (lines.Length > 4)
                        {
                            text = string.Join(Environment.NewLine, lines.Skip(4));
                        }
                    }
                    else
                    {
                        text = string.Join(Environment.NewLine, lines.Skip(2));
                    }
                }

                var segment = SrtSegment.From(text, start, end, index);
                if (!string.IsNullOrEmpty(textOriginal))
                    segment.TextOriginal = textOriginal;
                Segments.Add(segment);
            }
        }
    }
}
