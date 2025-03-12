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
        private bool isPlaying;
        private DispatcherTimer timer;
        private int currentSegmentIndex = -1;
        private string _videoPath;
        private string _subtitlesPath;

        // Object containing subtitle segments.
        public SrtData SrtData { get; set; } = new SrtData();

        public SrtSegment CurrentSegment =>
            currentSegmentIndex >= 0 && currentSegmentIndex < SrtData.Segments.Count
                ? SrtData.Segments[currentSegmentIndex]
                : null;

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

            this.PreviewMouseWheel += (s, ev) =>
            {
                if (ev.Delta > 0)
                {
                    this.BtnPrevious_Click(null, null);
                }
                else
                {
                    this.BtnNext_Click(null, null);
                }
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

                UpdateSubtitles();
            }
        }

        private void UpdateSubtitles(bool forceUpdate = false)
        {
            if (IsReady)
            {
                TimeSpan currentTime = mediaElement.Position;
                int index = FindSegmentIndex(currentTime);

                if (index != currentSegmentIndex || forceUpdate)
                {
                    currentSegmentIndex = index;
                    UpdateSubtitleViews();
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

                    var srtPath = Path.ChangeExtension(VideoPath, "srt");
                    if (File.Exists(srtPath))
                    {
                        SubtitlesPath = srtPath;
                        SrtData.Segments.Clear();
                        SrtData.LoadFrom(SubtitlesPath);
                        UpdateSubtitles(true);
                    }
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
                    UpdateSubtitles(true);
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

            var bckPath = SubtitlesPath + ".bak";
            if (!File.Exists(bckPath))
                File.Copy(SubtitlesPath, bckPath);

            SrtData.SaveTo(SubtitlesPath);

            if (SrtData.ContainsOriginalText)
            {
                var path = Path.ChangeExtension(SubtitlesPath, ".final.srt");
                SrtData.SaveTo(path, false);
            }

            /*var sfd = new SaveFileDialog
            {
                Filter = "Subtitle Files|*.srt|All Files|*.*",
                FileName = Path.GetFileName(SubtitlesPath)
            };

            if (sfd.ShowDialog() == true)
            {
                SrtData.SaveTo(sfd.FileName);
            }*/
        }

        private void BtnPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (!IsReady) // || !mediaElement.CanPause)
                return;

            if (currentSegmentIndex > 0)
            {
                var diff = mediaElement.Position - CurrentSegment?.Start;
                if (diff?.TotalMilliseconds > 1000)
                {
                    mediaElement.Position = CurrentSegment.Start + TimeSpan.FromMilliseconds(100);
                    return;
                }

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
            if (!IsReady) //|| !mediaElement.CanPause)
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
            var index = SrtData
                .Segments
                .FindIndex(segment => time >= segment.Start && time <= segment.End);

            if (index >= 0)
                return index;

            index = SrtData
                .Segments
                .OrderBy(x => x.Start)
                .ToList()
                .FindIndex(segment => time < segment.Start);

            return index;
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
                // Bieżący napis.
                textBoxSubtitle.Text = SrtData.Segments[currentSegmentIndex].Text;
                labelTimeCurrent.Text = FormatTime(SrtData.Segments[currentSegmentIndex].Start);
                textBoxOriginalCurrent.Text = SrtData.Segments[currentSegmentIndex].TextOriginal;

                // Poprzedni napis.
                if (currentSegmentIndex > 0)
                {
                    textBoxSubtitlePrevious.Text = SrtData.Segments[currentSegmentIndex - 1].Text;
                    labelTimePrevious.Text = FormatTime(SrtData.Segments[currentSegmentIndex - 1].Start);
                    textBoxOriginalPrevious.Text = SrtData.Segments[currentSegmentIndex - 1].TextOriginal;
                }
                else
                {
                    textBoxSubtitlePrevious.Text = string.Empty;
                    labelTimePrevious.Text = string.Empty;
                    textBoxOriginalPrevious.Text = string.Empty;
                }

                // Następny napis.
                if (currentSegmentIndex < SrtData.Segments.Count - 1)
                {
                    textBoxSubtitleNext.Text = SrtData.Segments[currentSegmentIndex + 1].Text;
                    labelTimeNext.Text = FormatTime(SrtData.Segments[currentSegmentIndex + 1].Start);
                    textBoxOriginalNext.Text = SrtData.Segments[currentSegmentIndex + 1].TextOriginal;
                }
                else
                {
                    textBoxSubtitleNext.Text = string.Empty;
                    labelTimeNext.Text = string.Empty;
                    textBoxOriginalNext.Text = string.Empty;
                }
            }
            else
            {
                panelEditors.IsEnabled = false;
                textBoxSubtitlePrevious.Text = "";
                textBoxSubtitle.Text = "";
                textBoxSubtitleNext.Text = "";
                labelTimePrevious.Text = "";
                labelTimeCurrent.Text = "";
                labelTimeNext.Text = "";
                textBoxOriginalPrevious.Text = "";
                textBoxOriginalCurrent.Text = "";
                textBoxOriginalNext.Text = "";
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
