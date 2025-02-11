namespace Celc.VideoTranscriber.Library;

public class RawVideo
{
    public async Task<VideoInfo> Load(string filePath)
    {
        string mainDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Output");

        if (!Directory.Exists(mainDirectory))
            Directory.CreateDirectory(mainDirectory);

        if(!File.Exists(filePath))
            throw new FileNotFoundException("File not found");

        var videoTitle = Path.GetFileNameWithoutExtension(filePath);

        var path = GetUniqueFilePath(Path.Combine(mainDirectory, Path.GetFileName(filePath)));

        System.IO.File.Copy(filePath, path);

        return new VideoInfo
        {
            VideoPath = path,
            VideoUrl = "",
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

}
