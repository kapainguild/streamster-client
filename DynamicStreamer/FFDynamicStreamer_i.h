

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0622 */
/* at Tue Jan 19 04:14:07 2038
 */
/* Compiler settings for FFDynamicStreamer.idl:
    Oicf, W1, Zp8, env=Win64 (32b run), target_arch=AMD64 8.01.0622 
    protocol : all , ms_ext, c_ext, robust
    error checks: allocation ref bounds_check enum stub_data 
    VC __declspec() decoration level: 
         __declspec(uuid()), __declspec(selectany), __declspec(novtable)
         DECLSPEC_UUID(), MIDL_INTERFACE()
*/
/* @@MIDL_FILE_HEADING(  ) */



/* verify that the <rpcndr.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCNDR_H_VERSION__
#define __REQUIRED_RPCNDR_H_VERSION__ 500
#endif

#include "rpc.h"
#include "rpcndr.h"

#ifndef __RPCNDR_H_VERSION__
#error this stub requires an updated version of <rpcndr.h>
#endif /* __RPCNDR_H_VERSION__ */

#ifndef COM_NO_WINDOWS_H
#include "windows.h"
#include "ole2.h"
#endif /*COM_NO_WINDOWS_H*/

#ifndef __FFDynamicStreamer_i_h__
#define __FFDynamicStreamer_i_h__

#if defined(_MSC_VER) && (_MSC_VER >= 1020)
#pragma once
#endif

/* Forward Declarations */ 

#ifndef __IDynamicStreamerCallback_FWD_DEFINED__
#define __IDynamicStreamerCallback_FWD_DEFINED__
typedef interface IDynamicStreamerCallback IDynamicStreamerCallback;

#endif 	/* __IDynamicStreamerCallback_FWD_DEFINED__ */


#ifndef __IDynamicStreamerDecoderCallback_FWD_DEFINED__
#define __IDynamicStreamerDecoderCallback_FWD_DEFINED__
typedef interface IDynamicStreamerDecoderCallback IDynamicStreamerDecoderCallback;

#endif 	/* __IDynamicStreamerDecoderCallback_FWD_DEFINED__ */


#ifndef __IDynamicStreamer_FWD_DEFINED__
#define __IDynamicStreamer_FWD_DEFINED__
typedef interface IDynamicStreamer IDynamicStreamer;

#endif 	/* __IDynamicStreamer_FWD_DEFINED__ */


#ifndef __IDynamicStreamerStatistics_FWD_DEFINED__
#define __IDynamicStreamerStatistics_FWD_DEFINED__
typedef interface IDynamicStreamerStatistics IDynamicStreamerStatistics;

#endif 	/* __IDynamicStreamerStatistics_FWD_DEFINED__ */


#ifndef __DynamicStreamer_FWD_DEFINED__
#define __DynamicStreamer_FWD_DEFINED__

#ifdef __cplusplus
typedef class DynamicStreamer DynamicStreamer;
#else
typedef struct DynamicStreamer DynamicStreamer;
#endif /* __cplusplus */

#endif 	/* __DynamicStreamer_FWD_DEFINED__ */


#ifndef __DynamicStreamerStatistics_FWD_DEFINED__
#define __DynamicStreamerStatistics_FWD_DEFINED__

#ifdef __cplusplus
typedef class DynamicStreamerStatistics DynamicStreamerStatistics;
#else
typedef struct DynamicStreamerStatistics DynamicStreamerStatistics;
#endif /* __cplusplus */

#endif 	/* __DynamicStreamerStatistics_FWD_DEFINED__ */


/* header files for imported files */
#include "oaidl.h"
#include "ocidl.h"

#ifdef __cplusplus
extern "C"{
#endif 


/* interface __MIDL_itf_FFDynamicStreamer_0000_0000 */
/* [local] */ 

typedef 
enum StatisticType
    {
        statisticTypeVideoFrames	= 0,
        statisticTypeVideoBytes	= ( statisticTypeVideoFrames + 1 ) ,
        statisticTypeAudioFrames	= ( statisticTypeVideoBytes + 1 ) ,
        statisticTypeAudioBytes	= ( statisticTypeAudioFrames + 1 ) ,
        statisticTypeProcessingTime	= ( statisticTypeAudioBytes + 1 ) ,
        statisticTypeDropped	= ( statisticTypeProcessingTime + 1 ) ,
        statisticTypeErrors	= ( statisticTypeDropped + 1 ) ,
        statisticTypeCount	= ( statisticTypeErrors + 1 ) 
    } 	statisticType;



extern RPC_IF_HANDLE __MIDL_itf_FFDynamicStreamer_0000_0000_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_FFDynamicStreamer_0000_0000_v0_0_s_ifspec;

#ifndef __IDynamicStreamerCallback_INTERFACE_DEFINED__
#define __IDynamicStreamerCallback_INTERFACE_DEFINED__

/* interface IDynamicStreamerCallback */
/* [object][uuid] */ 


EXTERN_C const IID IID_IDynamicStreamerCallback;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("B2991AF4-6762-431C-A615-EB3B7B3CE882")
    IDynamicStreamerCallback : public IUnknown
    {
    public:
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE NotifyError( 
            /* [in] */ int errorCode,
            /* [in] */ BSTR errorMessage,
            /* [in] */ BSTR pattern) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IDynamicStreamerCallbackVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            IDynamicStreamerCallback * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            IDynamicStreamerCallback * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            IDynamicStreamerCallback * This);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *NotifyError )( 
            IDynamicStreamerCallback * This,
            /* [in] */ int errorCode,
            /* [in] */ BSTR errorMessage,
            /* [in] */ BSTR pattern);
        
        END_INTERFACE
    } IDynamicStreamerCallbackVtbl;

    interface IDynamicStreamerCallback
    {
        CONST_VTBL struct IDynamicStreamerCallbackVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IDynamicStreamerCallback_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IDynamicStreamerCallback_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IDynamicStreamerCallback_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IDynamicStreamerCallback_NotifyError(This,errorCode,errorMessage,pattern)	\
    ( (This)->lpVtbl -> NotifyError(This,errorCode,errorMessage,pattern) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IDynamicStreamerCallback_INTERFACE_DEFINED__ */


#ifndef __IDynamicStreamerDecoderCallback_INTERFACE_DEFINED__
#define __IDynamicStreamerDecoderCallback_INTERFACE_DEFINED__

/* interface IDynamicStreamerDecoderCallback */
/* [object][uuid] */ 


EXTERN_C const IID IID_IDynamicStreamerDecoderCallback;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("B2991AF4-6762-431C-A615-EB3B7B3CE883")
    IDynamicStreamerDecoderCallback : public IUnknown
    {
    public:
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE NotifyFrame( 
            /* [in] */ int width,
            /* [in] */ int height,
            /* [in] */ int length,
            /* [in] */ __int64 data) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IDynamicStreamerDecoderCallbackVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            IDynamicStreamerDecoderCallback * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            IDynamicStreamerDecoderCallback * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            IDynamicStreamerDecoderCallback * This);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *NotifyFrame )( 
            IDynamicStreamerDecoderCallback * This,
            /* [in] */ int width,
            /* [in] */ int height,
            /* [in] */ int length,
            /* [in] */ __int64 data);
        
        END_INTERFACE
    } IDynamicStreamerDecoderCallbackVtbl;

    interface IDynamicStreamerDecoderCallback
    {
        CONST_VTBL struct IDynamicStreamerDecoderCallbackVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IDynamicStreamerDecoderCallback_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IDynamicStreamerDecoderCallback_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IDynamicStreamerDecoderCallback_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IDynamicStreamerDecoderCallback_NotifyFrame(This,width,height,length,data)	\
    ( (This)->lpVtbl -> NotifyFrame(This,width,height,length,data) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IDynamicStreamerDecoderCallback_INTERFACE_DEFINED__ */


#ifndef __IDynamicStreamer_INTERFACE_DEFINED__
#define __IDynamicStreamer_INTERFACE_DEFINED__

/* interface IDynamicStreamer */
/* [unique][nonextensible][dual][uuid][object] */ 


EXTERN_C const IID IID_IDynamicStreamer;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("9ADD4A25-2BD3-42E5-A06F-C8548ECC9A95")
    IDynamicStreamer : public IDispatch
    {
    public:
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE SetEncoder( 
            /* [in] */ BSTR videoCodec,
            /* [in] */ BSTR videoOptions,
            /* [in] */ BSTR fallbackvideoCodec,
            /* [in] */ BSTR fallbackVideoOptions,
            /* [in] */ int videoMaxBitrate,
            /* [in] */ BSTR audioCodec,
            /* [in] */ BSTR audioOptions,
            /* [in] */ int audioMaxBitrate) = 0;
        
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE SetInput( 
            /* [in] */ BSTR type,
            /* [in] */ BSTR input,
            /* [in] */ BSTR options,
            /* [in] */ int fps,
            /* [in] */ int width,
            /* [in] */ int height) = 0;
        
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE AddOutput( 
            /* [in] */ BSTR type,
            /* [in] */ BSTR input,
            /* [in] */ BSTR options,
            /* [in] */ IDynamicStreamerDecoderCallback *pCallback,
            /* [retval][out] */ int *pId) = 0;
        
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE RemoveOutput( 
            /* [in] */ int id) = 0;
        
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE GetStatistics( 
            /* [retval][out] */ SAFEARRAY * *pStats) = 0;
        
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE SetCallback( 
            /* [in] */ IDynamicStreamerCallback *pCallback) = 0;
        
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE SetFilter( 
            /* [in] */ BSTR videoFilter) = 0;
        
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE SetDirectFrameCallback( 
            /* [in] */ IDynamicStreamerDecoderCallback *pCallback) = 0;
        
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE GetSupportedCodecs( 
            /* [retval][out] */ int *pFlags) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IDynamicStreamerVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            IDynamicStreamer * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            IDynamicStreamer * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            IDynamicStreamer * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetTypeInfoCount )( 
            IDynamicStreamer * This,
            /* [out] */ UINT *pctinfo);
        
        HRESULT ( STDMETHODCALLTYPE *GetTypeInfo )( 
            IDynamicStreamer * This,
            /* [in] */ UINT iTInfo,
            /* [in] */ LCID lcid,
            /* [out] */ ITypeInfo **ppTInfo);
        
        HRESULT ( STDMETHODCALLTYPE *GetIDsOfNames )( 
            IDynamicStreamer * This,
            /* [in] */ REFIID riid,
            /* [size_is][in] */ LPOLESTR *rgszNames,
            /* [range][in] */ UINT cNames,
            /* [in] */ LCID lcid,
            /* [size_is][out] */ DISPID *rgDispId);
        
        /* [local] */ HRESULT ( STDMETHODCALLTYPE *Invoke )( 
            IDynamicStreamer * This,
            /* [annotation][in] */ 
            _In_  DISPID dispIdMember,
            /* [annotation][in] */ 
            _In_  REFIID riid,
            /* [annotation][in] */ 
            _In_  LCID lcid,
            /* [annotation][in] */ 
            _In_  WORD wFlags,
            /* [annotation][out][in] */ 
            _In_  DISPPARAMS *pDispParams,
            /* [annotation][out] */ 
            _Out_opt_  VARIANT *pVarResult,
            /* [annotation][out] */ 
            _Out_opt_  EXCEPINFO *pExcepInfo,
            /* [annotation][out] */ 
            _Out_opt_  UINT *puArgErr);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *SetEncoder )( 
            IDynamicStreamer * This,
            /* [in] */ BSTR videoCodec,
            /* [in] */ BSTR videoOptions,
            /* [in] */ BSTR fallbackvideoCodec,
            /* [in] */ BSTR fallbackVideoOptions,
            /* [in] */ int videoMaxBitrate,
            /* [in] */ BSTR audioCodec,
            /* [in] */ BSTR audioOptions,
            /* [in] */ int audioMaxBitrate);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *SetInput )( 
            IDynamicStreamer * This,
            /* [in] */ BSTR type,
            /* [in] */ BSTR input,
            /* [in] */ BSTR options,
            /* [in] */ int fps,
            /* [in] */ int width,
            /* [in] */ int height);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *AddOutput )( 
            IDynamicStreamer * This,
            /* [in] */ BSTR type,
            /* [in] */ BSTR input,
            /* [in] */ BSTR options,
            /* [in] */ IDynamicStreamerDecoderCallback *pCallback,
            /* [retval][out] */ int *pId);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *RemoveOutput )( 
            IDynamicStreamer * This,
            /* [in] */ int id);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *GetStatistics )( 
            IDynamicStreamer * This,
            /* [retval][out] */ SAFEARRAY * *pStats);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *SetCallback )( 
            IDynamicStreamer * This,
            /* [in] */ IDynamicStreamerCallback *pCallback);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *SetFilter )( 
            IDynamicStreamer * This,
            /* [in] */ BSTR videoFilter);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *SetDirectFrameCallback )( 
            IDynamicStreamer * This,
            /* [in] */ IDynamicStreamerDecoderCallback *pCallback);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *GetSupportedCodecs )( 
            IDynamicStreamer * This,
            /* [retval][out] */ int *pFlags);
        
        END_INTERFACE
    } IDynamicStreamerVtbl;

    interface IDynamicStreamer
    {
        CONST_VTBL struct IDynamicStreamerVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IDynamicStreamer_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IDynamicStreamer_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IDynamicStreamer_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IDynamicStreamer_GetTypeInfoCount(This,pctinfo)	\
    ( (This)->lpVtbl -> GetTypeInfoCount(This,pctinfo) ) 

#define IDynamicStreamer_GetTypeInfo(This,iTInfo,lcid,ppTInfo)	\
    ( (This)->lpVtbl -> GetTypeInfo(This,iTInfo,lcid,ppTInfo) ) 

#define IDynamicStreamer_GetIDsOfNames(This,riid,rgszNames,cNames,lcid,rgDispId)	\
    ( (This)->lpVtbl -> GetIDsOfNames(This,riid,rgszNames,cNames,lcid,rgDispId) ) 

#define IDynamicStreamer_Invoke(This,dispIdMember,riid,lcid,wFlags,pDispParams,pVarResult,pExcepInfo,puArgErr)	\
    ( (This)->lpVtbl -> Invoke(This,dispIdMember,riid,lcid,wFlags,pDispParams,pVarResult,pExcepInfo,puArgErr) ) 


#define IDynamicStreamer_SetEncoder(This,videoCodec,videoOptions,fallbackvideoCodec,fallbackVideoOptions,videoMaxBitrate,audioCodec,audioOptions,audioMaxBitrate)	\
    ( (This)->lpVtbl -> SetEncoder(This,videoCodec,videoOptions,fallbackvideoCodec,fallbackVideoOptions,videoMaxBitrate,audioCodec,audioOptions,audioMaxBitrate) ) 

#define IDynamicStreamer_SetInput(This,type,input,options,fps,width,height)	\
    ( (This)->lpVtbl -> SetInput(This,type,input,options,fps,width,height) ) 

#define IDynamicStreamer_AddOutput(This,type,input,options,pCallback,pId)	\
    ( (This)->lpVtbl -> AddOutput(This,type,input,options,pCallback,pId) ) 

#define IDynamicStreamer_RemoveOutput(This,id)	\
    ( (This)->lpVtbl -> RemoveOutput(This,id) ) 

#define IDynamicStreamer_GetStatistics(This,pStats)	\
    ( (This)->lpVtbl -> GetStatistics(This,pStats) ) 

#define IDynamicStreamer_SetCallback(This,pCallback)	\
    ( (This)->lpVtbl -> SetCallback(This,pCallback) ) 

#define IDynamicStreamer_SetFilter(This,videoFilter)	\
    ( (This)->lpVtbl -> SetFilter(This,videoFilter) ) 

#define IDynamicStreamer_SetDirectFrameCallback(This,pCallback)	\
    ( (This)->lpVtbl -> SetDirectFrameCallback(This,pCallback) ) 

#define IDynamicStreamer_GetSupportedCodecs(This,pFlags)	\
    ( (This)->lpVtbl -> GetSupportedCodecs(This,pFlags) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IDynamicStreamer_INTERFACE_DEFINED__ */


#ifndef __IDynamicStreamerStatistics_INTERFACE_DEFINED__
#define __IDynamicStreamerStatistics_INTERFACE_DEFINED__

/* interface IDynamicStreamerStatistics */
/* [unique][nonextensible][dual][uuid][object] */ 


EXTERN_C const IID IID_IDynamicStreamerStatistics;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("9738B6EE-8144-4105-955E-9F9FEA664EAF")
    IDynamicStreamerStatistics : public IDispatch
    {
    public:
        virtual /* [id][propget] */ HRESULT STDMETHODCALLTYPE get_Interval( 
            /* [retval][out] */ LONGLONG *pVal) = 0;
        
        virtual /* [id][propget] */ HRESULT STDMETHODCALLTYPE get_Overall( 
            /* [retval][out] */ BOOL *pVal) = 0;
        
        virtual /* [id][propget] */ HRESULT STDMETHODCALLTYPE get_Id( 
            /* [retval][out] */ int *pVal) = 0;
        
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE GetValues( 
            /* [retval][out] */ SAFEARRAY * *pValues) = 0;
        
        virtual /* [id] */ HRESULT STDMETHODCALLTYPE GetError( 
            /* [out] */ BSTR *errorMessage,
            /* [retval][out] */ int *error) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IDynamicStreamerStatisticsVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            IDynamicStreamerStatistics * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            IDynamicStreamerStatistics * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            IDynamicStreamerStatistics * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetTypeInfoCount )( 
            IDynamicStreamerStatistics * This,
            /* [out] */ UINT *pctinfo);
        
        HRESULT ( STDMETHODCALLTYPE *GetTypeInfo )( 
            IDynamicStreamerStatistics * This,
            /* [in] */ UINT iTInfo,
            /* [in] */ LCID lcid,
            /* [out] */ ITypeInfo **ppTInfo);
        
        HRESULT ( STDMETHODCALLTYPE *GetIDsOfNames )( 
            IDynamicStreamerStatistics * This,
            /* [in] */ REFIID riid,
            /* [size_is][in] */ LPOLESTR *rgszNames,
            /* [range][in] */ UINT cNames,
            /* [in] */ LCID lcid,
            /* [size_is][out] */ DISPID *rgDispId);
        
        /* [local] */ HRESULT ( STDMETHODCALLTYPE *Invoke )( 
            IDynamicStreamerStatistics * This,
            /* [annotation][in] */ 
            _In_  DISPID dispIdMember,
            /* [annotation][in] */ 
            _In_  REFIID riid,
            /* [annotation][in] */ 
            _In_  LCID lcid,
            /* [annotation][in] */ 
            _In_  WORD wFlags,
            /* [annotation][out][in] */ 
            _In_  DISPPARAMS *pDispParams,
            /* [annotation][out] */ 
            _Out_opt_  VARIANT *pVarResult,
            /* [annotation][out] */ 
            _Out_opt_  EXCEPINFO *pExcepInfo,
            /* [annotation][out] */ 
            _Out_opt_  UINT *puArgErr);
        
        /* [id][propget] */ HRESULT ( STDMETHODCALLTYPE *get_Interval )( 
            IDynamicStreamerStatistics * This,
            /* [retval][out] */ LONGLONG *pVal);
        
        /* [id][propget] */ HRESULT ( STDMETHODCALLTYPE *get_Overall )( 
            IDynamicStreamerStatistics * This,
            /* [retval][out] */ BOOL *pVal);
        
        /* [id][propget] */ HRESULT ( STDMETHODCALLTYPE *get_Id )( 
            IDynamicStreamerStatistics * This,
            /* [retval][out] */ int *pVal);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *GetValues )( 
            IDynamicStreamerStatistics * This,
            /* [retval][out] */ SAFEARRAY * *pValues);
        
        /* [id] */ HRESULT ( STDMETHODCALLTYPE *GetError )( 
            IDynamicStreamerStatistics * This,
            /* [out] */ BSTR *errorMessage,
            /* [retval][out] */ int *error);
        
        END_INTERFACE
    } IDynamicStreamerStatisticsVtbl;

    interface IDynamicStreamerStatistics
    {
        CONST_VTBL struct IDynamicStreamerStatisticsVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IDynamicStreamerStatistics_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IDynamicStreamerStatistics_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IDynamicStreamerStatistics_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IDynamicStreamerStatistics_GetTypeInfoCount(This,pctinfo)	\
    ( (This)->lpVtbl -> GetTypeInfoCount(This,pctinfo) ) 

#define IDynamicStreamerStatistics_GetTypeInfo(This,iTInfo,lcid,ppTInfo)	\
    ( (This)->lpVtbl -> GetTypeInfo(This,iTInfo,lcid,ppTInfo) ) 

#define IDynamicStreamerStatistics_GetIDsOfNames(This,riid,rgszNames,cNames,lcid,rgDispId)	\
    ( (This)->lpVtbl -> GetIDsOfNames(This,riid,rgszNames,cNames,lcid,rgDispId) ) 

#define IDynamicStreamerStatistics_Invoke(This,dispIdMember,riid,lcid,wFlags,pDispParams,pVarResult,pExcepInfo,puArgErr)	\
    ( (This)->lpVtbl -> Invoke(This,dispIdMember,riid,lcid,wFlags,pDispParams,pVarResult,pExcepInfo,puArgErr) ) 


#define IDynamicStreamerStatistics_get_Interval(This,pVal)	\
    ( (This)->lpVtbl -> get_Interval(This,pVal) ) 

#define IDynamicStreamerStatistics_get_Overall(This,pVal)	\
    ( (This)->lpVtbl -> get_Overall(This,pVal) ) 

#define IDynamicStreamerStatistics_get_Id(This,pVal)	\
    ( (This)->lpVtbl -> get_Id(This,pVal) ) 

#define IDynamicStreamerStatistics_GetValues(This,pValues)	\
    ( (This)->lpVtbl -> GetValues(This,pValues) ) 

#define IDynamicStreamerStatistics_GetError(This,errorMessage,error)	\
    ( (This)->lpVtbl -> GetError(This,errorMessage,error) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IDynamicStreamerStatistics_INTERFACE_DEFINED__ */



#ifndef __FFDynamicStreamerLib_LIBRARY_DEFINED__
#define __FFDynamicStreamerLib_LIBRARY_DEFINED__

/* library FFDynamicStreamerLib */
/* [version][uuid] */ 



EXTERN_C const IID LIBID_FFDynamicStreamerLib;

EXTERN_C const CLSID CLSID_DynamicStreamer;

#ifdef __cplusplus

class DECLSPEC_UUID("E070BC4F-392A-43AA-9E89-039B68859242")
DynamicStreamer;
#endif

EXTERN_C const CLSID CLSID_DynamicStreamerStatistics;

#ifdef __cplusplus

class DECLSPEC_UUID("F11DB7A9-C9D8-4992-8AFD-4C03A00AC5E4")
DynamicStreamerStatistics;
#endif
#endif /* __FFDynamicStreamerLib_LIBRARY_DEFINED__ */

/* Additional Prototypes for ALL interfaces */

unsigned long             __RPC_USER  BSTR_UserSize(     unsigned long *, unsigned long            , BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserMarshal(  unsigned long *, unsigned char *, BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserUnmarshal(unsigned long *, unsigned char *, BSTR * ); 
void                      __RPC_USER  BSTR_UserFree(     unsigned long *, BSTR * ); 

unsigned long             __RPC_USER  LPSAFEARRAY_UserSize(     unsigned long *, unsigned long            , LPSAFEARRAY * ); 
unsigned char * __RPC_USER  LPSAFEARRAY_UserMarshal(  unsigned long *, unsigned char *, LPSAFEARRAY * ); 
unsigned char * __RPC_USER  LPSAFEARRAY_UserUnmarshal(unsigned long *, unsigned char *, LPSAFEARRAY * ); 
void                      __RPC_USER  LPSAFEARRAY_UserFree(     unsigned long *, LPSAFEARRAY * ); 

unsigned long             __RPC_USER  BSTR_UserSize64(     unsigned long *, unsigned long            , BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserMarshal64(  unsigned long *, unsigned char *, BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserUnmarshal64(unsigned long *, unsigned char *, BSTR * ); 
void                      __RPC_USER  BSTR_UserFree64(     unsigned long *, BSTR * ); 

unsigned long             __RPC_USER  LPSAFEARRAY_UserSize64(     unsigned long *, unsigned long            , LPSAFEARRAY * ); 
unsigned char * __RPC_USER  LPSAFEARRAY_UserMarshal64(  unsigned long *, unsigned char *, LPSAFEARRAY * ); 
unsigned char * __RPC_USER  LPSAFEARRAY_UserUnmarshal64(unsigned long *, unsigned char *, LPSAFEARRAY * ); 
void                      __RPC_USER  LPSAFEARRAY_UserFree64(     unsigned long *, LPSAFEARRAY * ); 

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


