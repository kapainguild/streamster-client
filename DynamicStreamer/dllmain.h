// dllmain.h : Declaration of module class.

class CFFDynamicStreamerModule : public ATL::CAtlDllModuleT< CFFDynamicStreamerModule >
{
public :
	DECLARE_LIBID(LIBID_FFDynamicStreamerLib)
	DECLARE_REGISTRY_APPID_RESOURCEID(IDR_FFDYNAMICSTREAMER, "{6203FBD4-6D3E-46DF-9D64-4E351590B92D}")
};

extern class CFFDynamicStreamerModule _AtlModule;
