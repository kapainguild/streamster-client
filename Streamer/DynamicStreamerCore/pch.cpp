// pch.cpp: source file corresponding to the pre-compiled header

#include "pch.h"

// When you are using pre-compiled headers, this source file is necessary for compilation to succeed.

typedef void (*LogCallbackFunction)(int, const char*, const char*);

extern "C"
{
	DLL_EXPORT(void) Core_Init(LogCallbackFunction onLog, StreamerConstants* constants);

	DLL_EXPORT(int) Core_GetErrorMessage(int error, int bufferLength, char* buffer);
}

LogCallbackFunction s_onLog = nullptr;


void Log(int bffmpeg, int severity, const char* msg, va_list params)
{
	if (s_onLog)
	{
		char buf[2048];

		vsnprintf(buf, sizeof(buf), msg, params);

		s_onLog(severity, msg, buf);
	}
}

void Info(const char* msg, ...)
{
	va_list arglist;

	va_start(arglist, msg);
	Log(0, 1, msg, arglist);
	va_end(arglist);
}

void Warning(const char* msg, ...)
{
	va_list arglist;

	va_start(arglist, msg);
	Log(0, 2, msg, arglist);
	va_end(arglist);
}

void Error(const char* msg, ...)
{
	va_list arglist;

	va_start(arglist, msg);
	Log(0, 3, msg, arglist);
	va_end(arglist);
}

void Error(const streamer_exception& e, const char* msg)
{
	Error("%s failed (%d: %s)", msg, e.errorCode, e.what());
}

int GetReturnCode(const streamer_exception& e)
{
	if (e.errorCode)
		return e.errorCode;
	return ErrorCodes::InternalErrorUnknown;
}

int LogAndReturn(const streamer_exception& e, const char* msg)
{
	Error(e, msg);
	return GetReturnCode(e); 
}




void logHandler(void*, int level, const char* msg, va_list params)
{
	if (level <= FFMPEG_LOG_LEVEL)
		Log(1, 1, msg, params);
}

DLL_EXPORT(void) Core_Init(LogCallbackFunction onLog, StreamerConstants* constants)
{
	s_onLog = onLog;

	av_log_set_level(FFMPEG_LOG_LEVEL);
	av_log_set_callback(logHandler);

	avdevice_register_all();

	*constants = StreamerConstants();
}

DLL_EXPORT(int) Core_GetErrorMessage(int error, int bufferLength, char* buffer)
{
	return av_strerror(error, buffer, bufferLength);
}

__int64 CurrentTime()
{
	FILETIME time;
	GetSystemTimeAsFileTime(&time);
	return *(__int64*)&time;
}


