#pragma once

#include "QSV\QSV_Encoder.h"

class EncoderContextQsvDx
{
public:
	qsv_t* handle;
	qsv_param_t setup = { 0 };
	void* device;
	void* deviceCtx;

	bool platform_logged = false;
	bool updateBitratePending = false;

	AVPacket pkt;

	EncoderContextQsvDx()
	{
		av_init_packet(&pkt);
		handle = nullptr;
		device = nullptr;
		deviceCtx = nullptr;
	}

	~EncoderContextQsvDx()
	{
		if (handle)
		{
			qsv_encoder_close(handle);
			handle = nullptr;
		}
	}
};

