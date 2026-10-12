#include <cstddef>
#include <cstdint>
#include <vector>
#include <cmath>
#include "FvSDK.h"

// Compile against the installed public SDK instead of guessing QueryInterface IDs.
static_assert(sizeof(FPS_v1) == 32 && sizeof(AvgFPS_v1) == 56 && sizeof(Samples_v1) == 24,
    "Installed FrameView SDK layout changed; review the bridge before sampling.");

extern "C" __declspec(dllexport) unsigned __cdecl FrameViewLayout()
{
    return (sizeof(AvgFPS_v1) << 16) | (sizeof(FPS_v1) << 8) | sizeof(Samples_v1);
}

extern "C" __declspec(dllexport) unsigned __cdecl FrameViewOpen(void** output, unsigned* stage)
{
    if (!output || !stage) return FV_ERROR_INVALID_ARGS;
    *output = nullptr;
    *stage = 1;
    FvStatus result = FvSDK_Initialize();
    if (result != FV_SUCCESS) return result;
    *stage = 2;
    FvSession session = nullptr;
    result = FvSDK_CreateSession(&session);
    if (result != FV_SUCCESS) return result;
    FvMetricType metric = eAvgFPS;
    *stage = 3;
    result = FvSDK_StartSession(session);
    if (result == FV_SUCCESS) { *stage = 4; result = FvSDK_EnableMetrics(session, &metric, 1); }
    if (result != FV_SUCCESS) { FvSDK_StopSession(session); FvSDK_DestroySession(session); return result; }
    *output = session;
    *stage = 0;
    return FV_SUCCESS;
}

extern "C" __declspec(dllexport) unsigned __cdecl FrameViewRead(void* handle, unsigned pid,
    double* fps, unsigned long long* swapChain, unsigned long long* timestamp)
{
    if (!handle || !fps || !swapChain || !timestamp) return FV_ERROR_INVALID_ARGS;
    *fps = 0; *swapChain = 0; *timestamp = 0;
    auto session = static_cast<FvSession>(handle);
    FvStatus status = FvSDK_SampleData(session, FVSDK_SAMPLEDATA_DONOTWAIT);
    if (status != FV_SUCCESS) return status;
    Samples samples;
    samples.type = eAvgFPS;
    status = FvSDK_ReadData(session, &samples, 1);
    if (status != FV_SUCCESS && status != FV_INSUFFICIENT_BUFFER && status != FV_MORE_DATA) return status;
    if (samples.mNumSamples == 0) return FV_NO_DATA;
    if (samples.mNumSamples > 4096) return FV_ERROR_INVALID_ARGS;
    std::vector<AvgFPS_v1> values(samples.mNumSamples);
    samples.mData = values.data();
    status = FvSDK_ReadData(session, &samples, 1);
    if (status != FV_SUCCESS && status != FV_MORE_DATA) return status;
    if (samples.mNumSamples > values.size()) return FV_ERROR_INVALID_ARGS;
    const AvgFPS_v1* selected = nullptr;
    for (size_t i = 0; i < samples.mNumSamples; ++i)
    {
        const auto& value = values[i];
        if (value.mFPS.PID != pid || !std::isfinite(value.mFPS.AvgFPS) || value.mFPS.AvgFPS <= 0) continue;
        if (!selected || value.mTimestamp > selected->mTimestamp) selected = &value;
    }
    if (!selected) return FV_NO_DATA;
    *fps = selected->mFPS.AvgFPS;
    *swapChain = selected->mFPS.SwapChain;
    *timestamp = selected->mTimestamp;
    return FV_SUCCESS;
}

extern "C" __declspec(dllexport) void __cdecl FrameViewClose(void* handle)
{
    if (handle)
    {
        auto session = static_cast<FvSession>(handle);
        FvSDK_StopSession(session);
        FvSDK_DestroySession(session);
    }
    FvSDK_Shutdown();
}
