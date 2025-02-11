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
        private const string LineDelimiter = "<<<END_OF_LINE>>>";

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
                    // Budujemy tekst wejściowy z napisów od RequestStart do RequestEnd.
                    StringBuilder inputBuilder = new StringBuilder();
                    for (int i = batch.RequestStart; i <= batch.RequestEnd; i++)
                    {
                        inputBuilder.Append(segments[i].Text.Replace("\r\n", ", ").Replace("\n", ", "))
                            .Append(LineDelimiter);
                    }

                    string batchInput = inputBuilder.ToString();

                    // Generujemy prompt w języku angielskim z informacją o separatorze.
                    string prompt =
                        $"Please translate the following subtitles from {sourceLanguage} to {destLanguage}.\n" +
                        "Each line in the input represents a separate subtitle.\n" +
                        $"For each line, output the translated text followed immediately by the delimiter {LineDelimiter}.\n" +
                        "Do not add any extra text, numbering, or commentary.\n" +
                        "If an input line is empty, output only the delimiter.\n" +
                        "Ensure that the total number of delimiters in the output exactly matches the total number of input lines.";

                    // Wywołanie API – Deepseek lub OpenAI.
                    var threadIndex = batch.Index % threadsCount;
                    string response;
                    if (GetApiKeys(openAiApiKey).Count > 0)
                    {
                        response = await new OpenAiHelper().ExecutePrompt(GetApiKeys(openAiApiKey)[threadIndex], gptModel, prompt, batchInput);
                    }
                    else if (GetApiKeys(deepseekApiKey).Count > 0)
                    {
                        response = await new DeepseekHelper().ExecutePrompt(GetApiKeys(deepseekApiKey)[threadIndex], prompt, batchInput);
                    }
                    else
                    {
                        response = batchInput;
                    }

                    // Rozbijamy odpowiedź na linie.
                    var responseLines = response
                        .Split(new[] { LineDelimiter, "\r\n", "\n" }, StringSplitOptions.None)
                        .Select(line => line.Trim().TrimEnd('.'))
                        .ToList();

                    // Obliczamy offset oraz liczbę efektywnych napisów.
                    int offset = batch.EffectiveStart - batch.RequestStart;
                    int effectiveCount = batch.EffectiveEnd - batch.EffectiveStart + 1;
                    if (offset + effectiveCount > responseLines.Count)
                    {
                        throw new Exception(
                            $"Expected at least {offset + effectiveCount} translation lines, but received only {responseLines.Count}.");
                    }

                    // Przypisujemy tłumaczenia do odpowiednich indeksów.
                    for (int i = 0; i < effectiveCount; i++)
                    {
                        int segmentIndex = batch.EffectiveStart + i;
                        translations[segmentIndex] = responseLines[offset + i];
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
                translatedData.Segments.Add(new SrtSegment
                {
                    Index = original.Index,
                    Start = original.Start,
                    End = original.End,
                    Text = twoLanguages ? original.Text + "\n-----\n" + translatedText : translatedText
                });
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
}
