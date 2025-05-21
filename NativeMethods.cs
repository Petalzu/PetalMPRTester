using System;
using System.Runtime.InteropServices;
using System.Text;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct PollingResult
    {
        public int sampleCount;
        public int validCount;
        public int outlierCount;
        public double minInterval;
        public double maxInterval;
        public double meanInterval;
        public double medianInterval;
        public double modeInterval;
        public double stdevInterval;
        public double meanRate;
        public double modeRate;
        public int likelyRate;
        public double stabilityScore;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string likelyRateText;
    }

    private const string DllName = "MousePollingEngine.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr CreatePollingContext(int samples, bool highPrecision, bool infinite);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void DestroyPollingContext(IntPtr ctx);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void StartPolling(IntPtr ctx);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int PollingStep(IntPtr ctx);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool IsPollingFinished(IntPtr ctx);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void AnalyzePolling(IntPtr ctx);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void GetPollingResult(IntPtr ctx, out PollingResult result);
}