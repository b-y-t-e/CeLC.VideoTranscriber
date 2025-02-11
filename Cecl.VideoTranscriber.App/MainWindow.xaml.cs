using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Celc.VideoTranscriber.Library;
using Microsoft.Win32;

namespace Cecl.VideoTranscriber.App
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
            TextBlockStatus.Text = "";

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

                TextBlockStatus.Text = "Downloading...";

                VideoInfo video = !String.IsNullOrEmpty(YoutubeTextBox.Text)
                    ? await new YoutubeDownloader().DownloadYoutube(YoutubeTextBox.Text)
                    : await new RawVideo().Load(VideoFileTextBox.Text);

                TextBlockStatus.Text = "Extracting audio...";

                AudioInfo audio = await new AudioExtractor()
                    .ExtractAudio(video);

                TextBlockStatus.Text = "Transcribing...";

                srtEng = await new AudioTranscriber()
                    .TranscribeAudioToSrt(audio, ComboBoxSource.Text, ComboBoxWhisperModel.Text);


                if (!string.IsNullOrEmpty(ComboBoxDestination.Text))
                {
                    TextBlockStatus.Text = $"Translating to {ComboBoxDestination.Text}...";

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

                TextBlockStatus.Text = $"> Job done in {stowatch.Elapsed.TotalSeconds}s <";

                MessageBox.Show($"Job done in {stowatch.Elapsed.TotalSeconds}s", "Success", MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                TextBlockStatus.Text = $"> Error: {ex.Message}, after {stowatch.Elapsed.TotalSeconds}s <";
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
    }
}
