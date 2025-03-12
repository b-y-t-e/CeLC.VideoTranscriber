using System.Diagnostics;
using NAudio.Wave;

namespace CeLC.VideoTranscriber.Library;

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

public class TextMuxerExtractor
{
    public async Task MuxVideoWithText(
        string inputVideoFile,
        string inputTextFile,
        string startTime,
        string endTime,
        string outputFile = null)
    {
        if (string.IsNullOrEmpty(outputFile))
            outputFile = System.IO.Path.Combine(
                Path.GetDirectoryName(inputVideoFile),
                $"{Path.GetFileNameWithoutExtension(inputVideoFile)}_with_subtitles{Path.GetExtension(inputVideoFile)}");

        var newTextFile = System.IO.Path.Combine(
            Path.GetDirectoryName(inputVideoFile),
            $"{Guid.NewGuid()}{Path.GetExtension(inputTextFile)}");

        var directory = Environment.CurrentDirectory;

        Environment.CurrentDirectory = Path.GetDirectoryName(inputVideoFile);
        File.Copy(inputTextFile, newTextFile);
        File.Delete(outputFile);

        try
        {
            string ffmpegPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg_.exe");

            string arguments = "";

            if (!string.IsNullOrWhiteSpace(startTime) && !string.IsNullOrWhiteSpace(endTime))
            {
                arguments =
                    $"-ss {startTime} -to {endTime} -i \"{inputVideoFile}\" -vf \"subtitles={Path.GetFileName(newTextFile)}:force_style='BorderStyle=3,PrimaryColour=&H00FFFFFF,BackColour=&HFF000000,FontSize=26'\" \"{outputFile}\"";
            }
            else if (string.IsNullOrWhiteSpace(startTime) && !string.IsNullOrWhiteSpace(endTime))
            {
                startTime = "00:00:00";
                arguments =
                    $"-ss {startTime} -to {endTime} -i \"{inputVideoFile}\" -vf \"subtitles={Path.GetFileName(newTextFile)}:force_style='BorderStyle=3,PrimaryColour=&H00FFFFFF,BackColour=&HFF000000,FontSize=26'\" \"{outputFile}\"";
            }
            else if (!string.IsNullOrWhiteSpace(startTime) && string.IsNullOrWhiteSpace(endTime))
            {
                endTime = "99:00:00";
                arguments =
                    $"-ss {startTime} -to {endTime} -i \"{inputVideoFile}\" -vf \"subtitles={Path.GetFileName(newTextFile)}:force_style='BorderStyle=3,PrimaryColour=&H00FFFFFF,BackColour=&HFF000000,FontSize=26'\" \"{outputFile}\"";
            }
            else
            {
                arguments =
                    $"-i \"{inputVideoFile}\" -vf \"subtitles={Path.GetFileName(newTextFile)}:force_style='BorderStyle=3,PrimaryColour=&H00FFFFFF,BackColour=&HFF000000,FontSize=26'\" \"{outputFile}\"";
            }

            ProcessStartInfo startInfo = new ProcessStartInfo(ffmpegPath, arguments)
            {
                CreateNoWindow = false,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process process = Process.Start(startInfo))
                await process.WaitForExitAsync();

        }
        finally
        {
            File.Delete(newTextFile);
            Environment.CurrentDirectory = directory;
        }
    }
}
