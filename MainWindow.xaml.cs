using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Windows.ApplicationModel.Resources;
using Windows.System;


namespace PetalMPRTester
{
    public sealed partial class MainWindow : Window
    {
        private int sampleCount = 1000;
        private bool infiniteMode = false;
        private IntPtr pollingCtx = IntPtr.Zero;
        private bool isTesting = false;
        private CancellationTokenSource? cts;



        public MainWindow()
        {
            this.InitializeComponent();

            // �Զ������������
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.Colors.Transparent;
            appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.Colors.Transparent;
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(CustomTitleBar);
            MainContentControl.Content = PollingTestGrid;

        }

        private List<double> rateHistory = new();
        private List<double> likelyRateHistory = new();

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog aboutDialog = new ContentDialog
            {
                Title = "����",
                Content = "PetalMPRTester\nPetal�����ѯ�ʼ�����\n���ߣ�Petalzu\n�汾��0.1.2\n",
                CloseButtonText = "�ر�",
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = ElementTheme.Default,
                Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"]
            };
            _ = aboutDialog.ShowAsync();
        }
        private void PollingTabButton_Click(object sender, RoutedEventArgs e)
        {
            // ��ʾ�ر��ʲ���ҳ��
            MainContentControl.Content = PollingTestGrid;
        }

        private void KeyTabButton_Click(object sender, RoutedEventArgs e)
        {
            // �л��� KeyTestPage ҳ��
            MainContentControl.Content = new KeyTestPage();
        }
        private void RealTimeSamplingTabButton_Click(object sender, RoutedEventArgs e)
        {
            MainContentControl.Content = new RealTimeSamplingPage();
        }

        private async void ShowErrorDialog(string message)
        {
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "����",
                Content = message,
                CloseButtonText = "ȷ��",
                XamlRoot = this.Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }

        private double GetMostLikelyRate()
        {
            if (rateHistory.Count == 0)
                return 0;

            var dict = new Dictionary<int, int>();
            foreach (var rate in rateHistory)
            {
                int key = (int)Math.Round(rate);
                if (key % 500 != 0) continue;
                if (dict.ContainsKey(key))
                    dict[key]++;
                else
                    dict[key] = 1;
            }
            if (dict.Count == 0) return 0; // û�б�׼����

            int mostLikely = 0;
            int maxCount = 0;
            foreach (var kv in dict)
            {
                if (kv.Value > maxCount)
                {
                    mostLikely = kv.Key;
                    maxCount = kv.Value;
                }
            }
            return mostLikely;
        }
        private double ParseLikelyRateText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            // ȥ�����з����ֺ�С����
            var sb = new System.Text.StringBuilder();
            foreach (char c in text)
            {
                if ((c >= '0' && c <= '9') || c == '.')
                    sb.Append(c);
            }
            if (double.TryParse(sb.ToString(), out double value))
                return value;
            return 0;
        }
        private void ShowPollingResult(NativeMethods.PollingResult result, bool append = false)
        {
            string msg;
            if (result.sampleCount == 0 || result.meanRate == 0)
            {
                msg = "�ɼ�ֹͣ��\n";
                double likely = GetMostLikelyRate();
                if (likely > 0)
                {
                    msg += $"���п��ܵĻر��ʣ�{likely} Hz\n";
                }
            }
            else
            {
                msg = $"��������{result.sampleCount}\n" +
                      $"��Ч��{result.validCount}\n" +
                      $"�쳣��{result.outlierCount}\n" +
                      $"��С�����{result.minInterval:F2} ��s\n" +
                      $"�������{result.maxInterval:F2} ��s\n" +
                      $"��ֵ��{result.meanInterval:F2} ��s\n" +
                      $"��λ����{result.medianInterval:F2} ��s\n" +
                      $"������{result.modeInterval:F2} ��s\n" +
                      $"��׼�{result.stdevInterval:F2} ��s\n" +
                      $"��ֵ���ʣ�{result.meanRate:F2} Hz\n" +
                      $"�������ʣ�{result.modeRate:F2} Hz\n" +
                      $"�Ʋ����ʣ�{result.likelyRateText}\n" +
                      $"�ȶ��Է�����{result.stabilityScore:F2}\n";
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                if (append)
                    ResultTextBlock.Text += msg;
                else
                    ResultTextBlock.Text = msg;

                // ֻ������Ч����ʱ����
                if (result.sampleCount > 0 && result.meanRate > 0)
                {
                    rateHistory.Add(result.meanRate);
                    likelyRateHistory.Add(ParseLikelyRateText(result.likelyRateText));
                    DrawChart();
                }
            });
        }

        private void DrawChart()
        {
            ChartCanvas.Children.Clear();
            double width = ChartCanvas.Width;
            double height = ChartCanvas.Height;

            if (rateHistory.Count == 0 && likelyRateHistory.Count == 0)
                return;

            double left = 40;
            double bottom = 30;
            double top = 10;
            double right = 60;

            double plotWidth = width - left - right;
            double plotHeight = height - top - bottom;

            var axisBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray);
            var axisThickness = 1.0;

            // X��
            var xAxis = new Line
            {
                X1 = left,
                Y1 = height - bottom,
                X2 = width - right,
                Y2 = height - bottom,
                Stroke = axisBrush,
                StrokeThickness = axisThickness
            };
            ChartCanvas.Children.Add(xAxis);

            // Y��
            var yAxis = new Line
            {
                X1 = left,
                Y1 = top,
                X2 = left,
                Y2 = height - bottom,
                Stroke = axisBrush,
                StrokeThickness = axisThickness
            };
            ChartCanvas.Children.Add(yAxis);


            // ���ƿ̶Ⱥͱ�ǩ
            int yTicks = 5;
            int xTicks = 5;
            int count = rateHistory.Count;
            int maxPoints = 100;
            int startIdx = count > maxPoints ? count - maxPoints : 0;

            // ����Y�᷶Χ
            double maxRate = 1;
            double minRate = double.MaxValue;
            for (int i = startIdx; i < count; i++)
            {
                if (rateHistory[i] > maxRate) maxRate = rateHistory[i];
                if (rateHistory[i] < minRate) minRate = rateHistory[i];
                if (i < likelyRateHistory.Count)
                {
                    if (likelyRateHistory[i] > maxRate) maxRate = likelyRateHistory[i];
                    if (likelyRateHistory[i] < minRate) minRate = likelyRateHistory[i];
                }
            }
            if (minRate == maxRate) maxRate = minRate + 1;


            // Y��̶�
            for (int i = 0; i <= yTicks; i++)
            {
                double frac = i / (double)yTicks;
                double y = top + frac * plotHeight;
                double rateValue = maxRate - frac * (maxRate - minRate);

                // �̶���
                var tick = new Line
                {
                    X1 = left - 5,
                    Y1 = y,
                    X2 = left,
                    Y2 = y,
                    Stroke = axisBrush,
                    StrokeThickness = 1
                };
                ChartCanvas.Children.Add(tick);

                // ��ǩ
                var label = new TextBlock
                {
                    Text = rateValue.ToString("F0"),
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                    FontSize = 12
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, y - 10);
                ChartCanvas.Children.Add(label);
            }

            // X��̶�
            int pointsToShow = Math.Min(maxPoints, count - startIdx);
            for (int i = 0; i <= xTicks; i++)
            {
                double frac = i / (double)xTicks;
                double x = left + frac * plotWidth;
                int idx = startIdx + (int)(frac * (pointsToShow - 1));
                // �̶���
                var tick = new Line
                {
                    X1 = x,
                    Y1 = height - bottom,
                    X2 = x,
                    Y2 = height - bottom + 5,
                    Stroke = axisBrush,
                    StrokeThickness = 1
                };
                ChartCanvas.Children.Add(tick);

                // ��ǩ
                var label = new TextBlock
                {
                    Text = (idx + 1).ToString(),
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                    FontSize = 12
                };
                Canvas.SetLeft(label, x - 10);
                Canvas.SetTop(label, height - bottom + 8);
                ChartCanvas.Children.Add(label);
            }
            // ���ƾ�ֵ�������ߣ���ɫ��
            if (rateHistory.Count >= 2)
            {
                Polyline meanLine = new Polyline
                {
                    Stroke = new SolidColorBrush(Microsoft.UI.Colors.Yellow),
                    StrokeThickness = 2
                };
                for (int i = startIdx; i < count; i++)
                {
                    double x = left + (i - startIdx) * plotWidth / (maxPoints - 1);
                    double y = top + (maxRate - rateHistory[i]) / (maxRate - minRate) * plotHeight;
                    meanLine.Points.Add(new Windows.Foundation.Point(x, y));

                    // ֻΪ���µ���Ʊ�ǩ
                    if (i == count - 1)
                    {
                        var label = new TextBlock
                        {
                            Text = rateHistory[i].ToString("F0"),
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Yellow),
                            FontSize = 12
                        };
                        Canvas.SetLeft(label, x + 16); // �Ҳ���һ������
                        Canvas.SetTop(label, y - 10);
                        ChartCanvas.Children.Add(label);
                    }
                }
                ChartCanvas.Children.Add(meanLine);
            }
            // �����Ʋ��������ߣ���ɫ��
            if (likelyRateHistory.Count >= 2)
            {
                Polyline likelyLine = new Polyline
                {
                    Stroke = new SolidColorBrush(Microsoft.UI.Colors.Lime),
                    StrokeThickness = 2
                };
                int likelyCount = likelyRateHistory.Count;
                for (int i = startIdx; i < count && i < likelyCount; i++)
                {
                    double x = left + (i - startIdx) * plotWidth / (maxPoints - 1);
                    double y = top + (maxRate - likelyRateHistory[i]) / (maxRate - minRate) * plotHeight;
                    likelyLine.Points.Add(new Windows.Foundation.Point(x, y));

                    // ֻΪ���µ���Ʊ�ǩ
                    if (i == likelyCount - 1)
                    {
                        var label = new TextBlock
                        {
                            Text = likelyRateHistory[i].ToString("F0"),
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Lime),
                            FontSize = 12
                        };
                        Canvas.SetLeft(label, x + 16); // �Ҳ���һ������
                        Canvas.SetTop(label, y + 2);
                        ChartCanvas.Children.Add(label);
                    }
                }
                ChartCanvas.Children.Add(likelyLine);
            }

            // ͼ��
            double legendX = width - right - 110;
            double legendY = top + 10;

            // ��ֵ����ͼ������ɫ��
            var legendMean = new Rectangle
            {
                Width = 18,
                Height = 6,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.Yellow)
            };
            Canvas.SetLeft(legendMean, legendX);
            Canvas.SetTop(legendMean, legendY);
            ChartCanvas.Children.Add(legendMean);

            var legendMeanText = new TextBlock
            {
                Text = "��ֵ����",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Yellow),
                FontSize = 12
            };
            Canvas.SetLeft(legendMeanText, legendX + 24);
            Canvas.SetTop(legendMeanText, legendY - 4);
            ChartCanvas.Children.Add(legendMeanText);

            // �Ʋ�����ͼ������ɫ��
            var legendLikely = new Rectangle
            {
                Width = 18,
                Height = 6,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.Lime)
            };
            Canvas.SetLeft(legendLikely, legendX);
            Canvas.SetTop(legendLikely, legendY + 18);
            ChartCanvas.Children.Add(legendLikely);

            var legendLikelyText = new TextBlock
            {
                Text = "�Ʋ�����",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Lime),
                FontSize = 12
            };
            Canvas.SetLeft(legendLikelyText, legendX + 24);
            Canvas.SetTop(legendLikelyText, legendY + 14);
            ChartCanvas.Children.Add(legendLikelyText);
        }

        private async void StartTest()
        {
            if (isTesting) return;
            isTesting = true;
            cts = new CancellationTokenSource();

            if (infiniteMode)
            {
                ResultTextBlock.Text = "";
                StopButton.Visibility = Visibility.Visible;
                rateHistory.Clear(); // �����ʷ����
                likelyRateHistory.Clear(); // <-- ����������Ʋ�������ʷ
                DrawChart();
                while (isTesting && !cts.Token.IsCancellationRequested)
                {
                    pollingCtx = NativeMethods.CreatePollingContext(sampleCount, false, false); // ������ sampleCount
                    NativeMethods.StartPolling(pollingCtx);

                    await Task.Run(() =>
                    {
                        while (!NativeMethods.IsPollingFinished(pollingCtx) && isTesting && !cts.Token.IsCancellationRequested)
                        {
                            NativeMethods.PollingStep(pollingCtx);
                        }
                    });

                    NativeMethods.AnalyzePolling(pollingCtx);
                    NativeMethods.PollingResult result;
                    NativeMethods.GetPollingResult(pollingCtx, out result);

                    ShowPollingResult(result);
                    DispatcherQueue.TryEnqueue(() => DrawChart());

                    NativeMethods.DestroyPollingContext(pollingCtx);
                    pollingCtx = IntPtr.Zero;
                }
                isTesting = false;
                StopButton.Visibility = Visibility.Collapsed;
                SampleProgressBar.Visibility = Visibility.Collapsed;
                return;
            }

            pollingCtx = NativeMethods.CreatePollingContext(sampleCount, false, false);
            NativeMethods.StartPolling(pollingCtx);

            StopButton.Visibility = Visibility.Collapsed;

            SampleProgressBar.Visibility = Visibility.Visible;
            SampleProgressBar.Minimum = 0;
            SampleProgressBar.Maximum = sampleCount;
            SampleProgressBar.Value = 0;

            await Task.Run(() =>
            {
                int lastCount = 0;
                while (!NativeMethods.IsPollingFinished(pollingCtx) && isTesting && !cts.Token.IsCancellationRequested)
                {
                    int count = NativeMethods.PollingStep(pollingCtx);
                    if (count != lastCount && count % 10 == 0)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            SampleProgressBar.Value = Math.Min(count, sampleCount);
                        });
                        lastCount = count;
                    }
                }
            });

            NativeMethods.AnalyzePolling(pollingCtx);
            NativeMethods.PollingResult finalResult;
            NativeMethods.GetPollingResult(pollingCtx, out finalResult);

            ShowPollingResult(finalResult);
            //rateHistory.Add(finalResult.meanRate);
            //likelyRateHistory.Add(finalResult.likelyRate);

            NativeMethods.DestroyPollingContext(pollingCtx);
            pollingCtx = IntPtr.Zero;
            isTesting = false;
            StopButton.Visibility = Visibility.Collapsed;
            SampleProgressBar.Visibility = Visibility.Collapsed;
        }


        private void StopTest()
        {
            isTesting = false;
            cts?.Cancel();
            if (pollingCtx != IntPtr.Zero)
            {
                NativeMethods.DestroyPollingContext(pollingCtx);
                pollingCtx = IntPtr.Zero;
            }
            StopButton.Visibility = Visibility.Collapsed;
        }

        private void StandardTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(SampleCountBox.Text, out sampleCount) || sampleCount < 100)
                sampleCount = 100;
            infiniteMode = false;
            StartTest();
        }

        private void InfiniteTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(SampleCountBox.Text, out sampleCount) || sampleCount < 100)
                sampleCount = 100;
            infiniteMode = true;
            StartTest();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopTest();
        }
    }
}