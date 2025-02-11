using System.Text.RegularExpressions;
using System.Web;
using YoutubeExplode;
using YoutubeExplode.Converter;

namespace CeLC.VideoTranscriber.Library;

public class YoutubeDownloader
{
    public async Task<VideoInfo> DownloadYoutube(string url)
    {
        string mainDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Output");

        if (!Directory.Exists(mainDirectory))
            Directory.CreateDirectory(mainDirectory);

        var videoUrl = GetYouTubeVideoUrl(url);
        var videoTitle = GetYouTubeVideoTitle(url);
        var id = GetYouTubeVideoId(videoUrl);

        var youtube = new YoutubeClient();
        var videoInfo = await youtube.Videos.GetAsync(id);
        if (string.IsNullOrEmpty(videoTitle))
            videoTitle = MakeSafeForFileName(videoInfo.Title);

        var inputAviPath = GetUniqueFilePath(System.IO.Path.Combine(
            mainDirectory,
            $"{videoTitle}.mp4"));

        await youtube.Videos.DownloadAsync(videoUrl, inputAviPath);

        return new VideoInfo
        {
            VideoPath = inputAviPath,
            VideoUrl = videoUrl,
            VideoTitle = videoTitle
        };
    }

    static string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }

        string directory = System.IO.Path.GetDirectoryName(filePath);
        string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);
        string extension = System.IO.Path.GetExtension(filePath);
        int fileNumber = 1;

        string newFilePath;
        do
        {
            newFilePath = System.IO.Path.Combine(directory, $"{fileNameWithoutExtension}({fileNumber++}){extension}");
        } while (File.Exists(newFilePath));

        return newFilePath;
    }

    string GetYouTubeVideoUrl(string videoUrl)
    {
        var urlMatch = Regex.Match(videoUrl, @"https:\/\/www\.youtube\.com\/watch\?v=[\w-]+");
        if (urlMatch.Success)
            return urlMatch.Value;
        return null;
    }

    string GetYouTubeVideoTitle(string videoUrl)
    {
        // Wyszukuje tytuł, ignorując białe znaki.
        var titleMatch = Regex.Match(videoUrl, @"-[\s]*title[\s]+'([^']*)'");
        if (titleMatch.Success)
            return titleMatch.Groups[1].Value;
        return null;
    }

    static string MakeSafeForFileName(string input)
    {
        // Lista niedozwolonych znaków w nazwach plików dla większości systemów plików
        string invalidChars = Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
        string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

        // Zamiana niedozwolonych znaków na podkreślnik
        return Regex.Replace(input, invalidRegStr, "_");
    }

    string GetYouTubeVideoId(string url)
    {
        // Tworzenie Uri z podanego URL, z założeniem, że może być potrzeba dodania schematu.
        Uri uriResult;
        var result = Uri.TryCreate(url, UriKind.Absolute, out uriResult)
            ? uriResult
            : new Uri("http://" + url);

        // Parsowanie query string
        var query = HttpUtility.ParseQueryString(uriResult.Query);

        // Zwracanie wartości parametru 'v', który jest identyfikatorem wideo
        return query["v"];
    }

}
