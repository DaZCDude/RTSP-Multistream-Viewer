using System.Windows;
using System.Windows.Controls;
using LibVLCSharp.Shared;
using System.IO;
using LibVLCSharp.WPF;
using System.Windows.Media;
using System.Text;
using System.Windows.Input;

namespace ZCRTSPViewer_WPF
{
    public partial class MainWindow : Window
    {
        private int CameraAmount = 0;

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
        }

        private void OnFullscreenExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.WindowStyle == WindowStyle.None)
            {
                this.ResizeMode = ResizeMode.CanResize;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
            }
            else
            {
                this.ResizeMode = ResizeMode.NoResize;
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Normal;
                this.WindowState = WindowState.Maximized;
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

            List<string> cameraUrls = new List<string>();
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

            var videoView = new VideoView();
            videoView.Background = Brushes.Black;

            var _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVlc);

            //Force 16:9 Aspect Ratio
            _mediaPlayer.AspectRatio = "16:9";

            videoView.MediaPlayer = _mediaPlayer;

            var media = new Media(_libVlc, streamUrl, FromType.FromLocation);

            // Add options to reduce resolution and improve performance
            media.AddOption(":network-caching=3000"); // Adjust network caching for smoother playback
            media.AddOption(":avcodec-hw=any"); // Enable hardware acceleration
            media.AddOption(":swscale-mode=fast");  // Faster scaling (if resizing)
            media.AddOption(":avcodec-skiploopfilter=all"); // Skip CPU-heavy filters

            _mediaPlayer.Play(media);

            Grid.SetRow(videoView, row);
            Grid.SetColumn(videoView, column);

            CameraAmount += 1;
            VideoGrid.Children.Add(videoView);
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
    }
}