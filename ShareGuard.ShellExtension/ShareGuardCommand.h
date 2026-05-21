// ShareGuardCommand.h
// IExplorerCommand implementation for the "Clean with ShareGuard" context menu entry.
// This class runs in-process inside explorer.exe — no exceptions may escape.

#pragma once

#include <windows.h>
#include <shobjidl_core.h>
#include <shlwapi.h>
#include <wrl/module.h>
#include <wrl/implements.h>
#include <wrl/client.h>
#include <string>

// {B7C8D9E0-F1A2-4B5C-8D7E-9F0A1B2C3D4E}
static constexpr CLSID CLSID_ShareGuardCommand =
{ 0xB7C8D9E0, 0xF1A2, 0x4B5C, { 0x8D, 0x7E, 0x9F, 0x0A, 0x1B, 0x2C, 0x3D, 0x4E } };

class ShareGuardCommand final
    : public Microsoft::WRL::RuntimeClass<
          Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
          IExplorerCommand>
{
public:
    ShareGuardCommand() = default;

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(_In_opt_ IShellItemArray* psiItemArray, _Outptr_ LPWSTR* ppszName) override;
    IFACEMETHODIMP GetIcon(_In_opt_ IShellItemArray* psiItemArray, _Outptr_ LPWSTR* ppszIcon) override;
    IFACEMETHODIMP GetToolTip(_In_opt_ IShellItemArray* psiItemArray, _Outptr_ LPWSTR* ppszInfotip) override;
    IFACEMETHODIMP GetCanonicalName(_Out_ GUID* pguidCommandName) override;
    IFACEMETHODIMP GetState(_In_opt_ IShellItemArray* psiItemArray, _In_ BOOL fOkToBeSlow, _Out_ EXPCMDSTATE* pCmdState) override;
    IFACEMETHODIMP GetFlags(_Out_ EXPCMDFLAGS* pFlags) override;
    IFACEMETHODIMP EnumSubCommands(_Outptr_ IEnumExplorerCommand** ppEnum) override;
    IFACEMETHODIMP Invoke(_In_opt_ IShellItemArray* psiItemArray, _In_opt_ IBindCtx* pbc) override;

private:
    // Inner implementations — separated from SEH wrappers because MSVC does not
    // allow __try/__except in functions that use C++ objects with destructors.
    HRESULT GetStateImpl(_In_opt_ IShellItemArray* psiItemArray, _Out_ EXPCMDSTATE* pCmdState) noexcept;
    HRESULT InvokeImpl(_In_opt_ IShellItemArray* psiItemArray);

    // Check if a file path has a supported extension for metadata cleaning.
    static bool IsSupportedExtension(_In_ PCWSTR path) noexcept;

    // Try to send UTF-8 file paths via Named Pipe. Returns true on success.
    static bool TrySendViaPipe(_In_ const char* utf8Payload, _In_ DWORD cbPayload) noexcept;

    // Launch Shareguard-wpf.exe with /clean arguments as a fallback.
    static bool LaunchProcess(_In_ const std::wstring& arguments);

    // Helper: duplicate a string via CoTaskMemAlloc for COM out-params.
    static HRESULT SHStrDupW_Safe(_In_ PCWSTR psz, _Outptr_ LPWSTR* ppwsz) noexcept;
};
