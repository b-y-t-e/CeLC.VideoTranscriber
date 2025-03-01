using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CeLC.VideoTranscriber.Library
{
    /// <summary>
    /// Klasa odpowiedzialna za tłumaczenie napisów SRT z podziałem na partie (batch),
    /// dodając margines kontekstowy, aby uniknąć "ucięcia" kontekstu oraz scalanie wyników.
    /// Dodatkowo umożliwia sparametryzowanie tłumaczenia (sourceLanguage -> targetLanguage)
    /// oraz wysyła prompt w języku angielskim w odpowiednim formacie.
    /// </summary>
    public class SrtTranslator
    {
        public SrtTranslator()
        {
        }

        /// <summary>
        /// Informacje o partii – zakres indeksów, który wysyłamy do API oraz zakres efektywny, który wykorzystujemy.
        /// </summary>
        private class BatchInfo
        {
            public int Index { get; set; }
            public int RequestStart { get; set; } // indeks początkowy w oryginalnej liście, który wysyłamy
            public int RequestEnd { get; set; } // indeks końcowy (włącznie) w żądaniu

            public int
                EffectiveStart { get; set; } // indeks pierwszego napisu, którego tłumaczenie z wyniku wykorzystujemy

            public int EffectiveEnd { get; set; } // indeks ostatniego napisu, którego tłumaczenie wykorzystujemy
        }

        /// <summary>
        /// Tłumaczy cały obiekt SrtData, dzieląc go na partie tak, aby nie przekroczyć limitu napisów wysyłanych w jednym żądaniu.
        /// Parametry:
        ///   - srtInput: dane napisów,
        ///   - sourceLanguage: język źródłowy,
        ///   - destLanguage: język docelowy,
        ///   - maxBatchSize: maksymalna liczba napisów wysyłana w jednym żądaniu (w tym marginesy),
        ///   - margin: liczba napisów jako margines kontekstowy.
        /// </summary>
        public async Task<SrtInfo> TranslateSrt(
            SrtInfo srtInput,
            string sourceLanguage,
            string destLanguage,
            string gptModel = "GPT4o",
            int maxBatchSize = 100,
            int margin = 3,
            Action<int, int>? progress = null,
            string deepseekApiKey = null,
            string openAiApiKey = null,
            bool twoLanguages = false)
        {
            var outputSrtPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(srtInput.SrtPath),
                twoLanguages
                    ? $"{System.IO.Path.GetFileNameWithoutExtension(srtInput.SrtPath)}-{sourceLanguage}-{destLanguage}.srt"
                    : $"{System.IO.Path.GetFileNameWithoutExtension(srtInput.SrtPath)}-{destLanguage}.srt");

            maxBatchSize = 75;

            var srtData = srtInput.SrtData;
            var segments = srtData.Segments;
            int total = segments.Count;
            if (total == 0)
            {
                srtData.SaveTo(outputSrtPath);
                return new SrtInfo
                {
                    SrtData = srtData,
                    SrtPath = outputSrtPath,
                    SrtTitle = srtInput.SrtTitle,
                };
            }

            // Przygotowujemy listę partii.
            List<BatchInfo> batches = new List<BatchInfo>();

            var batchIndex = 0;
            if (total <= maxBatchSize)
            {
                batches.Add(new BatchInfo
                {
                    RequestStart = 0,
                    RequestEnd = total - 1,
                    EffectiveStart = 0,
                    EffectiveEnd = total - 1,
                    Index = batchIndex++
                });
            }
            else
            {
                // Pierwsza partia – brak marginesu z lewej strony.
                int firstEffectiveSize = maxBatchSize - margin;
                int middleEffectiveSize = maxBatchSize - 2 * margin;

                int currentEffectiveStart = 0;
                // PARTIA PIERWSZA
                int firstEffectiveEnd = currentEffectiveStart + firstEffectiveSize - 1;
                if (firstEffectiveEnd > total - 1)
                    firstEffectiveEnd = total - 1;
                int firstRequestEnd = (firstEffectiveEnd < total - 1) ? firstEffectiveEnd + margin : firstEffectiveEnd;
                batches.Add(new BatchInfo
                {
                    RequestStart = 0,
                    RequestEnd = firstRequestEnd,
                    EffectiveStart = currentEffectiveStart,
                    EffectiveEnd = firstEffectiveEnd,
                    Index = batchIndex++
                });
                currentEffectiveStart = firstEffectiveEnd + 1;

                // PARTIE ŚRODKOWE
                while (currentEffectiveStart + middleEffectiveSize <= total - 1)
                {
                    int effectiveEnd = currentEffectiveStart + middleEffectiveSize - 1;
                    int requestStart = currentEffectiveStart - margin;
                    int requestEnd = effectiveEnd + margin;
                    batches.Add(new BatchInfo
                    {
                        RequestStart = requestStart,
                        RequestEnd = requestEnd,
                        EffectiveStart = currentEffectiveStart,
                        EffectiveEnd = effectiveEnd,
                        Index = batchIndex++
                    });
                    currentEffectiveStart = effectiveEnd + 1;
                }

                // PARTIA OSTATNIA (jeśli zostały jakieś napisy)
                if (currentEffectiveStart < total)
                {
                    int requestStart = currentEffectiveStart - margin;
                    if (requestStart < 0)
                        requestStart = 0;
                    int requestEnd = total - 1;
                    batches.Add(new BatchInfo
                    {
                        RequestStart = requestStart,
                        RequestEnd = requestEnd,
                        EffectiveStart = currentEffectiveStart,
                        EffectiveEnd = total - 1,
                        Index = batchIndex++
                    });
                }
            }

            // Współbieżny słownik do przechowywania tłumaczeń: klucz = oryginalny indeks napisu.
            ConcurrentDictionary<int, string> translations = new ConcurrentDictionary<int, string>();

            // Licznik ukończonych partii dla progress.
            int processedBatches = 0;

            // Przetwarzamy partie równolegle.
            var threadsCount = GetThreadsCount(openAiApiKey, deepseekApiKey);
            await Parallel.ForEachAsync(
                batches,
                new ParallelOptions() { MaxDegreeOfParallelism = threadsCount },
                async (batch, cancellationToken) =>
                {
                    var separator = "</END_OF_LINE>";
                    var srtLines = new SrtLines();

                    // Budujemy tekst wejściowy z napisów od RequestStart do RequestEnd.
                    for (int i = batch.RequestStart; i <= batch.RequestEnd; i++)
                        srtLines.Add(segments[i].Text);

                    // Generujemy prompt w języku angielskim z informacją o separatorze.
                    string prompt =
                        $"Please translate the following subtitles from {sourceLanguage} to {destLanguage}. " +
                        $"Each input line in the input represents a separate subtitle. " +
                        $"For each line, follow these rules exactly:\n" +
                        $"1. If the line is non-empty, output the translated text immediately followed by the delimiter {separator} with no additional spaces or characters.\n" +
                        $"2. If the input line is empty, output only the delimiter.\n" +
                        $"3. Ensure that exactly one delimiter is output per input line so that the total number of {separator} delimiters exactly equals the number of input lines.\n" +
                        //$"4. 4. For any repeated lines, ensure that each occurrence is translated individually and that the order, repetition, and count of lines are preserved exactly as in the input.\n"+
                        $"4. Do not output any extra delimiters, text, numbering, or commentary.\n" +
                        $"5. Maintain the original style of expression as much as possible, and note that some fragments may pertain to biblical context; handle such references with appropriate care and terminology.\n" +
                        $"6. Avoid literal translations of idiomatic expressions.\n";

                    var batchInput = srtLines.ToCompressedString(separator);

                    // Wywołanie API – Deepseek lub OpenAI.
                    var threadIndex = batch.Index % threadsCount;
                    string response;
                    if (GetApiKeys(openAiApiKey).Count > 0)
                    {
                        response = await new OpenAiHelper()
                            .ExecutePrompt(
                                GetApiKeys(openAiApiKey)[threadIndex],
                                gptModel,
                                prompt,
                                batchInput);
                    }
                    else if (GetApiKeys(deepseekApiKey).Count > 0)
                    {
                        response = await new DeepseekHelper()
                            .ExecutePrompt(
                                GetApiKeys(deepseekApiKey)[threadIndex],
                                prompt,
                                batchInput);
                    }
                    else
                    {
                        response = batchInput;
                    }

                    // Rozbijamy odpowiedź na linie.
                    /*var responseLines = response
                        .Split(new[] { LineDelimiter, "\r\n", "\n" }, StringSplitOptions.None)
                        .Select(line => line.Trim().TrimEnd('.'))
                        .ToList();

                    while (responseLines.LastOrDefault() == "")
                        responseLines.RemoveAt(responseLines.Count - 1);*/

                    srtLines.Apply(response, separator);

                    // Obliczamy offset oraz liczbę efektywnych napisów.
                    int offset = batch.EffectiveStart - batch.RequestStart;
                    int effectiveCount = batch.EffectiveEnd - batch.EffectiveStart + 1;
                    /*if (offset + effectiveCount > responseLines.Count)
                    {
                        throw new Exception(
                            $"Expected at least {offset + effectiveCount} translation lines, but received only {responseLines.Count}.");
                    }*/

                    // Przypisujemy tłumaczenia do odpowiednich indeksów.
                    var outputLines = srtLines.ToList();
                    for (int i = 0; i < effectiveCount; i++)
                    {
                        int segmentIndex = batch.EffectiveStart + i;
                        translations[segmentIndex] = outputLines[offset + i];
                    }

                    // Aktualizujemy progress – używamy Interlocked aby zapewnić poprawność w środowisku wielowątkowym.
                    if (progress != null)
                    {
                        int current = Interlocked.Increment(ref processedBatches);
                        progress(current, batches.Count);
                    }
                });

            // Łączymy przetłumaczone napisy w nowy obiekt SrtData.
            SrtData translatedData = new SrtData();
            for (int i = 0; i < total; i++)
            {
                SrtSegment original = segments[i];
                string translatedText = translations.ContainsKey(i) ? translations[i] : original.Text;
                translatedData.Segments.Add(
                    SrtSegment.From(
                        translatedText,
                        twoLanguages ? original.Text : "",
                        original.Start,
                        original.End,
                        original.Index));
            }

            translatedData.SaveTo(outputSrtPath);

            return new SrtInfo
            {
                SrtData = translatedData,
                SrtPath = outputSrtPath,
                SrtTitle = srtInput.SrtTitle,
            };
        }

        private IList<string> GetApiKeys(string deepseekApiKey)
        {
            return
                $"{deepseekApiKey}"
                    .Split(';')
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();
        }

        private int GetThreadsCount(string openAiApiKey, string deepseekApiKey)
        {
            if (GetApiKeys(openAiApiKey).Count > 0)
                return GetApiKeys(openAiApiKey).Count;

            if (GetApiKeys(deepseekApiKey).Count > 0)
                return GetApiKeys(deepseekApiKey).Count;

            return 1;
        }
    }

    class SrtLines
    {
        private List<SrtLine> _lines;

        public SrtLines()
        {
            _lines = new List<SrtLine>();
        }

        public void Add(String text)
        {
            var lastLine = _lines.LastOrDefault();
            if (lastLine != null && lastLine.Text.Trim() == text.Trim())
                lastLine.Repeated++;
            else
                _lines.Add(new SrtLine { Text = text.Trim() });
        }

        public void Apply(string text, params string[]? separators)
        {
            if (separators == null)
                separators = new[] { "\r\n", "\n" };

            // Podział tekstu na wiersze i usunięcie pustych wierszy z końca
            var linesArray = text.Split(separators, StringSplitOptions.None);
            int countInput = linesArray.Length;
            while (countInput > 0 && string.IsNullOrEmpty(linesArray[countInput - 1]))
            {
                countInput--;
            }

            var bbbLines = SrtLines.FromString(text, separators);
            var bbb = bbbLines.ToUncompressedString("\r\n");
            bbb = bbb;

            var aaa = this.ToUncompressedString("\r\n");
            aaa = aaa;

            // Usunięcie pustych wierszy z końca _lines
            int countCurrent = _lines.Count;
            while (countCurrent > 0 && string.IsNullOrEmpty(_lines[countCurrent - 1].Text))
            {
                countCurrent--;
            }

            if (countInput != countCurrent)
                throw new Exception("Invalid number of lines");

            for (int i = 0; i < countInput; i++)
                _lines[i].Text = linesArray[i].Trim();
        }

        public String ToUncompressedString(String? separator)
        {
            if (separator == null)
                separator = "\r\n";

            var result = new StringBuilder();
            foreach (var srtLine in _lines)
            {
                for (int i = 0; i < srtLine.Repeated; i++)
                    result.Append(srtLine.Text).Append(separator);
            }

            return result.ToString();
        }


        public String ToCompressedString(String? separator)
        {
            if (separator == null)
                separator = "\r\n";

            // Ustalenie liczby niepustych wierszy na końcu
            int count = _lines.Count;
            while (count > 0 && string.IsNullOrEmpty(_lines[count - 1].Text))
            {
                count--;
            }

            // Pobranie tylko tych wierszy, które nie są puste na końcu
            var linesToJoin = _lines.Take(count).Select(x => x.Text);
            return string.Join(separator, linesToJoin);
        }

        public List<String> ToList()
        {
            var result = new List<String>();
            foreach (var srtLine in _lines)
            {
                for (int i = 0; i < srtLine.Repeated; i++)
                    result.Add(srtLine.Text);
            }

            return result;
        }

        public static SrtLines FromString(String text, params String[]? separators)
        {
            if (separators == null)
                separators = new[] { "\r\n", "\n" };

            var lines = new SrtLines();
            var linesArray = text.Split(separators, StringSplitOptions.None);
            foreach (var line in linesArray)
                lines.Add(line);
            return lines;
        }

        class SrtLine
        {
            public string Text { get; set; } = "";
            public int Repeated { get; set; } = 1;

            public override string ToString()
            {
                return $"'{Text}' * {Repeated}";
            }
        }
    }
}
