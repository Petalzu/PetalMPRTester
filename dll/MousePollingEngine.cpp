#define NOMINMAX
#include <windows.h>
#include <vector>
#include <chrono>
#include <numeric>
#include <algorithm>
#include <cmath>
#include <map>
#include <string>
#include <cstring>
#include <mmsystem.h>

extern "C"
{

    // 结果结构体
    struct PollingResult
    {
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
    };

    // 采集上下文
    struct PollingContext
    {
        std::vector<long long> timestamps;
        int sampleCount;
        bool highPrecisionMode;
        bool infiniteMode;
        LARGE_INTEGER freq;      // 频率
        LARGE_INTEGER startTime; // 采集起始时间
        POINT lastPos;
        std::vector<long long> dynamicIntervals;
        bool collecting;
        PollingResult result;
    };

    // 创建采集上下文
    __declspec(dllexport) PollingContext *CreatePollingContext(int samples, bool highPrecision, bool infinite)
    {
        timeBeginPeriod(1);
        PollingContext *ctx = new PollingContext();
        ctx->sampleCount = samples;
        ctx->highPrecisionMode = highPrecision;
        ctx->infiniteMode = infinite;
        ctx->timestamps.reserve(samples + 1);
        ctx->collecting = false;
        QueryPerformanceFrequency(&ctx->freq);
        return ctx;
    }

    // 释放采集上下文
    __declspec(dllexport) void DestroyPollingContext(PollingContext *ctx)
    {
        if (ctx)
        {
            delete ctx;
            timeEndPeriod(1);
        }
    }

    // 开始采集
    __declspec(dllexport) void StartPolling(PollingContext *ctx)
    {
        if (!ctx)
            return;
        ctx->timestamps.clear();
        ctx->dynamicIntervals.clear();
        ctx->collecting = true;
        GetCursorPos(&ctx->lastPos);
        QueryPerformanceCounter(&ctx->startTime);
        ctx->timestamps.push_back(0);
    }

    // 轮询采集
    __declspec(dllexport) int PollingStep(PollingContext *ctx)
    {
        if (!ctx || !ctx->collecting)
            return 0;
        POINT currentPos;
        GetCursorPos(&currentPos);
        if (currentPos.x != ctx->lastPos.x || currentPos.y != ctx->lastPos.y)
        {
            LARGE_INTEGER now;
            QueryPerformanceCounter(&now);
            // 以纳秒为单位
            long long elapsed = (now.QuadPart - ctx->startTime.QuadPart) * 1000000000LL / ctx->freq.QuadPart;
            ctx->timestamps.push_back(elapsed);
            if (ctx->timestamps.size() > 2)
            {
                long long interval = ctx->timestamps[ctx->timestamps.size() - 1] - ctx->timestamps[ctx->timestamps.size() - 2];
                ctx->dynamicIntervals.push_back(interval);
            }
            ctx->lastPos = currentPos;
        }
        if (!ctx->infiniteMode && (int)ctx->timestamps.size() - 1 >= ctx->sampleCount)
        {
            ctx->collecting = false;
        }
        return (int)ctx->timestamps.size() - 1;
    }

    // 是否采集完成
    __declspec(dllexport) bool IsPollingFinished(PollingContext *ctx)
    {
        if (!ctx)
            return true;
        return !ctx->collecting;
    }

    // 分析采集数据
    __declspec(dllexport) void AnalyzePolling(PollingContext *ctx)
    {
        if (!ctx)
            return;
        std::vector<long long> intervals;
        intervals.reserve(ctx->timestamps.size() - 1);
        for (size_t i = 1; i < ctx->timestamps.size(); i++)
        {
            intervals.push_back(ctx->timestamps[i] - ctx->timestamps[i - 1]); // 纳秒
        }
        if (intervals.empty())
        {
            memset(&ctx->result, 0, sizeof(PollingResult));
            strcpy_s(ctx->result.likelyRateText, "无数据");
            return;
        }
        // 剔除异常值（IQR法）
        std::vector<long long> filteredIntervals = intervals;
        std::sort(filteredIntervals.begin(), filteredIntervals.end());
        size_t q1Index = filteredIntervals.size() / 4;
        size_t q3Index = filteredIntervals.size() * 3 / 4;
        long long q1 = filteredIntervals[q1Index];
        long long q3 = filteredIntervals[q3Index];
        long long iqr = q3 - q1;
        double iqrFactor = 1.5; // <-- 补充声明
        long long lowerBound = static_cast<long long>(q1 - iqrFactor * iqr);
        long long upperBound = static_cast<long long>(q3 + iqrFactor * iqr);

        std::vector<long long> cleanIntervals;
        for (long long interval : intervals)
        {
            if (interval >= lowerBound && interval <= upperBound)
            {
                cleanIntervals.push_back(interval);
            }
        }

        // 滑动中值滤波
        int windowSize = 7;
        std::vector<long long> filtered;
        for (size_t i = 0; i + windowSize <= cleanIntervals.size(); ++i)
        {
            std::vector<long long> window(cleanIntervals.begin() + i, cleanIntervals.begin() + i + windowSize);
            std::nth_element(window.begin(), window.begin() + windowSize / 2, window.end());
            filtered.push_back(window[windowSize / 2]);
        }

        // 只取中间80%区间
        size_t start = static_cast<size_t>(filtered.size() * 0.1);
        size_t end = static_cast<size_t>(filtered.size() * 0.9);
        if (end > filtered.size())
            end = filtered.size();
        if (start > end)
            start = end;
        std::vector<long long> stableIntervals;
        if (end > start)
            stableIntervals = std::vector<long long>(filtered.begin() + start, filtered.begin() + end);
        else
            stableIntervals = filtered; // 防止空

        // 后续统计全部用stableIntervals替换cleanIntervals
        double sum = std::accumulate(stableIntervals.begin(), stableIntervals.end(), 0.0);
        double mean = sum / (stableIntervals.empty() ? 1 : stableIntervals.size());
        double pollingRate = 1e9 / mean; // Hz
        double sq_sum = 0;
        for (long long interval : cleanIntervals)
        {
            sq_sum += (interval - mean) * (interval - mean);
        }
        double stdev = std::sqrt(sq_sum / (cleanIntervals.empty() ? 1 : cleanIntervals.size()));
        long long minInterval = cleanIntervals.empty() ? 0LL : *std::min_element(cleanIntervals.begin(), cleanIntervals.end());
        long long maxInterval = cleanIntervals.empty() ? 0LL : *std::max_element(cleanIntervals.begin(), cleanIntervals.end());
        long long median = cleanIntervals.empty() ? 0LL : cleanIntervals[cleanIntervals.size() / 2];

        // 更细粒度分桶（如1us=1000ns）
        std::map<int, int> histogram;
        int bucketWidth = 1000; // 1us
        for (long long interval : cleanIntervals)
        {
            int bucket = static_cast<int>(interval / bucketWidth);
            histogram[bucket]++;
        }
        int maxCount = 0;
        int modeBucket = 0;
        for (const auto &pair : histogram)
        {
            if (pair.second > maxCount)
            {
                maxCount = pair.second;
                modeBucket = pair.first;
            }
        }
        double modeInterval = modeBucket * bucketWidth + bucketWidth / 2.0;
        double modePollingRate = 1e9 / modeInterval;
        // int roundedRate = static_cast<int>(std::round(modePollingRate));
        // char likelyRateText[32] = "";
        // if (roundedRate > 6000 && roundedRate < 10000)
        //     strcpy_s(likelyRateText, "8000 Hz");
        // else if (roundedRate > 3500 && roundedRate < 6000)
        //     strcpy_s(likelyRateText, "4000 Hz");
        // else if (roundedRate > 1800 && roundedRate < 3500)
        //     strcpy_s(likelyRateText, "2000 Hz");
        // else if (roundedRate > 850 && roundedRate < 1800)
        //     strcpy_s(likelyRateText, "1000 Hz");
        // else if (roundedRate > 325 && roundedRate < 850)
        //     strcpy_s(likelyRateText, "500 Hz");
        // else if (roundedRate > 175 && roundedRate < 325)
        //     strcpy_s(likelyRateText, "250 Hz");
        // else if (roundedRate > 50 && roundedRate < 175)
        //     strcpy_s(likelyRateText, "125 Hz");
        // else
        //     sprintf_s(likelyRateText, "%d Hz (非标准)", roundedRate);

        int roundedRate = static_cast<int>(std::round(pollingRate));
        char likelyRateText[32] = "";
        if (roundedRate > 4500 && roundedRate < 10000)
            strcpy_s(likelyRateText, "8000 Hz");
        else if (roundedRate > 3000 && roundedRate < 4500)
            strcpy_s(likelyRateText, "4000 Hz");
        else if (roundedRate > 1500 && roundedRate < 3500)
            strcpy_s(likelyRateText, "2000 Hz");
        else if (roundedRate > 750 && roundedRate < 1500)
            strcpy_s(likelyRateText, "1000 Hz");
        else if (roundedRate > 325 && roundedRate < 750)
            strcpy_s(likelyRateText, "500 Hz");
        else if (roundedRate > 175 && roundedRate < 325)
            strcpy_s(likelyRateText, "250 Hz");
        else if (roundedRate > 50 && roundedRate < 175)
            strcpy_s(likelyRateText, "125 Hz");
        else
            sprintf_s(likelyRateText, "%d Hz (非标准)", roundedRate);
        double stabilityScore = 100.0 * (1.0 - std::min(stdev / mean, 1.0));
        // 结果全部转为微秒(us)输出
        ctx->result.sampleCount = (int)intervals.size();
        ctx->result.validCount = (int)cleanIntervals.size();
        ctx->result.outlierCount = (int)(intervals.size() - cleanIntervals.size());
        ctx->result.minInterval = minInterval / 1000.0;
        ctx->result.maxInterval = maxInterval / 1000.0;
        ctx->result.meanInterval = mean / 1000.0;
        ctx->result.medianInterval = median / 1000.0;
        ctx->result.modeInterval = modeInterval / 1000.0;
        ctx->result.stdevInterval = stdev / 1000.0;
        ctx->result.meanRate = pollingRate;
        ctx->result.modeRate = modePollingRate;
        ctx->result.likelyRate = roundedRate;
        ctx->result.stabilityScore = stabilityScore;
        strcpy_s(ctx->result.likelyRateText, likelyRateText);
    }

    // 获取分析结果
    __declspec(dllexport) void GetPollingResult(PollingContext *ctx, PollingResult *outResult)
    {
        if (!ctx || !outResult)
            return;
        *outResult = ctx->result;
    }
}