using System.Diagnostics;
using NAudio.Wave;

namespace Celc.VideoTranscriber.Library;

public class AudioExtractor
{
    void ConvertTo16KMono(string inputFile, string outputFile)
    {
        // AudioFileReader automatycznie obsługuje różne formaty (mp3, wav, itp.)
        using (var reader = new AudioFileReader(inputFile))
        {
            // Ustawiamy docelowy format: 16 kHz, 16-bit, 1 kanał (mono)
            var targetFormat = new WaveFormat(16000, 16, 1);

            // MediaFoundationResampler wykonuje konwersję formatu
            using (var resampler = new MediaFoundationResampler(reader, targetFormat))
            {
                // Ustawienie jakości resamplingu (zakres: 1 - 60, gdzie 60 to najwyższa jakość)
                resampler.ResamplerQuality = 60;

                // Tworzymy plik WAV w zadanym formacie
                WaveFileWriter.CreateWaveFile(outputFile, resampler);
            }
        }
    }

    public async Task<AudioInfo> ExtractAudio(VideoInfo inputFilePath)
    {
        var outputMp3Path = System.IO.Path.Combine(
            Path.GetDirectoryName(inputFilePath.VideoPath),
            $"{Path.GetFileNameWithoutExtension(inputFilePath.VideoPath)}.mp3");

        var outputwavPath = System.IO.Path.Combine(
            Path.GetDirectoryName(inputFilePath.VideoPath),
            $"{Path.GetFileNameWithoutExtension(inputFilePath.VideoPath)}.wav");

        try
        {
            await ConvertVideoToMp3(inputFilePath.VideoPath, outputMp3Path);

            ConvertTo16KMono(outputMp3Path, outputwavPath);
        }
        finally
        {
            if (File.Exists(outputMp3Path))
                File.Delete(outputMp3Path);
        }

        return new AudioInfo
        {
            AudioPath = outputwavPath,
            AudioTitle = inputFilePath.VideoTitle
        };
    }

    async Task ConvertVideoToMp3(string inputFilePath, string outputFilePath)
    {
        string ffmpegPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg_.exe");

        //outputFilePath = System.IO.Path.ChangeExtension(outputFilePath, ".mp3");

        string arguments = $"-i \"{inputFilePath}\" -vn -ar 44100 -ac 2 -b:a 192k \"{outputFilePath}\"";

        ProcessStartInfo startInfo = new ProcessStartInfo(ffmpegPath, arguments)
        {
            CreateNoWindow = false,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using (Process process = Process.Start(startInfo))
            await process.WaitForExitAsync();
    }
}
