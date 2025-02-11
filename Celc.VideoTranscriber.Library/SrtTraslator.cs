namespace Celc.VideoTranscriber.Library;

public class SrtTraslator
{
    class TranslateJob
    {
        public string Text { get; set; }
        public SrtSegment Segment { get; set; }
    }

    public async Task<SrtInfo> TranslateSrt(
        SrtInfo srtInput,
        string sourceLanguage,
        string destLanguage,
        bool twoLanguages = false,
        string deepseekApiKey = null,
        string openAiApiKey = null)
    {
        var outputSrtPath = System.IO.Path.Combine(
            Path.GetDirectoryName(srtInput.SrtPath),
            twoLanguages
                ? $"{Path.GetFileNameWithoutExtension(srtInput.SrtPath)}-{sourceLanguage}-{destLanguage}.srt"
                : $"{Path.GetFileNameWithoutExtension(srtInput.SrtPath)}-{destLanguage}.srt");

        var srtData = new SrtData();

        var back = 3;
        var next = 3;
        var i = 0;
        var segments = srtInput.SrtData.Segments;

        var itemsToTranslate = new List<TranslateJob>();
        foreach (var segment in segments)
        {
            var textToTranslate = "";
            for (var j = Math.Max(0, i - back); j < Math.Min(segments.Count, i + next); j++)
                textToTranslate += segments[j].Index + "\n" + segments[j].Text + "\n\n";

            itemsToTranslate.Add(new TranslateJob()
            {
                Text = textToTranslate,
                Segment = segment
            });
            i++;
        }

        // zapełnienie cache
        await Parallel.ForEachAsync(itemsToTranslate, async (itemToTranslate, ct) =>
        {
            await TranslateSrtSegment(sourceLanguage, destLanguage, deepseekApiKey, openAiApiKey, itemToTranslate);
        });

        foreach (var itemToTranslate in itemsToTranslate)
        {
            srtData.Segments.Add(new SrtSegment
            {
                Index = itemToTranslate.Segment.Index,
                Start = itemToTranslate.Segment.Start,
                End = itemToTranslate.Segment.End,
                Text = twoLanguages
                    ? itemToTranslate.Segment.Text +
                      "\n-----\n" +
                      await TranslateSrtSegment(sourceLanguage, destLanguage, deepseekApiKey, openAiApiKey, itemToTranslate)
                    : await TranslateSrtSegment(sourceLanguage, destLanguage, deepseekApiKey, openAiApiKey, itemToTranslate)
            });
        }

        srtData.SaveTo(outputSrtPath);

        return new SrtInfo
        {
            SrtData = srtData,
            SrtPath = outputSrtPath,
            SrtTitle = srtInput.SrtTitle,
        };
    }

    private static async Task<string> TranslateSrtSegment(
        string sourceLanguage,
        string destLanguage,
        string deepseekApiKey,
        string openAiApiKey,
        TranslateJob itemToTranslate)
    {
        if (!string.IsNullOrEmpty(openAiApiKey))
            return await TranslateOpenAi(openAiApiKey, sourceLanguage, destLanguage, itemToTranslate.Segment.Index,
                itemToTranslate.Text);
        if (!string.IsNullOrEmpty(deepseekApiKey))
            return await TranslateDeepseek(deepseekApiKey, sourceLanguage, destLanguage, itemToTranslate.Segment.Index,
                itemToTranslate.Text);
        return itemToTranslate.Segment.Text;
    }

    private static async Task<string> TranslateOpenAi(string apiKey, string sourceLanguage, string destLanguage,
        int segmentIndex, string toTranslate)
    {
        return await new OpenAiHelper().ExecutePrompt(
            apiKey,
            $"You are a subtitle translation assistant that converts {sourceLanguage} subtitles to {destLanguage}. " +
            $"Your input consists of multiple SRT blocks, each formatted as follows:" +
            $"\n\n<line number>\n<subtitle text>\n\nTranslate line {segmentIndex}, use the context from all provided " +
            $"lines to ensure a high-quality and consistent translation, but only output the translation " +
            $"of that specific line. Do not include any extra commentary or formatting—just the translated text.",
            toTranslate);
    }

    private static async Task<string> TranslateDeepseek(string apiKey, string sourceLanguage, string destLanguage,
        int segmentIndex, string toTranslate)
    {
        return await new DeepseekHelper().ExecutePrompt(
            apiKey,
            $"You are a subtitle translation assistant that converts {sourceLanguage} subtitles to {destLanguage}. " +
            $"Your input consists of multiple SRT blocks, each formatted as follows:" +
            $"\n\n<line number>\n<subtitle text>\n\nTranslate line {segmentIndex}, use the context from all provided " +
            $"lines to ensure a high-quality and consistent translation, but only output the translation " +
            $"of that specific line. Do not include any extra commentary or formatting—just the translated text.",
            toTranslate);
    }
}
