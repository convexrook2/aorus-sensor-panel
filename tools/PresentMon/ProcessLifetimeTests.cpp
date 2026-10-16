// Exercise the actual pinned PresentMon output code, including its process map.
// The test build replaces OutputThread.cpp with this translation unit.
#include "OutputThread.cpp"
#include <stdexcept>

static void Require(bool value, char const* message)
{
    if (!value) throw std::runtime_error(message);
}

static void PresentAt(PMTraceSession const& session, std::vector<ProcessEvent>& events,
                      uint32_t pid, uint64_t time, uint64_t chain)
{
    auto frame = std::make_shared<PresentEvent>();
    frame->ProcessId = pid;
    frame->PresentStartTime = time;
    frame->SwapChainAddress = chain;
    frame->FinalState = PresentResult::Presented;
    frame->Runtime = Runtime::DXGI;
    std::vector<uint64_t> toggles;
    ProcessEvents(session, { frame }, &events, &toggles, false);
}

int wmain()
{
    wchar_t name[] = L"ProcessLifetimeTests";
    wchar_t noCsv[] = L"--no_csv";
    wchar_t quiet[] = L"--no_console_stats";
    wchar_t* arguments[] = { name, noCsv, quiet };
    if (!ParseCommandLine(3, arguments)) return 2;
    try {
        constexpr uint32_t pid = 424242;
        PMTraceSession session;
        session.mTimestampFrequency.QuadPart = 10000000;
        std::vector<ProcessEvent> events = {
            { L"git.exe", 100000000, pid, true },
            { L"",        110000000, pid, false },
            { L"bf6.exe", 120000000, pid, true },
        };

        // Keep the earlier process until its final frame. Then allow its stop
        // and the replacement's start to advance before BF6's first frame.
        PresentAt(session, events, pid, 105000000, 0x111);
        Require(gProcesses.at(pid).mModuleName == L"git.exe", "Premature process replacement");
        PresentAt(session, events, pid, 125000000, 0x222);
        Require(gProcesses.at(pid).mModuleName == L"bf6.exe", "PID reuse retained git.exe for BF6");
        Require(!gProcesses.at(pid).mSwapChain.contains(0x111), "Old process frame history survived PID reuse");
        Require(events.empty() && gTargetProcessCount == 1, "Process events blocked or target count leaked");
        std::puts("PASS: final old frame, PID reuse, executable name, and independent frame history");

        // A start/rundown can replace a cached process even if its stop was lost.
        gProcesses.at(pid).mSwapChain[0x333] = SwapChainData{};
        ProcessProcessEvent({ L"replacement.exe", 130000000, pid, true });
        Require(gProcesses.at(pid).mModuleName == L"replacement.exe" &&
                gProcesses.at(pid).mSwapChain.empty() && gTargetProcessCount == 1,
                "Replacement start retained the previous process's swap chains");
        std::puts("PASS: replacement start resets all previous process state");

        // Closing the helper's query handle releases its process object too.
        HANDLE query = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, GetCurrentProcessId());
        Require(query != nullptr, "Could not establish test query handle");
        gProcesses.at(pid).mHandle = query;
        ProcessProcessEvent({ L"", 140000000, pid, false });
        DWORD flags = 0;
        Require(!GetHandleInformation(query, &flags) && GetLastError() == ERROR_INVALID_HANDLE &&
                gProcesses.empty() && gTargetProcessCount == 0,
                "Terminated process state or query handle remained retained");
        std::puts("PASS: process stop releases query handles and tracked state");
        return 0;
    } catch (std::exception const& error) {
        std::fprintf(stderr, "FAIL: %s\n", error.what());
        return 1;
    }
}
