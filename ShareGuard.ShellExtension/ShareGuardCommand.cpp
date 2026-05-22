// ShareGuardCommand.cpp
// IExplorerCommand implementation for "Clean with ShareGuard" context menu.
//
// SAFETY: This code runs in-process inside explorer.exe.
// Public IExplorerCommand methods use SEH __try/__except wrappers that call
// separate *Impl methods. This separation is required because MSVC does not
// allow __try/__except in functions that use C++ objects with destructors
// (std::wstring, ComPtr, etc.).

#include "ShareGuardCommand.h"

#include <string>
#include <shlwapi.h>
#include <strsafe.h>
#include <pathcch.h>
#include <new>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "pathcch.lib")

// ---------------------------------------------------------------------------
// Supported extensions for metadata cleaning
// ---------------------------------------------------------------------------
static constexpr const wchar_t* k_SupportedExtensions[] = {
    L".jpg", L".jpeg", L".png", L".webp", L".pdf", L".mp4"
};

static constexpr const wchar_t k_PipeName[]    = L"\\\\.\\pipe\\ShareGuard_ContextMenu_Pipe";
static constexpr const wchar_t k_ExeName[]     = L"Shareguard-wpf.exe";
static constexpr DWORD         k_PipeTimeoutMs = 500;

// ---------------------------------------------------------------------------
// Helper: CoTaskMemAlloc-based string duplication for COM out-parameters.
// ---------------------------------------------------------------------------
HRESULT ShareGuardCommand::SHStrDupW_Safe(_In_ PCWSTR psz, _Outptr_ LPWSTR* ppwsz) noexcept
{
    if (!ppwsz) return E_POINTER;
    *ppwsz = nullptr;

    if (!psz) return E_INVALIDARG;

    const size_t cb = (wcslen(psz) + 1) * sizeof(wchar_t);
    LPWSTR dup = static_cast<LPWSTR>(CoTaskMemAlloc(cb));
    if (!dup) return E_OUTOFMEMORY;

    memcpy(dup, psz, cb);
    *ppwsz = dup;
    return S_OK;
}

// ===========================================================================
// Simple IExplorerCommand methods — no C++ objects, safe for inline SEH
// ===========================================================================

IFACEMETHODIMP ShareGuardCommand::GetTitle(
    _In_opt_ IShellItemArray* /*psiItemArray*/,
    _Outptr_ LPWSTR* ppszName)
{
    __try
    {
        return SHStrDupW_Safe(L"Clean with ShareGuard", ppszName);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return E_FAIL;
    }
}

IFACEMETHODIMP ShareGuardCommand::GetIcon(
    _In_opt_ IShellItemArray* /*psiItemArray*/,
    _Outptr_ LPWSTR* ppszIcon)
{
    __try
    {
        // TODO: Add custom icon path in future (e.g. DLL resource icon)
        return SHStrDupW_Safe(L"", ppszIcon);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return E_FAIL;
    }
}

IFACEMETHODIMP ShareGuardCommand::GetToolTip(
    _In_opt_ IShellItemArray* /*psiItemArray*/,
    _Outptr_ LPWSTR* ppszInfotip)
{
    __try
    {
        return SHStrDupW_Safe(L"Strip metadata from selected files", ppszInfotip);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return E_FAIL;
    }
}

IFACEMETHODIMP ShareGuardCommand::GetCanonicalName(_Out_ GUID* pguidCommandName)
{
    __try
    {
        if (!pguidCommandName) return E_POINTER;
        *pguidCommandName = CLSID_ShareGuardCommand;
        return S_OK;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return E_FAIL;
    }
}

IFACEMETHODIMP ShareGuardCommand::GetFlags(_Out_ EXPCMDFLAGS* pFlags)
{
    __try
    {
        if (!pFlags) return E_POINTER;
        *pFlags = ECF_DEFAULT;
        return S_OK;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return E_FAIL;
    }
}

IFACEMETHODIMP ShareGuardCommand::EnumSubCommands(_Outptr_ IEnumExplorerCommand** ppEnum)
{
    __try
    {
        if (ppEnum) *ppEnum = nullptr;
        return E_NOTIMPL;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return E_FAIL;
    }
}

// ===========================================================================
// Extension check (case-insensitive)
// ===========================================================================
bool ShareGuardCommand::IsSupportedExtension(_In_ PCWSTR path) noexcept
{
    if (!path) return false;

    const wchar_t* ext = PathFindExtensionW(path);
    if (!ext || ext[0] == L'\0') return false;

    for (const auto* supported : k_SupportedExtensions)
    {
        if (_wcsicmp(ext, supported) == 0)
            return true;
    }
    return false;
}

// ===========================================================================
// GetState — SEH wrapper + Impl
// Uses ComPtr, so the logic must be in a separate function from __try.
// ===========================================================================

HRESULT ShareGuardCommand::GetStateImpl(
    _In_opt_ IShellItemArray* psiItemArray,
    _Out_ EXPCMDSTATE* pCmdState) noexcept
{
    if (!pCmdState) return E_POINTER;
    *pCmdState = ECS_HIDDEN;

    if (!psiItemArray) return S_OK;

    DWORD count = 0;
    HRESULT hr = psiItemArray->GetCount(&count);
    if (FAILED(hr) || count == 0) return S_OK;

    for (DWORD i = 0; i < count; ++i)
    {
        Microsoft::WRL::ComPtr<IShellItem> item;
        if (SUCCEEDED(psiItemArray->GetItemAt(i, &item)))
        {
            LPWSTR path = nullptr;
            if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &path)))
            {
                const bool supported = IsSupportedExtension(path);
                CoTaskMemFree(path);

                if (supported)
                {
                    *pCmdState = ECS_ENABLED;
                    return S_OK;
                }
            }
        }
    }
    return S_OK;
}

IFACEMETHODIMP ShareGuardCommand::GetState(
    _In_opt_ IShellItemArray* psiItemArray,
    _In_ BOOL /*fOkToBeSlow*/,
    _Out_ EXPCMDSTATE* pCmdState)
{
    __try
    {
        return GetStateImpl(psiItemArray, pCmdState);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        if (pCmdState) *pCmdState = ECS_HIDDEN;
        return E_FAIL;
    }
}

// ===========================================================================
// Named Pipe communication
// ===========================================================================
bool ShareGuardCommand::TrySendViaPipe(
    _In_ const char* utf8Payload,
    _In_ DWORD cbPayload) noexcept
{
    // WaitNamedPipe checks if the pipe exists and a server is listening.
    if (!WaitNamedPipeW(k_PipeName, k_PipeTimeoutMs))
        return false;

    HANDLE hPipe = CreateFileW(
        k_PipeName,
        GENERIC_WRITE,
        0,
        nullptr,
        OPEN_EXISTING,
        0,
        nullptr);

    if (hPipe == INVALID_HANDLE_VALUE)
        return false;

    DWORD written = 0;
    const BOOL ok = WriteFile(hPipe, utf8Payload, cbPayload, &written, nullptr);
    CloseHandle(hPipe);

    return ok && (written == cbPayload);
}

// ===========================================================================
// Process launch fallback
// ===========================================================================
bool ShareGuardCommand::LaunchProcess(_In_ const std::wstring& arguments)
{
    // Resolve path relative to this DLL's location.
    wchar_t dllPath[MAX_PATH] = {};
    HMODULE hMod = nullptr;

    // Get the handle of *this* DLL using the address of a function inside it.
    if (!GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
            GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&LaunchProcess),
            &hMod))
    {
        return false;
    }

    if (!GetModuleFileNameW(hMod, dllPath, MAX_PATH))
        return false;

    // Strip DLL filename to get directory.
    PathCchRemoveFileSpec(dllPath, MAX_PATH);

    // Build full exe path: <dllDir>\Shareguard-wpf.exe
    wchar_t exePath[MAX_PATH] = {};
    if (FAILED(PathCchCombine(exePath, MAX_PATH, dllPath, k_ExeName)))
        return false;

    // Build command line: "exePath" /clean "path1" "path2" ...
    std::wstring cmdLine;
    cmdLine.reserve(arguments.size() + MAX_PATH + 32);
    cmdLine += L"\"";
    cmdLine += exePath;
    cmdLine += L"\" /clean";
    cmdLine += arguments;

    // CreateProcessW requires a mutable buffer.
    std::wstring cmdBuf = cmdLine;

    STARTUPINFOW si = {};
    si.cb = sizeof(si);
    PROCESS_INFORMATION pi = {};

    const BOOL created = CreateProcessW(
        exePath,
        &cmdBuf[0],
        nullptr,
        nullptr,
        FALSE,
        CREATE_NEW_PROCESS_GROUP | DETACHED_PROCESS,
        nullptr,
        dllPath,  // Working directory = DLL directory
        &si,
        &pi);

    if (created)
    {
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        return true;
    }
    return false;
}

// ===========================================================================
// Invoke — SEH wrapper + Impl
// Uses std::wstring and ComPtr, so the logic must be in a separate function.
// ===========================================================================

HRESULT ShareGuardCommand::InvokeImpl(_In_opt_ IShellItemArray* psiItemArray)
{
    if (!psiItemArray) return E_INVALIDARG;

    DWORD count = 0;
    HRESULT hr = psiItemArray->GetCount(&count);
    if (FAILED(hr) || count == 0) return S_OK;

    // Collect all file paths.
    std::wstring pathsForPipe;   // newline-delimited for pipe
    std::wstring pathsForCli;    // quoted space-delimited for CLI

    for (DWORD i = 0; i < count; ++i)
    {
        Microsoft::WRL::ComPtr<IShellItem> item;
        if (FAILED(psiItemArray->GetItemAt(i, &item)))
            continue;

        LPWSTR path = nullptr;
        if (FAILED(item->GetDisplayName(SIGDN_FILESYSPATH, &path)))
            continue;

        if (!IsSupportedExtension(path))
        {
            CoTaskMemFree(path);
            continue;
        }

        // Build pipe payload (newline-delimited)
        if (!pathsForPipe.empty())
            pathsForPipe += L'\n';
        pathsForPipe += path;

        // Build CLI arguments (quoted, space-separated)
        pathsForCli += L" \"";
        pathsForCli += path;
        pathsForCli += L'"';

        CoTaskMemFree(path);
    }

    if (pathsForPipe.empty())
        return S_OK;  // No supported files selected

    // Convert pipe payload to UTF-8.
    const int utf8Size = WideCharToMultiByte(
        CP_UTF8, 0,
        pathsForPipe.c_str(), static_cast<int>(pathsForPipe.size()),
        nullptr, 0, nullptr, nullptr);

    if (utf8Size > 0)
    {
        // Use a stack-friendly allocation for small payloads, heap for large.
        constexpr int kStackBufSize = 4096;
        char stackBuf[kStackBufSize];
        char* utf8Buf = (utf8Size <= kStackBufSize)
            ? stackBuf
            : new (std::nothrow) char[utf8Size];

        if (utf8Buf)
        {
            WideCharToMultiByte(
                CP_UTF8, 0,
                pathsForPipe.c_str(), static_cast<int>(pathsForPipe.size()),
                utf8Buf, utf8Size, nullptr, nullptr);

            // Strategy 1: Try Named Pipe (app already running)
            const bool pipeSent = TrySendViaPipe(
                utf8Buf,
                static_cast<DWORD>(utf8Size));

            if (utf8Buf != stackBuf)
                delete[] utf8Buf;

            if (pipeSent)
                return S_OK;
        }
    }

    // Strategy 2: Fall back to launching the process
    LaunchProcess(pathsForCli);

    return S_OK;
}

IFACEMETHODIMP ShareGuardCommand::Invoke(
    _In_opt_ IShellItemArray* psiItemArray,
    _In_opt_ IBindCtx* /*pbc*/)
{
    __try
    {
        return InvokeImpl(psiItemArray);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return E_FAIL;
    }
}
