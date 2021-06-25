#pragma once
class DecoderContext
{

public:
	AVCodecContext* context = nullptr;


	DecoderContext()
	{
	}

	~DecoderContext()
	{
		avcodec_free_context(&context);
	}
};


struct DecoderProperties
{
	AVPixelFormat pix_fmt;

	AVSampleFormat sample_fmt;
	uint64_t channel_layout;
};

