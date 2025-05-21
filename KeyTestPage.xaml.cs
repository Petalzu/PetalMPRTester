using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace PetalMPRTester
{
    public sealed partial class KeyTestPage : Page
    {
        private readonly SolidColorBrush blueBrush = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);

        public KeyTestPage()
        {
            this.InitializeComponent();
            this.PointerPressed += KeyTestPage_PointerPressed;
            this.PointerReleased += KeyTestPage_PointerReleased;
            this.PointerWheelChanged += KeyTestPage_PointerWheelChanged;
            this.PointerExited += KeyTestPage_PointerExited;
        }

        private void SetEllipseFill(Ellipse ellipse, bool isPressed)
        {
            ellipse.Fill = isPressed ? blueBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        private void KeyTestPage_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed)
                SetEllipseFill(EllipseLeft, true);
            if (props.IsRightButtonPressed)
                SetEllipseFill(EllipseRight, true);
            if (props.IsMiddleButtonPressed)
                SetEllipseFill(EllipseMiddle, true);
            if (props.IsXButton1Pressed)
                SetEllipseFill(EllipseXButton1, true);
            if (props.IsXButton2Pressed)
                SetEllipseFill(EllipseXButton2, true);
        }

        // 新增方法
        private void KeyTestPage_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            SetEllipseFill(EllipseLeft, false);
            SetEllipseFill(EllipseRight, false);
            SetEllipseFill(EllipseMiddle, false);
            SetEllipseFill(EllipseXButton1, false);
            SetEllipseFill(EllipseXButton2, false);
            SetEllipseFill(EllipseWheelUp, false);
            SetEllipseFill(EllipseWheelDown, false);
        }

        private void KeyTestPage_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            SetEllipseFill(EllipseLeft, false);
            SetEllipseFill(EllipseRight, false);
            SetEllipseFill(EllipseMiddle, false);
            SetEllipseFill(EllipseXButton1, false);
            SetEllipseFill(EllipseXButton2, false);
        }

        private void KeyTestPage_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
            if (delta > 0)
            {
                SetEllipseFill(EllipseWheelUp, true);
                Task.Delay(120).ContinueWith(_ =>
                    DispatcherQueue.TryEnqueue(() => SetEllipseFill(EllipseWheelUp, false)));
            }
            else if (delta < 0)
            {
                SetEllipseFill(EllipseWheelDown, true);
                Task.Delay(120).ContinueWith(_ =>
                    DispatcherQueue.TryEnqueue(() => SetEllipseFill(EllipseWheelDown, false)));
            }
        }
    }
}