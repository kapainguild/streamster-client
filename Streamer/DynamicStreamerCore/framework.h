#pragma once

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files
#include <windows.h>

#include <exception>
#include <string>
#include <vector>

extern "C"
{
#include "libavutil/imgutils.h"
#include "libavutil/samplefmt.h"
//#include "libavutil/hwcontext_d3d11va.h"
#include "libavformat/avformat.h"
#include "libavcodec/avcodec.h"
#include "libswscale/swscale.h"
#include "libavdevice/avdevice.h"
#include "libavfilter/avfilter.h"
#include <libavfilter/buffersink.h>
#include <libavfilter/buffersrc.h>
}

