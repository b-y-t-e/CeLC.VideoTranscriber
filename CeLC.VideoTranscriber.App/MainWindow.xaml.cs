using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CeLC.VideoTranscriber.Library;
using Microsoft.Win32;

namespace CeLC.VideoTranscriber.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            UpdateValuesOnChange();
        }

        private void OnVideoFileTextBoxKerPressed(object sender, KeyEventArgs e)
        {
            UpdateValuesOnChange();
        }

        private void OnVideoFileTextBoxChanged(object sender, TextChangedEventArgs e)
        {
            UpdateValuesOnChange();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // Konfiguracja okna dialogowego wyboru pliku
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Video files|*.mp4;*.avi;*.mkv|All files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
                VideoFileTextBox.Text = openFileDialog.FileName;
        }

        private async void OnExecuteClick(object sender, RoutedEventArgs e)
        {
            OnTranslationProgress("");

            if (!string.IsNullOrWhiteSpace(YoutubeTextBox.Text) && !YoutubeTextBox.Text.ToLower().StartsWith("http"))
                YoutubeTextBox.Text = "https://" + YoutubeTextBox.Text;

            SrtInfo srtEng = null;
            Stopwatch stowatch = Stopwatch.StartNew();

            try
            {
                this.IsEnabled = false;

                if (String.IsNullOrEmpty(YoutubeTextBox.Text) &&
                    String.IsNullOrEmpty(VideoFileTextBox.Text))
                    throw new Exception("You must enter a video file.");

                OnTranslationProgress("Downloading...");

                VideoInfo video = !String.IsNullOrEmpty(YoutubeTextBox.Text)
                    ? await new YoutubeDownloader().DownloadYoutube(YoutubeTextBox.Text)
                    : await new RawVideo().Load(VideoFileTextBox.Text);

                OnTranslationProgress("Extracting audio...");

                AudioInfo audio = await new AudioExtractor()
                    .ExtractAudio(video);

                OnTranslationProgress("Transcribing...");

                srtEng = await new AudioTranscriber()
                    .TranscribeAudioToSrt(audio, ComboBoxSource.Text, ComboBoxWhisperModel.Text);


                if (!string.IsNullOrEmpty(ComboBoxDestination.Text))
                {
                    OnTranslationProgress($"Translating to {ComboBoxDestination.Text}...");

                    SrtInfo srtPol =
                        await new SrtTranslator()
                            .TranslateSrt(
                                srtEng,
                                ComboBoxSource.Text,
                                ComboBoxDestination.Text,
                                ComboBoxOpenAIModel.Text,
                                openAiApiKey: OpenAiApiKey.Text,
                                deepseekApiKey: DeepSeekApiKey.Text,
                                progress: OnTranslationProgress);

                    SrtInfo srtEngPol =
                        await new SrtTranslator()
                            .TranslateSrt(
                                srtEng,
                                ComboBoxSource.Text,
                                ComboBoxDestination.Text,
                                ComboBoxOpenAIModel.Text,
                                twoLanguages: true,
                                openAiApiKey: OpenAiApiKey.Text,
                                deepseekApiKey: DeepSeekApiKey.Text,
                                progress: OnTranslationProgress);
                }

                OnTranslationProgress($"> Job done in {stowatch.Elapsed.TotalSeconds}s <");

                MessageBox.Show($"Job done in {stowatch.Elapsed.TotalSeconds}s", "Success", MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                OnTranslationProgress($"> Error: {ex.Message}, after {stowatch.Elapsed.TotalSeconds}s <");
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;

                if (srtEng != null)
                    Process.Start("explorer.exe", Path.GetDirectoryName(srtEng.SrtPath));
            }
        }

        private void OnTranslationProgress(int count, int total)
        {
            Dispatcher.BeginInvoke(() =>
            {
                TextBlockStatus.Text =
                    $"> Translating to {ComboBoxDestination.Text} ({count}/{total})... <";
            });
        }

        private void OnTranslationProgress(string text)
        {
            Dispatcher.BeginInvoke(() => { TextBlockStatus.Text = text; });
        }

        private void OnMuxProgress(string text)
        {
            Dispatcher.BeginInvoke(() => { TextBlockStatusMux.Text = text; });
        }

        private void ApiKeyChanged(object sender, TextChangedEventArgs e)
        {
            UpdateValuesOnChange();
        }

        private void ApiKeyKeyPresed(object sender, KeyEventArgs e)
        {
            UpdateValuesOnChange();
        }

        private void UpdateValuesOnChange()
        {
            if (!string.IsNullOrWhiteSpace(VideoFileTextBox.Text))
            {
                YoutubeTextBox.Text = "";
                YoutubeTextBox.IsEnabled = false;
            }
            else
            {
                YoutubeTextBox.IsEnabled = true;
            }

            ComboBoxDestination.IsEnabled =
                !string.IsNullOrEmpty(DeepSeekApiKey.Text) || !string.IsNullOrEmpty(OpenAiApiKey.Text);

            if (string.IsNullOrEmpty(DeepSeekApiKey.Text) && string.IsNullOrEmpty(OpenAiApiKey.Text))
                ComboBoxDestination.Text = "";
        }

        private async void OnMuxExecuteClick(object sender, RoutedEventArgs e)
        {
            OnMuxProgress("");

            string? directory = null;
            Stopwatch stowatch = Stopwatch.StartNew();

            try
            {
                this.IsEnabled = false;

                if (String.IsNullOrEmpty(MuxVideoFileTextBox.Text) )
                    throw new Exception("You must enter a video file.");

                if (!File.Exists(MuxVideoFileTextBox.Text) )
                    throw new Exception("Video file does not exist.");

                if (String.IsNullOrEmpty(MuxSubtitlesTextBox.Text) )
                    throw new Exception("You must enter a subtitles file.");

                if (!File.Exists(MuxSubtitlesTextBox.Text) )
                    throw new Exception("Subtitles file does not exist.");

                directory = Path.GetDirectoryName(MuxVideoFileTextBox.Text);

                OnMuxProgress("Muxing...");

                await new TextMuxerExtractor().MuxVideoWithText(
                    MuxVideoFileTextBox.Text,
                    MuxSubtitlesTextBox.Text);

                OnMuxProgress($"> Job done in {stowatch.Elapsed.TotalSeconds}s <");

                MessageBox.Show($"Job done in {stowatch.Elapsed.TotalSeconds}s", "Success", MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                OnMuxProgress($"> Error: {ex.Message}, after {stowatch.Elapsed.TotalSeconds}s <");
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;

                if (directory != null)
                    Process.Start("explorer.exe", directory);
            }
        }

        private void BrowseMuxVideoFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Video files|*.mp4;*.avi;*.mkv|All files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
                MuxVideoFileTextBox.Text = openFileDialog.FileName;
        }

        private void BrowseMuxSubtitles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Srt|*.srt|All files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
                MuxSubtitlesTextBox.Text = openFileDialog.FileName;
        }
    }
}
