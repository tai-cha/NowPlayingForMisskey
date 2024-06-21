using iTunesLib;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Web;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NowPlayingForMisskey
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {

        private iTunesApp _iTunesApp;
        private _IiTunesEvents_OnPlayerPlayEventEventHandler _playEventHandler;
        private _IiTunesEvents_OnPlayerPlayingTrackChangedEventEventHandler _changedEventHandler;
        private IITTrack _currentTrack;
        private StorageFile _artworkFile;

        public MainWindow()
        {
            this.InitializeComponent();
            GetCurrentAppWindow().Resize(new Windows.Graphics.SizeInt32(1000, 450));

            InitializeiTunes();
            RegisterQuit();
            GetTrackInfo(null);
        }

        private void InitializeiTunes()
        {
            Debug.WriteLine("Initializing iTunes...");
            _iTunesApp = new iTunesApp();
            _playEventHandler = new _IiTunesEvents_OnPlayerPlayEventEventHandler(OnPlayed);
            _changedEventHandler = new _IiTunesEvents_OnPlayerPlayingTrackChangedEventEventHandler(OnTrackChanged);

            _iTunesApp.OnPlayerPlayEvent += _playEventHandler;
            _iTunesApp.OnPlayerPlayingTrackChangedEvent += _changedEventHandler;
            Debug.WriteLine("iTunes initialized and event handlers registered.");
        }
        private AppWindow GetCurrentAppWindow()
        {
            //Windowのハンドルを取得する
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            //hwndでWindowIdを取得する
            WindowId winId = Win32Interop.GetWindowIdFromWindow(hwnd);
            //WindowIdでAppWindow objectを取得して返す
            return AppWindow.GetFromWindowId(winId);
        }

        private void RegisterQuit()
        {
            AppWindow thisAppWindow = GetCurrentAppWindow();
            if (thisAppWindow != null)
            {
                thisAppWindow.Destroying += (s, e) =>
                {
                    OnQuit();
                };
            }

        }

        private void OnQuit()
        {
            try
            {
                if (_currentTrack != null)
                {
                    Marshal.ReleaseComObject(_currentTrack);
                    _currentTrack = null;
                }
                _iTunesApp.OnPlayerPlayEvent -= _playEventHandler;
                _iTunesApp.OnPlayerPlayingTrackChangedEvent -= _changedEventHandler;
                Marshal.ReleaseComObject(_iTunesApp);
                _iTunesApp = null;
            }
            catch (Exception)
            {
            }


            Debug.WriteLine("Quit unhook done.");
        }

        private void OnPlayed(object iTrack)
        {
            Debug.WriteLine("PlayedCalled");
            if (_currentTrack != null)
            {
                Marshal.ReleaseComObject(_currentTrack);
            }
            this.DispatcherQueue.TryEnqueue(() => GetTrackInfo(iTrack));
        }

        private void OnTrackChanged(object iTrack)
        {
            Debug.WriteLine("TrackChangeCalled");
            if (_currentTrack != null)
            {
                Marshal.ReleaseComObject(_currentTrack);
            }
            this.DispatcherQueue.TryEnqueue(() => GetTrackInfo(iTrack));
        }

        async private void GetTrackInfo(object iTrack)
        {
            try
            {
                _currentTrack = _iTunesApp.CurrentTrack;

                if (_currentTrack != null)
                {
                    // 再生中の曲情報を取得
                    string trackName = _currentTrack.Name;
                    string artistName = _currentTrack.Artist;
                    string albumName = _currentTrack.Album;

                    // UIに表示
                    TrackNameTextBlock.Text = $"Track: {trackName}";
                    ArtistNameTextBlock.Text = $"Artist: {artistName}";
                    AlbumNameTextBlock.Text = $"Album: {albumName}";

                    IITArtwork artwork = _currentTrack.Artwork[1];
                    if (artwork != null)
                    {
                        string tempPath = Path.Combine(Path.GetTempPath(), $"artwork-{Guid.NewGuid()}.png");
                        artwork.SaveArtworkToFile(tempPath);

                        BitmapImage bitmapImage = new BitmapImage();
                        _artworkFile = await StorageFile.GetFileFromPathAsync(tempPath);
                        IRandomAccessStream fileStream = await _artworkFile.OpenAsync(FileAccessMode.Read);
                        {
                            await bitmapImage.SetSourceAsync(fileStream);
                        }
                        ArtworkImage.Source = bitmapImage;
                    }
                    else
                    {
                        ArtworkImage.Source = new BitmapImage();
                    }
                }
                else
                {
                    TrackNameTextBlock.Text = "No track is currently playing.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        void CopyArtworkToClipBoard()
        {
            if (_artworkFile != null)
            {
                try
                {
                    var dataPackage = new DataPackage();
                    dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(_artworkFile));
                    Clipboard.SetContent(dataPackage);
                    Clipboard.Flush();

                    ShowClipboardNotification(_artworkFile.Path);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }

        private void ShowClipboardNotification(string imagePath)
        {
            var notificationBuilder = new AppNotificationBuilder()
                .AddText("アートワークがクリップボードにコピーされました。")
                .AddText("ノートに貼り付けることで添付できます。", new AppNotificationTextProperties().SetMaxLines(2))
                .SetHeroImage(new Uri(imagePath));

            var notificationManager = AppNotificationManager.Default;
            notificationManager.Show(notificationBuilder.BuildNotification());
        }


        void OnNoteClick(object sender, RoutedEventArgs e)
        {
            if (_currentTrack != null)
            {
                CopyArtworkToClipBoard();

                string trackName = _currentTrack.Name;
                string artistName = _currentTrack.Artist;
                string albumName = _currentTrack.Album;
                string shareText = HttpUtility.UrlEncode($"Track: {trackName}\nArtist: {artistName}\nAlbum: {albumName}\n#NowPlaying");
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri($"https://misskey-hub.net/share?text={shareText}&manualInstance=mi.taichan.site"));
            }
        }
    }
}
