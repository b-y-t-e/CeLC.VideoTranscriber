using Whisper.net;
using Whisper.net.Ggml;

namespace Celc.VideoTranscriber.Library;

public class AudioTranscriber
{
    public async Task<SrtInfo> TranscribeAudioToSrt(AudioInfo audio, string language, string whisperModel)
    {
        var type = (GgmlType)Enum.Parse(typeof(GgmlType), whisperModel, true);

        var outputSrtPath = System.IO.Path.Combine(
            Path.GetDirectoryName(audio.AudioPath),
            $"{Path.GetFileNameWithoutExtension(audio.AudioPath)}.srt");

        //var modelName = "ggml-large-v2-q8_0.bin";
        var modelName = $"Ggml-{type}.bin";
        var modelPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            modelName);

        if (!File.Exists(modelPath))
        {
            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(type);
            using var fileWriter = File.OpenWrite(modelPath);
            await modelStream.CopyToAsync(fileWriter);
        }

        using var whisperFactory = WhisperFactory.FromPath(modelPath);

        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage(language)
            .Build();

        var index = 0;
        var srtData = new SrtData();

        using var fileStream = File.OpenRead(audio.AudioPath);

        await foreach (var segment in processor.ProcessAsync(fileStream))
        {
            srtData.Segments.Add(new SrtSegment
            {
                Index = index,
                Start = segment.Start,
                End = segment.End,
                Text = segment.Text,
            });
            index++;
        }

        var mergedSrtData = SrtMerger.MergeCloseSegments(
            srtData,
            TimeSpan.FromSeconds(7),
            100);

        mergedSrtData.SaveTo(outputSrtPath);

        return new SrtInfo
        {
            SrtData = mergedSrtData,
            SrtPath = outputSrtPath,
            SrtTitle = audio.AudioTitle,
        };
    }
}
