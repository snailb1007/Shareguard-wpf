// dllmain.cpp
// DLL entry point and COM exports for ShareGuard Shell Extension.
//
// This DLL is loaded by explorer.exe. All exports are guarded with SEH
// to prevent any exception from crashing the shell.

#include "ClassFactory.h"
#include "ShareGuardCommand.h"

#include <windows.h>
#include <wrl/module.h>
#include <new>
#include <strsafe.h>


// ---------------------------------------------------------------------------
// DllMain
// ---------------------------------------------------------------------------
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

// ---------------------------------------------------------------------------
// DllGetClassObject
// Returns the class factory for CLSID_ShareGuardCommand.
// Inner function separated because WRL::Make returns ComPtr (C++ destructor)
// which cannot coexist with SEH __try/__except in the same function scope.
// ---------------------------------------------------------------------------
static HRESULT DllGetClassObject_Impl(
    _In_ REFCLSID rclsid, _In_ REFIID riid, _COM_Outptr_ void** ppv) noexcept
{
    if (ppv) *ppv = nullptr;

    if (!ppv)
        return E_POINTER;

    if (!IsEqualCLSID(rclsid, CLSID_ShareGuardCommand))
        return CLASS_E_CLASSNOTAVAILABLE;

    auto factory = Microsoft::WRL::Make<ClassFactory>();
    if (!factory)
        return E_OUTOFMEMORY;

    return factory->QueryInterface(riid, ppv);
}

STDAPI DllGetClassObject(_In_ REFCLSID rclsid, _In_ REFIID riid, _COM_Outptr_ void** ppv)
{
    __try
    {
        return DllGetClassObject_Impl(rclsid, riid, ppv);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return E_FAIL;
    }
}

// ---------------------------------------------------------------------------
// DllCanUnloadNow
// ---------------------------------------------------------------------------
STDAPI DllCanUnloadNow()
{
    __try
    {
        if (Microsoft::WRL::Module<Microsoft::WRL::InProc>::GetModule().GetObjectCount() == 0)
            return S_OK;
        return S_FALSE;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return S_FALSE;  // Keep loaded on error — safer than premature unload.
    }
}

// ---------------------------------------------------------------------------
// DllRegisterServer / DllUnregisterServer
// Registration is handled externally (sparse manifest or MSIX), but these
// exports are required by the COM contract.
// ---------------------------------------------------------------------------
STDAPI DllRegisterServer()
{
    return S_OK;
}

STDAPI DllUnregisterServer()
{
    return S_OK;
}
