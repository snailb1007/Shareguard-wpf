// ClassFactory.h
// IClassFactory implementation for ShareGuardCommand COM class.

#pragma once

#include <windows.h>
#include <unknwn.h>
#include <wrl/module.h>
#include <wrl/implements.h>

class ClassFactory final
    : public Microsoft::WRL::RuntimeClass<
          Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
          IClassFactory>
{
public:
    ClassFactory() = default;

    // IClassFactory
    IFACEMETHODIMP CreateInstance(_In_opt_ IUnknown* pUnkOuter, _In_ REFIID riid, _COM_Outptr_ void** ppvObject) override;
    IFACEMETHODIMP LockServer(BOOL fLock) override;
};
