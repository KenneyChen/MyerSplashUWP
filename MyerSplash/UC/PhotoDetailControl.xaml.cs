using JP.Utils.Debug;
using JP.Utils.UI;
using MyerSplash.Model;
using MyerSplashCustomControl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Threading;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace MyerSplash.UC
{
    public sealed partial class PhotoDetailControl : UserControl, INotifyPropertyChanged
    {
        public event Action OnHideControl;
        public event PropertyChangedEventHandler PropertyChanged;

        private Compositor _compositor;
        private Visual _detailGridVisual;
        private Visual _borderGridVisual;
        private Visual _downloadBtnVisual;
        private Visual _shareBtnVisual;
        private Visual _infoGridVisual;
        private Visual _downloadingHintGridVisual;
        private Visual _loadingPath;
        private Visual _okVisual;

        private CancellationTokenSource _cts;

        private UnsplashImageBase _currentImage;
        public UnsplashImageBase CurrentImage
        {
            get
            {
                return _currentImage;
            }
            set
            {
                if (_currentImage != value)
                {
                    _currentImage = value;
                    RaisePropertyChanged(nameof(CurrentImage));
                }
            }
        }

        private DataTransferManager _dataTransferManager;

        public bool IsShown { get; set; }

        private void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public PhotoDetailControl()
        {
            InitializeComponent();
            InitComposition();
            this.DataContext = this;
            this.SizeChanged += PhotoDetailControl_SizeChanged;
            _dataTransferManager = DataTransferManager.GetForCurrentView();
            _dataTransferManager.DataRequested += _dataTransferManager_DataRequested;
        }

        private void PhotoDetailControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var windowRect = Window.Current.Bounds;

            var R1 = 3d / 2d;
            var R2 = windowRect.Width / (windowRect.Height - 200);

            var drawingWidth = 0d;
            var drawingHeight = 0d;

            var height1 = (R2 / R1) * (windowRect.Height - 200);
            var height2 = (windowRect.Height - 200);

            drawingHeight = Math.Min(height1, height2) - 20f;
            drawingWidth = drawingHeight * R1;

            if (this.DetailContentGrid.Width != drawingWidth)
            {
                this.DetailContentGrid.Width = drawingWidth;
            }
            if (this.DetailContentGrid.Height != drawingHeight + 100)
            {
                this.DetailContentGrid.Height = drawingHeight + 100;
            }
        }

        public Rect GetRect()
        {
            return new Rect(0, 0, DetailContentGrid.Width, DetailContentGrid.Height - 100);
        }

        private async void _dataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            DataRequestDeferral deferral = args.Request.GetDeferral();
            sender.TargetApplicationChosen += (s, e) =>
              {
                  deferral.Complete();
              };
            await CurrentImage.SetDataRequestData(args.Request);
            deferral.Complete();
        }

        private void InitComposition()
        {
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            _detailGridVisual = ElementCompositionPreview.GetElementVisual(DetailGrid);
            _borderGridVisual = ElementCompositionPreview.GetElementVisual(MaskBorder);
            _downloadBtnVisual = ElementCompositionPreview.GetElementVisual(DownloadBtn);
            _infoGridVisual = ElementCompositionPreview.GetElementVisual(InfoGrid);
            _downloadingHintGridVisual = ElementCompositionPreview.GetElementVisual(LoadingHintGrid);
            _loadingPath = ElementCompositionPreview.GetElementVisual(LoadingPath);
            _okVisual = ElementCompositionPreview.GetElementVisual(OKBtn);
            _shareBtnVisual = ElementCompositionPreview.GetElementVisual(ShareBtn);

            ResetVisualInitState();
        }

        private void ResetVisualInitState()
        {
            _infoGridVisual.Offset = new Vector3(0f, -100f, 0);
            _downloadBtnVisual.Offset = new Vector3(100f, 0f, 0f);
            _shareBtnVisual.Offset = new Vector3(150f, 0f, 0f);
            _detailGridVisual.Opacity = 0;
            _okVisual.Offset = new Vector3(100f, 0f, 0f);
            _downloadingHintGridVisual.Offset = new Vector3(100f, 0f, 0f);

            PhotoSV.ChangeView(null, 0, null);
            StartLoadingAnimation();
        }

        private void MaskBorder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            HideDetailControl();
        }

        public void HideDetailControl()
        {
            ToggleDownloadBtnAnimation(false);
            ToggleShareBtnAnimation(false);
            ToggleOkBtnAnimation(false);
            ToggleDownloadingBtnAnimation(false);

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            ToggleInfoGridAnimation(false);
            batch.Completed += (s, ex) =>
            {
                OnHideControl?.Invoke();
                ToggleDetailGridAnimation(false);
            };
            batch.End();
        }

        public void ToggleDetailGridAnimation(bool show)
        {
            IsShown = show;

            this.Visibility = Visibility.Visible;

            var fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
            fadeAnimation.InsertKeyFrame(1f, show ? 1f : 0f);
            fadeAnimation.Duration = TimeSpan.FromMilliseconds(show ? 700 : 300);
            fadeAnimation.DelayTime = TimeSpan.FromMilliseconds(show ? 400 : 0);

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _detailGridVisual.StartAnimation("Opacity", fadeAnimation);

            if (show)
            {
                ToggleDownloadBtnAnimation(true);
                ToggleShareBtnAnimation(true);
                ToggleInfoGridAnimation(true);
            }

            batch.Completed += (sender, e) =>
            {
                if (!show)
                {
                    ResetVisualInitState();
                    this.Visibility = Visibility.Collapsed;
                }
            };
            batch.End();
        }

        private void ToggleDownloadBtnAnimation(bool show)
        {
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, new Vector3(show ? 0f : 100f, 0f, 0f));
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(1000);
            offsetAnimation.DelayTime = TimeSpan.FromMilliseconds(show ? 500 : 0);

            _downloadBtnVisual.StartAnimation("Offset", offsetAnimation);
        }

        private void ToggleShareBtnAnimation(bool show)
        {
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, new Vector3(show ? 0f : 150f, 0f, 0f));
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(show ? 1000 : 400);
            offsetAnimation.DelayTime = TimeSpan.FromMilliseconds(show ? 400 : 0);

            _shareBtnVisual.StartAnimation("Offset", offsetAnimation);
        }

        private void ToggleInfoGridAnimation(bool show)
        {
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, show ? 0f : -100f, 0f));
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(500);
            offsetAnimation.DelayTime = TimeSpan.FromMilliseconds(show ? 500 : 0);

            _infoGridVisual.StartAnimation("Offset", offsetAnimation);
        }

        private void DetailGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.DetailContentGrid.Height = this.DetailContentGrid.ActualWidth / 1.5 + 100;
            this.DetailContentGrid.Clip = new RectangleGeometry()
            { Rect = new Rect(0, 0, DetailContentGrid.ActualWidth, DetailContentGrid.Height) };
        }

        #region Download animation
        private void ToggleDownloadingBtnAnimation(bool show)
        {
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, new Vector3(show ? 0f : 100f, 0f, 0f));
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(500);
            offsetAnimation.DelayTime = TimeSpan.FromMilliseconds(show ? 200 : 0);

            _downloadingHintGridVisual.StartAnimation("Offset", offsetAnimation);
        }

        private void ToggleOkBtnAnimation(bool show)
        {
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, new Vector3(show ? 0f : 100f, 0f, 0f));
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(500);
            offsetAnimation.DelayTime = TimeSpan.FromMilliseconds(show ? 200 : 0);

            _okVisual.StartAnimation("Offset", offsetAnimation);
        }

        private async void DownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleDownloadBtnAnimation(false);
            ToggleDownloadingBtnAnimation(true);

            try
            {
                _cts = new CancellationTokenSource();
                await this.CurrentImage.DownloadFullImageAsync(_cts.Token);
                ToggleDownloadingBtnAnimation(false);

                //Still in this page
                if (IsShown)
                {
                    ToggleOkBtnAnimation(true);
                    ToastService.SendToast("Saved :D", TimeSpan.FromMilliseconds(1000));
                }
            }
            catch (OperationCanceledException)
            {
                ToggleDownloadBtnAnimation(true);
                ToggleDownloadingBtnAnimation(false);
                ToggleOkBtnAnimation(false);
                ToastService.SendToast("Cancelled", TimeSpan.FromMilliseconds(1000));
            }
            catch (Exception ex)
            {
                var task = Logger.LogAsync(ex);
                ToggleDownloadBtnAnimation(true);
                ToggleDownloadingBtnAnimation(false);
                ToggleOkBtnAnimation(false);
                ToastService.SendToast($"Exception throws.{ex.Message}", TimeSpan.FromMilliseconds(1000));
            }
        }

        private void StartLoadingAnimation()
        {
            var rotateAnimation = _compositor.CreateScalarKeyFrameAnimation();
            rotateAnimation.InsertKeyFrame(1, 360f, _compositor.CreateLinearEasingFunction());
            rotateAnimation.Duration = TimeSpan.FromMilliseconds(800);
            rotateAnimation.IterationBehavior = AnimationIterationBehavior.Forever;

            _loadingPath.CenterPoint = new Vector3((float)LoadingPath.ActualWidth / 2, (float)LoadingPath.ActualHeight / 2, 0f);

            _loadingPath.RotationAngleInDegrees = 0f;
            _loadingPath.StopAnimation("RotationAngleInDegrees");
            _loadingPath.StartAnimation("RotationAngleInDegrees", rotateAnimation);
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }
        #endregion

        private void DetailGrid_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (Math.Abs(e.Cumulative.Translation.Y) > 30)
            {
                MaskBorder_Tapped(null, null);
            }
        }

        private void InfoPlaceHolderGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var grid = sender as Grid;
            grid.Clip = new RectangleGeometry() { Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
        }

        private async void LargeImage_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            var image = (sender as FrameworkElement).DataContext as UnsplashImageBase;
            var file = await StorageFile.GetFileFromPathAsync(image.ListImageBitmap.LocalPath);
            if (file != null)
            {
                args.Data.SetStorageItems(new List<StorageFile>() { file });
            }
            args.Data.RequestedOperation = DataPackageOperation.Copy;
            args.Data.SetText(image.ShareText);
            args.Data.SetWebLink(new Uri(image.GetSaveImageUrlFromSettings()));
        }
    }
}
