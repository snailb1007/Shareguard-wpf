// ClassFactory.cpp
// IClassFactory implementation for ShareGuardCommand COM class.

#include "ClassFactory.h"
#include "ShareGuardCommand.h"

// ---------------------------------------------------------------------------
// IClassFactory::CreateInstance
// ---------------------------------------------------------------------------
IFACEMETHODIMP ClassFactory::CreateInstance(
    _In_opt_ IUnknown* pUnkOuter,
    _In_ REFIID riid,
    _COM_Outptr_ void** ppvObject)
{
    if (ppvObject) *ppvObject = nullptr;

    if (pUnkOuter)
        return CLASS_E_NOAGGREGATION;

    if (!ppvObject)
        return E_POINTER;

    auto cmd = Microsoft::WRL::Make<ShareGuardCommand>();
    if (!cmd)
        return E_OUTOFMEMORY;

    return cmd->QueryInterface(riid, ppvObject);
}

// ---------------------------------------------------------------------------
// IClassFactory::LockServer
// ---------------------------------------------------------------------------
IFACEMETHODIMP ClassFactory::LockServer(BOOL fLock)
{
    if (fLock)
        Microsoft::WRL::Module<Microsoft::WRL::InProc>::GetModule().IncrementObjectCount();
    else
        Microsoft::WRL::Module<Microsoft::WRL::InProc>::GetModule().DecrementObjectCount();

    return S_OK;
}
