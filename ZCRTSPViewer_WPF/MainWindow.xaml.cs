using System.Windows;
using System.Windows.Controls;
using LibVLCSharp.Shared;
using System.IO;
using LibVLCSharp.WPF;
using System.Windows.Media;
using System.Text;
using System.Windows.Input;
using System.Diagnostics;

namespace ZCRTSPViewer_WPF
{
    public partial class MainWindow : Window
    {
        //Timer Variables for restarting the program
        private System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
        private TimeSpan ResetTime = new TimeSpan(0,0,10);

        private bool IsRecording = false;

        //Keep count of all camera URLs and amounts
        List<string> cameraUrls = new List<string>();
        private int CameraAmount = 0;

        List<Process> RecordingProcesses = new List<Process>();

        private LibVLC _libVlc;

        public MainWindow()
        {
            InitializeComponent();

            //Initialize VLC Library
            Core.Initialize(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VLC"));

            // Initialize a single LibVLC instance
            _libVlc = new LibVLC();
            _libVlc.Log += OnVlcLog;

            //Load all camera views
            LoadView();

            //Set fullscren on startup, if argument is given
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                string argument = args[1];

                if (argument == "fullscreen")
                {
                    this.ResizeMode = ResizeMode.NoResize;
                    this.WindowStyle = WindowStyle.None;
                    this.WindowState = WindowState.Normal;
                    this.WindowState = WindowState.Maximized;
                    VideoGrid.Margin = new Thickness(0, 0, 0, 0);
                }

                else if (argument == "timer")
                {
                    StartResetTimer();
                }
            }

            // Check if both "fullscreen" and "timer" arguments are present
            if (args.Length > 2)
            {
                // Additional logic to check for both fullscreen and timer arguments
                if (args[1] == "fullscreen" && args[2] == "timer")
                {
                    // Handle both arguments here
                    // Fullscreen logic (as above)
                    this.ResizeMode = ResizeMode.NoResize;
                    this.WindowStyle = WindowStyle.None;
                    this.WindowState = WindowState.Normal;
                    this.WindowState = WindowState.Maximized;
                    VideoGrid.Margin = new Thickness(0, 0, 0, 0);

                    // Timer logic (as above)
                    StartResetTimer();
                }
            }
        }

        void StartResetTimer()
        {
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = ResetTime;
            dispatcherTimer.Start();

            ResetCamerasBtn.Content = "Stop Reset Cameras Timer";
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden; //Hide cmd
            startInfo.FileName = "ZCRTSPViewer_WPF.exe";
            startInfo.Arguments = "fullscreen timer";
            process.StartInfo = startInfo;
            process.Start();

            foreach (Process processlocal in RecordingProcesses)
            {
                processlocal.Kill();
            }

            Close();
        }

        private void OnFullscreenExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        private void ToggleFullscreen()
        {
            if (this.WindowStyle == WindowStyle.None)
            {
                this.ResizeMode = ResizeMode.CanResize;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                VideoGrid.Margin = new Thickness(0, 0, 0, 100);
            }
            else
            {
                this.ResizeMode = ResizeMode.NoResize;
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Normal;
                this.WindowState = WindowState.Maximized;
                VideoGrid.Margin = new Thickness(0, 0, 0, 0);
            }
        }

        public void LoadView()
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saved_cameras.txt");

            if (!File.Exists(filePath))
            {
                using (FileStream fs = File.Create(filePath))
                {
                    // Add some text to file
                    Byte[] title = new UTF8Encoding(true).GetBytes("http://download.blender.org/peach/bigbuckbunny_movies/big_buck_bunny_480p_h264.mov");
                    fs.Write(title, 0, title.Length);
                }

                MessageBox.Show("Initial 'saved_cameras.txt' has been created. You can now put your own URL's in the file");
            }

            int lineCount = 0;

            using (StreamReader sr = new StreamReader(filePath))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    lineCount++;

                    // Add the URL to the list
                    cameraUrls.Add(line);
                }
            }

            // Calculate the optimal grid size
            int requiredColumns = (int)Math.Ceiling(Math.Sqrt(lineCount)); // Square root to get a balanced grid
            int requiredRows = (int)Math.Ceiling((double)lineCount / requiredColumns);

            // Ensure the grid has enough rows and columns
            UpdateGrid(requiredRows, requiredColumns);

            // Now you have a list of camera URLs
            foreach (var url in cameraUrls)
            {
                AddVideoView(url);
            }
        }

        public void AddVideoView(string streamUrl)
        {
            int maxColumns = VideoGrid.ColumnDefinitions.Count;
            int maxRows = VideoGrid.RowDefinitions.Count;
            int totalSlots = maxRows * maxColumns;

            if (CameraAmount >= totalSlots)
            {
                //MessageBox.Show("Please increase grid size to add more cameras");
                return;
            }

            // Calculate position based on current camera count
            int row = CameraAmount / maxColumns;  // Integer division
            int column = CameraAmount % maxColumns;  // Remainder

            var videoView = new VideoView
            {
                Background = Brushes.Black,
                Tag = streamUrl // Store the URL inside the VideoView
            };
            videoView.Loaded += VideoView_Loaded;

            Grid.SetRow(videoView, row);
            Grid.SetColumn(videoView, column);

            CameraAmount += 1;
            VideoGrid.Children.Add(videoView);
        }

        private void VideoView_Loaded(object sender, RoutedEventArgs e)
        {
            var videoView = sender as VideoView;
            if (videoView == null || videoView.Tag == null) return;

            string streamUrl = videoView.Tag.ToString();
            if (string.IsNullOrEmpty(streamUrl)) return;

            var _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
            _mediaPlayer.AspectRatio = "16:9";

            videoView.MediaPlayer = _mediaPlayer;

            var media = new Media(_libVlc, streamUrl, FromType.FromLocation);
            media.AddOption(":network-caching=3000");
            media.AddOption(":avcodec-hw=any");
            media.AddOption(":swscale-mode=fast");
            media.AddOption(":avcodec-skiploopfilter=all");

            _mediaPlayer.Play(media);
        }


        private void OnVlcLog(object sender, LogEventArgs e)
        {
            // Format the log message
            string logMessage = $"[{e.Level}] {e.Module}: {e.Message}";

            // Write to a file (or integrate with a logging library)
            File.AppendAllText("vlc_logs.txt", logMessage + Environment.NewLine);
        }

        private void UpdateGrid(int targetRows, int targetColumns)
        {
            // Get current counts
            int currentRows = VideoGrid.RowDefinitions.Count;
            int currentColumns = VideoGrid.ColumnDefinitions.Count;

            // Update rows
            if (targetRows > currentRows)
            {
                // Add missing rows
                for (int i = currentRows; i < targetRows; i++)
                {
                    VideoGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                }
            }
            else if (targetRows < currentRows)
            {
                // Remove extra rows only if empty (from bottom-up)
                for (int i = currentRows - 1; i >= targetRows; i--)
                {
                    if (IsRowEmpty(i))
                    {
                        VideoGrid.RowDefinitions.RemoveAt(i);
                    }
                    else
                    {
                        MessageBox.Show($"Row {i} is not empty. Cannot remove.");
                        break; // Stop removing to avoid layout issues
                    }
                }
            }

            // Update columns
            if (targetColumns > currentColumns)
            {
                // Add missing columns
                for (int i = currentColumns; i < targetColumns; i++)
                {
                    VideoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }
            }
            else if (targetColumns < currentColumns)
            {
                // Remove extra columns only if empty (from right to left)
                for (int i = currentColumns - 1; i >= targetColumns; i--)
                {
                    if (IsColumnEmpty(i))
                    {
                        VideoGrid.ColumnDefinitions.RemoveAt(i);
                    }
                    else
                    {
                        MessageBox.Show($"Column {i} is not empty. Cannot remove.");
                        break;
                    }
                }
            }
        }

        // Check if a row is empty
        private bool IsRowEmpty(int row)
        {
            foreach (UIElement child in VideoGrid.Children)
            {
                if (Grid.GetRow(child) == row)
                {
                    return false; // Row contains at least one element
                }
            }
            return true;
        }

        // Check if a column is empty
        private bool IsColumnEmpty(int column)
        {
            foreach (UIElement child in VideoGrid.Children)
            {
                if (Grid.GetColumn(child) == column)
                {
                    return false; // Column contains at least one element
                }
            }
            return true;
        }

        private void RecordBtn_Click(object sender, RoutedEventArgs e)
        {
            IsRecording = !IsRecording;

            if (IsRecording)
            {
                RecordBtn.Content = "Stop Recording";

                //Create a new subfolder in the User Videos folder for all recordings
                string path = System.IO.Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) + "/ZCRTSP-Recordings").FullName;
                string currentdate = DateTime.Now.ToString("dd-MM-yyyy-HH-mm-ss");

                foreach (var url in cameraUrls)
                {
                    string MainStreamURL = url;

                    //If the URL is a substream, change it to a mainstream
                    if (url.EndsWith("2"))
                    {
                        MainStreamURL = url.Substring(0, url.Length - 1) + "1";
                    }

                    //Create the specific camera recording folder
                    string local_cam_path = Directory.CreateDirectory(path + "/" + MainStreamURL.Substring(MainStreamURL.LastIndexOf('@') + 1)).FullName;

                    using (Process process = new Process())
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            WindowStyle = ProcessWindowStyle.Hidden,
                            FileName = "ffmpeg.exe",
                            Arguments = "-rtsp_transport tcp -i " + MainStreamURL + " -c copy -fflags nobuffer -flags low_delay -vsync 0 -buffer_size 512k " + local_cam_path + "/" + currentdate + ".avi"
                        };
                        process.StartInfo = startInfo;
                        process.Start();
                    }
                }
            }
            else
            {
                RecordBtn.Content = "Start Recording";

                foreach (Process process in RecordingProcesses)
                {
                    process.Kill();
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //Kill all FFMPEG instances before closing the program completely
            foreach (Process process in RecordingProcesses)
            {
                process.Kill();
            }
        }

        private void ResetCamerasBtn_Click(object sender, RoutedEventArgs e)
        {
            if (dispatcherTimer.IsEnabled)
            {
                dispatcherTimer.Stop();
                ResetCamerasBtn.Content = "Start Reset Cameras Timer";
            }
            StartResetTimer();
        }

        private void ToggleFullscreenBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //Exit fullscreen is Escape key is pressed, and if fullscreen is active
            if (e.Key == Key.Escape)
            {
                if (this.WindowStyle == WindowStyle.None)
                {
                    ToggleFullscreen();
                }
                e.Handled = true;
            }
        }
    }
}