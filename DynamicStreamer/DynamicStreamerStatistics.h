// DynamicStreamerStatistics.h : Declaration of the CDynamicStreamerStatistics

#pragma once
#include "resource.h"       // main symbols

#include "Statistics.h"

#include "FFDynamicStreamer_i.h"



#if defined(_WIN32_WCE) && !defined(_CE_DCOM) && !defined(_CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA)
#error "Single-threaded COM objects are not properly supported on Windows CE platform, such as the Windows Mobile platforms that do not include full DCOM support. Define _CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA to force ATL to support creating single-thread COM object's and allow use of it's single-threaded COM object implementations. The threading model in your rgs file was set to 'Free' as that is the only threading model supported in non DCOM Windows CE platforms."
#endif

using namespace ATL;


// CDynamicStreamerStatistics

class ATL_NO_VTABLE CDynamicStreamerStatistics :
	public CComObjectRootEx<CComMultiThreadModel>,
	public CComCoClass<CDynamicStreamerStatistics, &CLSID_DynamicStreamerStatistics>,
	public IDispatchImpl<IDynamicStreamerStatistics, &IID_IDynamicStreamerStatistics, &LIBID_FFDynamicStreamerLib, /*wMajor =*/ 1, /*wMinor =*/ 0>
{
public:
	CDynamicStreamerStatistics()
	{
	}

DECLARE_REGISTRY_RESOURCEID(IDR_DYNAMICSTREAMERSTATISTICS)


BEGIN_COM_MAP(CDynamicStreamerStatistics)
	COM_INTERFACE_ENTRY(IDynamicStreamerStatistics)
	COM_INTERFACE_ENTRY(IDispatch)
	COM_INTERFACE_ENTRY_AGGREGATE(IID_IMarshal, m_ftm)
END_COM_MAP()


CComPtr<IUnknown> m_ftm;

	DECLARE_PROTECT_FINAL_CONSTRUCT()

	HRESULT FinalConstruct()
	{
		IUnknown* pUnk = GetUnknown();
		return CoCreateFreeThreadedMarshaler(pUnk, &m_ftm);
	}

	void FinalRelease()
	{
		m_ftm.Release();
	}

public:
	StatisticItem Item;
	int id;
	BOOL overall;
	int error;
	CStringA errorMessage;



	STDMETHOD(get_Interval)(LONGLONG* pVal);
	STDMETHOD(get_Overall)(BOOL* pVal);
	STDMETHOD(get_Id)(int* pVal);
	STDMETHOD(GetValues)(SAFEARRAY * * pValues);
	STDMETHOD(GetError)(BSTR *errorMsg, int *error);
};

OBJECT_ENTRY_AUTO(__uuidof(DynamicStreamerStatistics), CDynamicStreamerStatistics)
