using CeLC.VideoTranscriber.Library;

public class SrtMerger
{
    /// <summary>
    /// Scala fragmenty napisów, gdy:
    /// 1. Odstęp między końcem jednego segmentu a początkiem kolejnego jest mniejszy lub równy mergeThreshold.
    /// 2. Łączna długość tekstu bieżącego segmentu i następnego nie przekracza maxLength.
    /// </summary>
    /// <param name="srtData">Obiekt zawierający segmenty napisów.</param>
    /// <param name="mergeThreshold">Próg scalania (np. 6 sekund).</param>
    /// <param name="maxLength">Maksymalna łączna długość tekstu segmentu, domyślnie 100.</param>
    /// <returns>Nowy obiekt SrtData z połączonymi segmentami.</returns>
    public static SrtData MergeCloseSegments(SrtData srtData, TimeSpan mergeThreshold, int maxLength = 100)
    {
        if (srtData == null || srtData.Segments == null || srtData.Segments.Count == 0)
            return srtData;

        // Upewniamy się, że segmenty są posortowane według czasu rozpoczęcia
        var sortedSegments = srtData.Segments.OrderBy(s => s.Start).ToList();
        var mergedSegments = new List<SrtSegment>();

        // Inicjujemy pierwszy scalany segment
        SrtSegment current = SrtSegment.From(
            sortedSegments[0].Text,
            sortedSegments[0].Start,
            sortedSegments[0].End,
            sortedSegments[0].Index);

        // Iterujemy po kolejnych segmentach
        for (int i = 1; i < sortedSegments.Count; i++)
        {
            var next = sortedSegments[i];
            // Obliczamy przerwę między końcem bieżącego segmentu a początkiem następnego
            TimeSpan gap = next.Start - current.End;

            // Sprawdzamy, czy możemy scalić segmenty:
            // 1. Odstęp czasowy jest mniejszy lub równy mergeThreshold.
            // 2. Łączna długość tekstu nie przekracza maxLength.
            if (gap <= mergeThreshold && (current.Text.Length + next.Text.Length) <= maxLength)
            {
                // Scalanie tekstu (dodajemy nową linię pomiędzy fragmentami)
                //current.Text += Environment.NewLine + next.Text;
                current.Text = current.Text.Trim()  + " " + next.Text.Trim();
                // Aktualizujemy czas zakończenia scalonego segmentu
                current.End = next.End;
            }
            else
            {
                // Dodajemy bieżący segment do wyniku i rozpoczynamy nowy segment
                mergedSegments.Add(current);
                current = SrtSegment.From(
                    next.Text,
                    next.Start,
                    next.End,
                    next.Index);
            }
        }
        // Dodajemy ostatni segment
        mergedSegments.Add(current);

        // Opcjonalnie: przeliczamy indeksy, żeby były spójne
        for (int i = 0; i < mergedSegments.Count; i++)
        {
            mergedSegments[i].Index = i + 1;
        }

        return new SrtData { Segments = mergedSegments };
    }
}
