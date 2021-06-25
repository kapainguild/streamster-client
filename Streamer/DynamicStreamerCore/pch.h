// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here
#include "framework.h"

#define DLL_EXPORT(a)  __declspec( dllexport ) a __stdcall


#define FFMPEG_LOG_LEVEL AV_LOG_INFO
//#define FFMPEG_LOG_LEVEL AV_LOG_TRACE
//#define FFMPEG_LOG_LEVEL AV_LOG_VERBOSE


#define KEY_VALUE_SEPARATOR "^"

#define PAIRS_SEPARATOR "`"

#define S1(x) #x
#define LOCATION __FILE__ " ( " S1(__LINE__) " )"

class streamer_exception : public std::exception
{
public:
	const char* line;
	int errorCode;

	streamer_exception(const char* msg, int nerrorCode, const char* cline) : std::exception(msg), errorCode(nerrorCode), line(cline)
	{
	}
};

#define CHECK(stat) do { int ret = stat; if (ret < 0) {throw streamer_exception(#stat, ret, LOCATION);} } while(0);
#define THROW(msg) { throw streamer_exception(msg, 0, LOCATION); }
#define CATCH_STD catch (streamer_exception)
#define CATCH_USE catch (const streamer_exception& e)
#define RETHROW throw

enum ErrorCodes : int
{
	Ok = 0,
	InternalErrorUnknown = -33000000,
	InternalErrorUnknown1 = -33000001,
	InternalErrorUnknown2 = -33000002,
	InternalErrorUnknown3 = -33000003,
	InternalErrorUnknown4 = -33000004,
	InternalErrorUnknown5 = -33000005,
	InternalErrorLast = -33001000,
};

void Log(int bffmpeg, int severity, const char* msg, va_list params);
void Info(const char* msg, ...);
void Error(const char* msg, ...);
void Warning(const char* msg, ...);

void Error(const streamer_exception& e, const char* msg);

int GetReturnCode(const streamer_exception& e);

int LogAndReturn(const streamer_exception& e, const char* msg);

__int64 CurrentTime();



#include "Common.h"
#include "StreamerConstants.h"


#endif //PCH_H
