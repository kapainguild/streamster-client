#pragma once
class EncoderContext
{
public:
	AVCodecContext* context = nullptr;

	EncoderContext()
	{
	}

	~EncoderContext()
	{
		avcodec_free_context(&context);
	}
};

enum EncoderQuality
{
	Quality = 0, BalancedQuality = 1, Balanced = 2, Speed = 3
};



struct EncoderSpec
{
	//video
	AVRational sample_aspect_ratio;
	int width;
	int height;

	//audio
	int sample_rate;
	uint64_t channel_layout;

	//common
	AVRational time_base;
	EncoderQuality Quality; // qsv-only so far
};

struct EncoderBitrate
{
	int bit_rate;
	int max_rate;
	int buffer_size;
};


struct EncoderProperties
{
	//video
	AVPixelFormat pix_fmt;

	//audio
	AVSampleFormat sample_fmt;
	int required_frame_size;
};