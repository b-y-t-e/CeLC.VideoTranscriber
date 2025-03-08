using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
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
        private bool isPlaying;
        private DispatcherTimer timer;
        private int currentSegmentIndex = -1;
        private string _videoPath;
        private string _subtitlesPath;

        // Object containing subtitle segments.
        public SrtData SrtData { get; set; } =
            new SrtData();

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

        // Control is ready when both files are selected.
        public bool IsReady =>
            !string.IsNullOrEmpty(VideoPath) && !string.IsNullOrEmpty(SubtitlesPath);

        public SubtitleEditorControl()
        {
            InitializeComponent();
            DataContext = this;
            SetupTimer();

            // Subscribe to media events.
            mediaElement.MediaOpened += (s, ev) =>
            {
                // Media loaded successfully.
            };

            mediaElement.MediaFailed += (s, ev) =>
            {
                MessageBox.Show("MediaFailed - cannot play file: " + ev.ErrorException.Message);
                VideoPath = "";
                mediaElement.Source = null;
            };

            UpdateSubtitleViews();
        }

        // Toggle play/pause when video is clicked.
        private void MediaElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isPlaying)
                Stop();
            else
                Play();

            e.Handled = true;
        }

        // Timer tick: update slider and search for the current subtitle segment.
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
                    int index = FindSegmentIndex(currentTime);

                    if (index != currentSegmentIndex)
                    {
                        currentSegmentIndex = index;
                        UpdateSubtitleViews();
                    }
                }
            }
        }

        // TextChanged events for subtitle text boxes.
        private void TextBoxSubtitlePrevious_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isUpdatingSubtitle && currentSegmentIndex > 0)
            {
                PausePlaybackIfPlaying();
                SrtData.Segments[currentSegmentIndex - 1].Text = textBoxSubtitlePrevious.Text;
            }
        }

        private void TextBoxSubtitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isUpdatingSubtitle && currentSegmentIndex >= 0)
            {
                PausePlaybackIfPlaying();
                SrtData.Segments[currentSegmentIndex].Text = textBoxSubtitle.Text;
            }
        }

        private void TextBoxSubtitleNext_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isUpdatingSubtitle && currentSegmentIndex < SrtData.Segments.Count - 1)
            {
                PausePlaybackIfPlaying();
                SrtData.Segments[currentSegmentIndex + 1].Text = textBoxSubtitleNext.Text;
            }
        }

        // Slider events.
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

        // Button click events.
        private void BtnBrowseVideo_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.avi;*.mkv|All Files|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                if (File.Exists(ofd.FileName))
                {
                    VideoPath = ofd.FileName;
                    mediaElement.Source = new Uri(VideoPath);
                }
            }
        }

        private void BtnBrowseSubtitles_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Subtitle Files|*.srt|All Files|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                if (File.Exists(ofd.FileName))
                {
                    SubtitlesPath = ofd.FileName;
                    SrtData.Segments.Clear();
                    SrtData.LoadFrom(SubtitlesPath);
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!IsReady)
            {
                // Optionally, show a message like "No video or subtitles selected!"
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "Subtitle Files|*.srt|All Files|*.*",
                FileName = Path.GetFileName(SubtitlesPath)
            };

            if (sfd.ShowDialog() == true)
            {
                SrtData.SaveTo(sfd.FileName);
            }
        }

        private void BtnPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (!IsReady || !mediaElement.CanPause)
                return;

            if (currentSegmentIndex > 0)
            {
                bool wasPlaying = isPlaying;
                if (isPlaying)
                    Stop();

                currentSegmentIndex--;
                NavigateToCurrentSegment();
                UpdateSubtitleViews();

                if (wasPlaying)
                    Play();
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (!IsReady || !mediaElement.CanPause)
                return;

            if (currentSegmentIndex < SrtData.Segments.Count - 1)
            {
                bool wasPlaying = isPlaying;
                if (isPlaying)
                    Stop();

                currentSegmentIndex++;
                NavigateToCurrentSegment();
                UpdateSubtitleViews();

                if (wasPlaying)
                    Play();
            }
        }

        private void SetupTimer()
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            timer.Tick += Timer_Tick;
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

        // Pauses playback if currently playing.
        private void PausePlaybackIfPlaying()
        {
            if (isPlaying)
                Stop();
        }

        // Finds the index of the subtitle segment corresponding to the given time.
        private int FindSegmentIndex(TimeSpan time)
        {
            return SrtData.Segments.FindIndex(segment => time >= segment.Start && time <= segment.End);
        }

        // Formats a TimeSpan into a subtitle-friendly string.
        private string FormatTime(TimeSpan time)
        {
            return string.Format("{0:D2}:{1:D2}:{2:D2},{3:D3}",
                time.Hours, time.Minutes, time.Seconds, time.Milliseconds);
        }

        // Updates text boxes and labels for the previous, current, and next segments.
        private void UpdateSubtitleViews()
        {
            isUpdatingSubtitle = true;
            if (currentSegmentIndex >= 0 && currentSegmentIndex < SrtData.Segments.Count)
            {
                panelEditors.IsEnabled = true;
                // Current subtitle.
                textBoxSubtitle.Text = SrtData.Segments[currentSegmentIndex].Text;
                labelTimeCurrent.Content = FormatTime(SrtData.Segments[currentSegmentIndex].Start);

                // Previous subtitle.
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

                // Next subtitle.
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
                panelEditors.IsEnabled = false;
                textBoxSubtitlePrevious.Text = "";
                textBoxSubtitle.Text = "";
                textBoxSubtitleNext.Text = "";
                labelTimePrevious.Content = "";
                labelTimeCurrent.Content = "";
                labelTimeNext.Content = "";
            }
            isUpdatingSubtitle = false;
        }

        // Navigates to the start position of the current segment (with a small offset).
        private void NavigateToCurrentSegment()
        {
            mediaElement.Position = SrtData.Segments[currentSegmentIndex].Start + TimeSpan.FromMilliseconds(100);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

    }
}
