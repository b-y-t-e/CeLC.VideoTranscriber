using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CeLC.VideoTranscriber.Library;

namespace SubtitleEditorDemo
{
    public partial class SubtitleEditorControl : UserControl, INotifyPropertyChanged
    {
        private bool isUpdatingSubtitle = false;
        private bool isDraggingSlider = false;

        // Obiekt zawierający segmenty napisów
        public SrtData SrtData { get; set; } = new SrtData();

        private string _videoPath;

        public string VideoPath
        {
            get => _videoPath;
            set
            {
                _videoPath = value;
                OnPropertyChanged(nameof(VideoPath));
                OnPropertyChanged(nameof(IsReady));
            }
        }

        private string _subtitlesPath;

        public string SubtitlesPath
        {
            get => _subtitlesPath;
            set
            {
                _subtitlesPath = value;
                OnPropertyChanged(nameof(SubtitlesPath));
                OnPropertyChanged(nameof(IsReady));
            }
        }

        // Kontrolka jest gotowa, gdy wybrano oba pliki
        public bool IsReady => !string.IsNullOrEmpty(VideoPath) && !string.IsNullOrEmpty(SubtitlesPath);

        private bool isPlaying;
        private DispatcherTimer timer;
        private int currentSegmentIndex = -1;

        public SubtitleEditorControl()
        {
            InitializeComponent();
            DataContext = this;
            SetupTimer();

            mediaElement.MediaOpened += (s, ev) =>
            {
                // Odtwarzacz załadował plik wideo
            };

            mediaElement.MediaFailed += (s, ev) =>
            {
                MessageBox.Show("MediaFailed – nie można odtworzyć pliku: " + ev.ErrorException.Message);
                VideoPath = "";
                mediaElement.Source = null;
            };
        }

        private void SetupTimer()
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            timer.Tick += Timer_Tick;
        }

        // Kliknięcie na wideo – przełączenie play/pause
        private void MediaElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isPlaying)
                Stop();
            else
                Play();

            e.Handled = true;
        }

        private void Play()
        {
            if (isPlaying)
                return;
            mediaElement.Play();
            timer.Start();
            isPlaying = true;
        }

        private void Stop()
        {
            if (!isPlaying)
                return;
            mediaElement.Pause();
            timer.Stop();
            isPlaying = false;
        }

        // Timer aktualizuje slider i wyszukuje bieżący segment
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (mediaElement.NaturalDuration.HasTimeSpan)
            {
                if (!isDraggingSlider)
                {
                    sliderPosition.Maximum = mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                    sliderPosition.Value = mediaElement.Position.TotalSeconds;
                }

                if (IsReady)
                {
                    TimeSpan currentTime = mediaElement.Position;
                    int index = -1;
                    for (int i = 0; i < SrtData.Segments.Count; i++)
                    {
                        var segment = SrtData.Segments[i];
                        if (currentTime >= segment.Start && currentTime <= segment.End)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index != currentSegmentIndex)
                    {
                        currentSegmentIndex = index;
                        UpdateSubtitleViews();
                    }
                }
            }
        }

        // Metoda pomocnicza do formatowania czasu
        private string FormatTime(TimeSpan time)
        {
            return string.Format("{0:D2}:{1:D2}:{2:D2},{3:D3}",
                time.Hours, time.Minutes, time.Seconds, time.Milliseconds);
        }

        // Aktualizuje pola tekstowe i etykiety z czasem dla poprzedniego, bieżącego i następnego segmentu
        private void UpdateSubtitleViews()
        {
            isUpdatingSubtitle = true;
            if (currentSegmentIndex >= 0 && currentSegmentIndex < SrtData.Segments.Count)
            {
                // Bieżący napis
                textBoxSubtitle.Text = SrtData.Segments[currentSegmentIndex].Text;
                labelTimeCurrent.Content = FormatTime(SrtData.Segments[currentSegmentIndex].Start);

                // Poprzedni napis
                if (currentSegmentIndex > 0)
                {
                    textBoxSubtitlePrevious.Text = SrtData.Segments[currentSegmentIndex - 1].Text;
                    labelTimePrevious.Content = FormatTime(SrtData.Segments[currentSegmentIndex - 1].Start);
                }
                else
                {
                    textBoxSubtitlePrevious.Text = string.Empty;
                    labelTimePrevious.Content = string.Empty;
                }

                // Następny napis
                if (currentSegmentIndex < SrtData.Segments.Count - 1)
                {
                    textBoxSubtitleNext.Text = SrtData.Segments[currentSegmentIndex + 1].Text;
                    labelTimeNext.Content = FormatTime(SrtData.Segments[currentSegmentIndex + 1].Start);
                }
                else
                {
                    textBoxSubtitleNext.Text = string.Empty;
                    labelTimeNext.Content = string.Empty;
                }
            }
            else
            {
                textBoxSubtitlePrevious.Text = "";
                textBoxSubtitle.Text = "";
                textBoxSubtitleNext.Text = "";
                labelTimePrevious.Content = "";
                labelTimeCurrent.Content = "";
                labelTimeNext.Content = "";
            }

            isUpdatingSubtitle = false;
        }

        // Obsługa zdarzeń TextChanged dla trzech pól – każda zmiana zatrzymuje film i zapisuje tekst
        private void TextBoxSubtitlePrevious_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isUpdatingSubtitle && currentSegmentIndex > 0)
            {
                if (isPlaying)
                {
                    mediaElement.Pause();
                    timer.Stop();
                    isPlaying = false;
                }

                SrtData.Segments[currentSegmentIndex - 1].Text = textBoxSubtitlePrevious.Text;
            }
        }

        private void TextBoxSubtitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isUpdatingSubtitle && currentSegmentIndex >= 0)
            {
                if (isPlaying)
                {
                    mediaElement.Pause();
                    timer.Stop();
                    isPlaying = false;
                }

                SrtData.Segments[currentSegmentIndex].Text = textBoxSubtitle.Text;
            }
        }

        private void TextBoxSubtitleNext_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isUpdatingSubtitle && currentSegmentIndex < SrtData.Segments.Count - 1)
            {
                if (isPlaying)
                {
                    mediaElement.Pause();
                    timer.Stop();
                    isPlaying = false;
                }

                SrtData.Segments[currentSegmentIndex + 1].Text = textBoxSubtitleNext.Text;
            }
        }

        // Obsługa zdarzeń myszy dla slidera
        private void SliderPosition_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDraggingSlider = true;
        }

        private void SliderPosition_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDraggingSlider = false;
            if (mediaElement.NaturalDuration.HasTimeSpan)
            {
                mediaElement.Position = TimeSpan.FromSeconds(sliderPosition.Value);
            }
        }

        // Wybór pliku wideo
        private void BtnBrowseVideo_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.avi;*.mkv|All Files|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                if (File.Exists(VideoPath))
                {
                    VideoPath = ofd.FileName;
                    mediaElement.Source = new Uri(VideoPath);
                }
            }
        }

        // Wybór pliku napisów
        private void BtnBrowseSubtitles_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Subtitle Files|*.srt|All Files|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                SubtitlesPath = ofd.FileName;
                if (File.Exists(SubtitlesPath))
                {
                    SrtData.Segments.Clear();
                    SrtData.LoadFrom(SubtitlesPath);
                }
            }
        }

        // Zapis napisów do pliku
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!IsReady)
            {
                //MessageBox.Show("No video or subtitles selected!");
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "Subtitle Files|*.srt|All Files|*.*",
                FileName = Path.GetFileName(this.SubtitlesPath) // "subtitles_modified.srt"
            };

            if (sfd.ShowDialog() == true)
            {
                SrtData.SaveTo(sfd.FileName);
            }
        }

        // Nawigacja do poprzedniego segmentu
        private void BtnPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (!IsReady || !mediaElement.CanPause) return;
            if (currentSegmentIndex > 0)
            {
                bool wasPlaying = isPlaying;
                if (isPlaying)
                    Stop();

                currentSegmentIndex--;
                mediaElement.Position = SrtData.Segments[currentSegmentIndex].Start + TimeSpan.FromMilliseconds(100);
                UpdateSubtitleViews();

                if (wasPlaying)
                    Play();
            }
        }

        // Nawigacja do następnego segmentu
        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (!IsReady || !mediaElement.CanPause) return;
            if (currentSegmentIndex < SrtData.Segments.Count - 1)
            {
                bool wasPlaying = isPlaying;
                if (isPlaying)
                    Stop();

                currentSegmentIndex++;
                mediaElement.Position = SrtData.Segments[currentSegmentIndex].Start + TimeSpan.FromMilliseconds(100);
                UpdateSubtitleViews();

                if (wasPlaying)
                    Play();
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #endregion
    }
}
