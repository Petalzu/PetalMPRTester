using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace PetalMPRTester
{
    public sealed partial class RealTimeSamplingPage : Page
    {
        private bool isSampling = false;
        private CancellationTokenSource? cts;
        private List<System.Numerics.Vector2> points = new();

        public RealTimeSamplingPage()
        {
            this.InitializeComponent();
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private static int GetScreenWidth() => GetSystemMetrics(0);
        private static int GetScreenHeight() => GetSystemMetrics(1);
        private int GetMaxPoints()
        {
            if (int.TryParse(MaxPointsBox.Text, out int val) && val > 0)
                return val;
            return 50;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            isSampling = true;
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            points.Clear();
            ScatterCanvas.Children.Clear();
            cts = new CancellationTokenSource();
            _ = StartDrawingAsync(cts.Token);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            isSampling = false;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            cts?.Cancel();
        }

        private async Task StartDrawingAsync(CancellationToken token)
        {
            int screenWidth = GetScreenWidth();
            int screenHeight = GetScreenHeight();

            while (isSampling && !token.IsCancellationRequested)
            {
                var pt = GetCursorPosition();
                points.Add(new System.Numerics.Vector2(pt.X, pt.Y));
                DrawScatter(screenWidth, screenHeight);
                await Task.Delay(1);
            }
        }

        private void DrawScatter(int screenWidth, int screenHeight)
        {
            ScatterCanvas.Children.Clear();

            double width = ScatterCanvas.ActualWidth > 0 ? ScatterCanvas.ActualWidth : ScatterCanvas.Width;
            double height = ScatterCanvas.ActualHeight > 0 ? ScatterCanvas.ActualHeight : ScatterCanvas.Height;
            if (width == 0 || height == 0) return;

            int maxPoints = GetMaxPoints();
            int startIdx = points.Count > maxPoints ? points.Count - maxPoints : 0;

            for (int i = startIdx; i < points.Count; i++)
            {
                var pt = points[i];
                double px = Math.Clamp(pt.X / screenWidth * width, 0, width);
                double py = Math.Clamp(pt.Y / screenHeight * height, 0, height);

                var ellipse = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Microsoft.UI.Colors.White),
                    Opacity = 0.9
                };
                Canvas.SetLeft(ellipse, px - 4);
                Canvas.SetTop(ellipse, py - 4);
                ScatterCanvas.Children.Add(ellipse);
            }
        }
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private static POINT GetCursorPosition()
        {
            GetCursorPos(out POINT pt);
            return pt;
        }
    }
}