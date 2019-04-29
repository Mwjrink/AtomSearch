using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AtomSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool CurrentlyShown => Visibility == Visibility.Visible;

        private event Action<Result> ResultClickedEvent;

        public MainWindow()
        {
            //SourceInitialized += MainWindow_SourceInitialized;

            App.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            // ANIMATION REQUESTS
            MainWindowVM.RequestActivateEvent += async () => await ActivateAndFocus().ConfigureAwait(false);
            MainWindowVM.RequestMinimizeEvent += async () => await DeActivate(true).ConfigureAwait(false);
            MainWindowVM.RequestToggleShowEvent += async () => await ToggleShow().ConfigureAwait(false);
            MainWindowVM.RequestChangeResultsVisibilityEvent += async value => await ChangeResultsVisibility(value).ConfigureAwait(false);
            LostKeyboardFocus += async (_, __) => await DeActivate().ConfigureAwait(false);

            // STANDARD REQUESTS
            MainWindowVM.RequestResetAnimationFrameRateEvent += ResetAnimationFrameRate;

            // CREATE THE DATACONTEXT
            //TODO move setting the framerate to a post initializeComponent method, then put it back into the .xaml Window.Resources 
            var vm = new MainWindowVM();

            ResultClickedEvent += vm.ResultClicked;

            // SET THE DATACONTEXT
            DataContext = vm;

            InitializeComponent();

            // This is down here because AtomSearch is null until InitializeComponent is called
            AtomSearch.PreviewKeyUp += (_, e) => vm.PreviewSearchBoxKeyUp(e.Key);

#if DEBUG
            Topmost = false;
#endif

            // SET THE DEFAULTS SO ANIMATIONS WORK PROPERLY
            ResultsBorder.Visibility = Visibility.Hidden;
            ResultsBlurEffectBorder.Visibility = Visibility.Hidden;

            //AtomSearchBorderScaleTransform.ScaleX = 0.8;
            //AtomSearchBorderScaleTransform.ScaleY = 0.8;
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Debug.Print("Unhandled exception: " + e.ToString());
        }

        public void ResetAnimationFrameRate(int value)
            => Resources["AnimationTargetFramerate"] = value;

        public async Task ActivateAndFocus()
        {
            Debug.Print("Activating");

            WindowState = WindowState.Normal;
            Activate();
            Show();
            //await ((Storyboard)Resources["ZoomInStoryBoard"]).BeginAsync(this).ConfigureAwait(true);
            AtomSearch.Focus();
        }

        public async Task ToggleShow()
        {
            if (CurrentlyShown)
                await DeActivate(true).ConfigureAwait(false);
            else
                await ActivateAndFocus().ConfigureAwait(false);
        }

        public async Task DeActivate(bool force = false)
        {
            if (!IsKeyboardFocusWithin || force)
            {
                //await ((Storyboard)Resources["ZoomOutStoryBoard"]).BeginAsync(this).ConfigureAwait(true);

                Hide();
            }
        }

        private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            ResultsView.SelectionMode = SelectionMode.Single;

            if (e.Key == Key.Up)
            {
                if (ResultsView.SelectedIndex > -1)
                    ResultsView.SelectedIndex--;

                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (ResultsView.SelectedIndex < ResultsView.Items.Count - 1)
                    ResultsView.SelectedIndex++;

                e.Handled = true;
            }

            try
            {
                ResultsView.ScrollIntoView(ResultsView.SelectedItem);
            }
            catch (Exception ex)
            {
                Debug.Print("Error scrolling selected with down arrow into view" + ex.Message);
            }
        }

        private async Task ChangeResultsVisibility(bool value)
        {
            if (value)
            {
                if (ResultsBorder.Visibility == Visibility.Hidden)
                {
                    ResultsBlurEffectBorder.Visibility = Visibility.Visible;
                    ResultsBorder.Visibility = Visibility.Visible;
                    //await ((Storyboard)ResultsBorder.Resources["ZoomInStoryBoard"]).BeginAsync(this).ConfigureAwait(true);
                }
            }
            else
            {
                if (ResultsBorder.Visibility == Visibility.Visible)
                {
                    ResultsBlurEffectBorder.Visibility = Visibility.Hidden;
                    //await ((Storyboard)ResultsBorder.Resources["ZoomOutStoryBoard"]).BeginAsync(this).ConfigureAwait(true);
                    ResultsBorder.Visibility = Visibility.Hidden;
                }
            }
        }

        private void Result_MouseDown(object sender, MouseButtonEventArgs e)
            => ResultClickedEvent?.Invoke((Result)((Grid)sender).DataContext);

        //private void MainWindow_SourceInitialized(object sender, EventArgs e)
        //{
        //    if (!NativeMethods.DwmIsCompositionEnabled())
        //        return;

        //    var hwnd = new WindowInteropHelper(this).Handle;

        //    var hwndSource = HwndSource.FromHwnd(hwnd);
        //    var sizeFactor = hwndSource.CompositionTarget.TransformToDevice.Transform(new Vector(1.0, 1.0));

        //    // Pretty sure this is already covered by the xaml code
        //    Background = System.Windows.Media.Brushes.Transparent;
        //    hwndSource.CompositionTarget.BackgroundColor = Colors.Transparent;

        //    using (var path = new GraphicsPath())
        //    {
        //        path.AddEllipse(0, 0, (int)(ActualWidth * sizeFactor.X), (int)(ActualHeight * sizeFactor.Y));

        //        using (var region = new Region(path))
        //        using (var graphics = Graphics.FromHwnd(hwnd))
        //        {
        //            var hRgn = region.GetHrgn(graphics);

        //            var blur = new NativeMethods.DWM_BLURBEHIND
        //            {
        //                dwFlags = NativeMethods.DWM_BB.DWM_BB_ENABLE | NativeMethods.DWM_BB.DWM_BB_BLURREGION | NativeMethods.DWM_BB.DWM_BB_TRANSITIONMAXIMIZED,
        //                fEnable = true,
        //                hRgnBlur = hRgn,
        //                fTransitionOnMaximized = true
        //            };

        //            NativeMethods.DwmEnableBlurBehindWindow(hwnd, ref blur);

        //            region.ReleaseHrgn(hRgn);
        //        }
        //    }
        //}

        //[SuppressUnmanagedCodeSecurity]
        //private static class NativeMethods
        //{
        //    [StructLayout(LayoutKind.Sequential)]
        //    public struct DWM_BLURBEHIND
        //    {
        //        public DWM_BB dwFlags;
        //        public bool fEnable;
        //        public IntPtr hRgnBlur;
        //        public bool fTransitionOnMaximized;
        //    }

        //    [Flags]
        //    public enum DWM_BB
        //    {
        //        DWM_BB_ENABLE = 1,
        //        DWM_BB_BLURREGION = 2,
        //        DWM_BB_TRANSITIONMAXIMIZED = 4
        //    }

        //    [DllImport("dwmapi.dll", PreserveSig = false)]
        //    public static extern bool DwmIsCompositionEnabled();

        //    [DllImport("dwmapi.dll", PreserveSig = false)]
        //    public static extern void DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND blurBehind);
        //}

    }
}
