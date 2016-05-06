﻿using JP.API;
using JP.Utils.UI;
using MyerSplash.Common;
using MyerSplash.Model;
using MyerSplash.ViewModel;
using MyerSplashCustomControl;
using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace MyerSplash.View
{
    public sealed partial class MainPage : BindablePage
    {
        private MainViewModel MainVM { get; set; }

        private Compositor _compositor;
        private Visual _loadingVisual;
        private Visual _refreshVisual;
        private Visual _containerVisual;
        private Visual _drawerVisual;
        private Visual _drawerMaskVisual;

        private UnsplashImage _currentImg;

        private int _zindex = 1;

        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(MainPage), new PropertyMetadata(false, OnLoadingPropertyChanged));

        public static void OnLoadingPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var page = d as MainPage;
            if (!(bool)e.NewValue)
            {
                page.HideLoading();
            }
            else page.ShowLoading();
        }

        public bool DrawerOpended
        {
            get { return (bool)GetValue(DrawerOpendedProperty); }
            set { SetValue(DrawerOpendedProperty, value); }
        }

        public static readonly DependencyProperty DrawerOpendedProperty =
            DependencyProperty.Register("DrawerOpended", typeof(bool), typeof(MainPage), new PropertyMetadata(false, OnDrawerOpenedPropertyChanged));

        public static void OnDrawerOpenedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var page = d as MainPage;
            page.ToggleDrawerAnimation((bool)e.NewValue);
            page.ToggleDrawerMaskAnimation((bool)e.NewValue);
        }

        public MainPage()
        {
            this.InitializeComponent();
            this.DataContext = MainVM = new MainViewModel();
            this.SizeChanged += MainPage_SizeChanged;
            this.Loaded += MainPage_Loaded;

            InitComposition();
            InitBinding();
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            _loadingVisual.Offset = new Vector3(0f, -60f, 0f);
            _drawerMaskVisual.Opacity = 0;
            _drawerVisual.Offset = new Vector3(-(float)Window.Current.Bounds.Width, 0f, 0f);
            
            DetailControl.Visibility = Visibility.Collapsed;
            DrawerMaskBorder.Visibility = Visibility.Collapsed;
        }

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(!DrawerOpended)
            {
                _drawerVisual.Offset = new Vector3(-(float)Window.Current.Bounds.Width, 0f, 0f);
            }
        }

        private void InitBinding()
        {
            var b = new Binding()
            {
                Source = MainVM,
                Path = new PropertyPath("IsRefreshing"),
                Mode = BindingMode.TwoWay,
            };
            this.SetBinding(IsLoadingProperty, b);

            var b2 = new Binding()
            {
                Source = MainVM,
                Path = new PropertyPath("DrawerOpened"),
                Mode = BindingMode.TwoWay,
            };
            this.SetBinding(DrawerOpendedProperty, b2);
        }

        private void InitComposition()
        {
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            _loadingVisual = ElementCompositionPreview.GetElementVisual(LoadingGrid);
            _refreshVisual = ElementCompositionPreview.GetElementVisual(RefreshSymbol);
            _drawerVisual = ElementCompositionPreview.GetElementVisual(DrawerControl);
            _drawerMaskVisual = ElementCompositionPreview.GetElementVisual(DrawerMaskBorder);
        }

        #region Loading Animation
        private void ShowLoading()
        {
            var showAnimation = _compositor.CreateVector3KeyFrameAnimation();
            showAnimation.InsertKeyFrame(1, new Vector3(0f, 50f, 0f));
            showAnimation.Duration = TimeSpan.FromMilliseconds(500);

            var linearEasingFunc = _compositor.CreateLinearEasingFunction();

            var rotateAnimation = _compositor.CreateScalarKeyFrameAnimation();
            rotateAnimation.InsertKeyFrame(1, 3600f, linearEasingFunc);
            rotateAnimation.Duration = TimeSpan.FromMilliseconds(10000);
            rotateAnimation.IterationBehavior = AnimationIterationBehavior.Forever;

            _refreshVisual.CenterPoint = new Vector3((float)RefreshSymbol.ActualWidth / 2, (float)RefreshSymbol.ActualHeight / 2, 0f);
            _refreshVisual.RotationAngleInDegrees = 0;

            _refreshVisual.StopAnimation("RotationAngleInDegrees");
            _refreshVisual.StartAnimation("RotationAngleInDegrees", rotateAnimation);
            _loadingVisual.StartAnimation("Offset", showAnimation);
        }

        private void HideLoading()
        {
            var showAnimation = _compositor.CreateScalarKeyFrameAnimation();
            showAnimation.InsertKeyFrame(1, -60f);
            showAnimation.Duration = TimeSpan.FromMilliseconds(500);

            _loadingVisual.StartAnimation("Offset.y", showAnimation);
        }
        #endregion

        #region Drawer
        private void ToggleDrawerAnimation(bool show)
        {
            var offsetAnim = _compositor.CreateScalarKeyFrameAnimation();
            offsetAnim.InsertKeyFrame(1f, show ? 0f : -(float)Window.Current.Bounds.Width);
            offsetAnim.Duration = TimeSpan.FromMilliseconds(300);

            _drawerVisual.StartAnimation("Offset.x", offsetAnim);
        }

        private void ToggleDrawerMaskAnimation(bool show)
        {
            if (show) DrawerMaskBorder.Visibility = Visibility.Visible;

            var fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
            fadeAnimation.InsertKeyFrame(1f, show ? 1f : 0f,_compositor.CreateLinearEasingFunction());
            fadeAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _drawerMaskVisual.StartAnimation("Opacity", fadeAnimation);
            batch.Completed += (sender, e) =>
              {
                  if (!show) DrawerMaskBorder.Visibility = Visibility.Collapsed;
              };
            batch.End();
        }
        #endregion

        #region List Animation
        private void AdaptiveGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            int index = args.ItemIndex;

            if (!args.InRecycleQueue)
            {
                args.ItemContainer.Loaded -= ItemContainer_Loaded;
                args.ItemContainer.Loaded += ItemContainer_Loaded;
            }
        }

        private void ItemContainer_Loaded(object sender, RoutedEventArgs e)
        {
            var itemsPanel = (ItemsWrapGrid)ImageGridView.ItemsPanelRoot;
            var itemContainer = (GridViewItem)sender;
            var itemIndex = ImageGridView.IndexFromContainer(itemContainer);

            // Don't animate if we're not in the visible viewport
            if (itemIndex >= itemsPanel.FirstVisibleIndex && itemIndex <= itemsPanel.LastVisibleIndex)
            {
                var itemVisual = ElementCompositionPreview.GetElementVisual(itemContainer);

                itemVisual.Opacity = 0f;
                itemVisual.Offset = new Vector3(50, 0, 0);

                // Create KeyFrameAnimations
                var offsetAnimation = _compositor.CreateScalarKeyFrameAnimation();
                offsetAnimation.InsertExpressionKeyFrame(1f, "0");
                offsetAnimation.Duration = TimeSpan.FromMilliseconds(700);
                offsetAnimation.DelayTime = TimeSpan.FromMilliseconds((itemIndex * 100));

                var fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.InsertExpressionKeyFrame(1f, "1");
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(700);
                fadeAnimation.DelayTime = TimeSpan.FromMilliseconds(itemIndex * 100);

                // Start animations
                itemVisual.StartAnimation("Offset.X", offsetAnimation);
                itemVisual.StartAnimation("Opacity", fadeAnimation);
            }
            itemContainer.Loaded -= ItemContainer_Loaded;
        }
        #endregion

        private void ImageGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            DetailControl.Visibility = Visibility.Visible;

            var item = e.ClickedItem;
            var container = ImageGridView.ContainerFromItem(item) as FrameworkElement;
            Canvas.SetZIndex(container, ++_zindex);

            _containerVisual = ElementCompositionPreview.GetElementVisual(container);

            _currentImg = item as UnsplashImage;
            DetailControl.UnsplashImage = _currentImg;

            var currentPos = container.TransformToVisual(DetailControl).TransformPoint(new Point(0, 0));
            var targetPos = DetailControl.ContentGrid.TransformToVisual(DetailControl).TransformPoint(new Point(0, 0));
            var targetRatio = DetailControl.ContentGrid.ActualWidth / container.ActualWidth;
            var targetOffsetX = targetPos.X - currentPos.X;
            var targetOffsetY = targetPos.Y - currentPos.Y;

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            MoveItemAnimation(new Vector3((float)targetOffsetX, (float)targetOffsetY, 0f), (float)targetRatio);
            DetailControl.ToggleDetailGridAnimation(true);
            batch.Completed += (senderx, ex) =>
            {

            };
            batch.End();
        }

        private void MoveItemAnimation(Vector3 targetOffset, float widthRatio)
        {
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, targetOffset);
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(500);

            var scaleAnimation = _compositor.CreateScalarKeyFrameAnimation();
            scaleAnimation.InsertKeyFrame(1f, widthRatio);
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(500);

            var fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
            fadeAnimation.InsertKeyFrame(1f, 0.5f);
            fadeAnimation.Duration = TimeSpan.FromMilliseconds(500);

            _containerVisual.StartAnimation("Offset", offsetAnimation);
            _containerVisual.StartAnimation("Scale.x", scaleAnimation);
            _containerVisual.StartAnimation("Scale.y", scaleAnimation);
            //_containerVisual.StartAnimation("Opacity", fadeAnimation);
        }

        private void HideItemDetailAnimation()
        {
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, new Vector3(0, 0, 0));
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(500);

            var scaleAnimation = _compositor.CreateScalarKeyFrameAnimation();
            scaleAnimation.InsertKeyFrame(1f, 1f);
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(500);

            _containerVisual.StartAnimation("Offset", offsetAnimation);
            _containerVisual.StartAnimation("Scale.x", scaleAnimation);
            _containerVisual.StartAnimation("Scale.y", scaleAnimation);
        }

        private void StackPanel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ImageGridView.GetScrollViewer().ChangeView(null, 0, null);
        }

        private void DetailControl_OnHideControl()
        {
            HideItemDetailAnimation();
        }
    }
}
