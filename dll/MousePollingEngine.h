#pragma once

#ifdef __cplusplus
extern "C" {
#endif

// 结果结构体
typedef struct PollingResult {
    int sampleCount;
    int validCount;
    int outlierCount;
    double minInterval;
    double maxInterval;
    double meanInterval;
    double medianInterval;
    double modeInterval;
    double stdevInterval;
    double meanRate;
    double modeRate;
    int likelyRate;
    double stabilityScore;
    char likelyRateText[32];
} PollingResult;

// 采集上下文前置声明
typedef struct PollingContext PollingContext;

// 导出函数声明
__declspec(dllexport) PollingContext* CreatePollingContext(int samples, bool highPrecision, bool infinite);
__declspec(dllexport) void DestroyPollingContext(PollingContext* ctx);
__declspec(dllexport) void StartPolling(PollingContext* ctx);
__declspec(dllexport) int PollingStep(PollingContext* ctx);
__declspec(dllexport) bool IsPollingFinished(PollingContext* ctx);
__declspec(dllexport) void AnalyzePolling(PollingContext* ctx);
__declspec(dllexport) void GetPollingResult(PollingContext* ctx, PollingResult* outResult);

#ifdef __cplusplus
}
#endif