// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently,
// but are changed infrequently

#pragma once

#ifndef STRICT
#define STRICT
#endif

#include <SDKDDKVer.h>

#define _ATL_APARTMENT_THREADED

#define _ATL_NO_AUTOMATIC_NAMESPACE

#define _ATL_CSTRING_EXPLICIT_CONSTRUCTORS	// some CString constructors will be explicit


#define ATL_NO_ASSERT_ON_DESTROY_NONEXISTENT_WINDOW

#include "resource.h"
#include <atlbase.h>
#include <atlcom.h>
#include <atlctl.h>
#include <atlstr.h>
#include <vector>
#include <deque>
#include <atlsafe.h>
#include <inttypes.h>

extern "C"
{
#include "libavutil/imgutils.h"
#include "libavutil/samplefmt.h"
#include "libavformat/avformat.h"
#include "libavcodec\avcodec.h"
#include "libswscale/swscale.h"
#include "libavdevice\avdevice.h"
#include "libavfilter\avfilter.h"
#include <libavfilter/buffersink.h>
#include <libavfilter/buffersrc.h>

}



void SetThreadName(DWORD dwThreadID, const char* threadName);





class CriticalSectionLock
{
public:
	// constructor
	CriticalSectionLock(CRITICAL_SECTION& cs) : m_cs(cs)
	{
		EnterCriticalSection(&m_cs);
	}

	// destructor
	~CriticalSectionLock()
	{
		LeaveCriticalSection(&m_cs);
	}

private:
	// Disable copy constructor
	CriticalSectionLock(const CriticalSectionLock&) = delete;

private:
	CRITICAL_SECTION& m_cs;
};


using namespace ATL;

#define S1(x) #x
#define S2(x) S1(x)
#define LOCATION __FILE__ " ( " S2(__LINE__) " )"

class streamer_exception : public std::exception
{
public:
	const char* line;
	int errorCode;

	streamer_exception(const char* msg, int nerrorCode, const char* cline) : std::exception(msg), errorCode(nerrorCode), line(cline)
	{
	}
};


#define CHECK(stat) do { int ret = stat; if (ret < 0) {/*ATLASSERT(0);*/throw streamer_exception(NULL, ret, LOCATION);} } while(0);
#define THROW(msg) {/*ATLASSERT(0);*/throw streamer_exception(msg, 0, LOCATION);}
#define CATCH_STD catch (streamer_exception)
#define CATCH_USE catch (const streamer_exception& e)
#define RETHROW throw
