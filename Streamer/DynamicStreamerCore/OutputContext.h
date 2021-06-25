#pragma once



class OutputContext
{
public:
	AVFormatContext* context;

	AVIOInterruptCB interrupter;

	bool writeTrailer{ false };

	int64_t baseTime{ 0 }; 

	std::vector<AVRational> time_bases;

	OutputContext()
	{
		interrupter = AVIOInterruptCB();
		interrupter.callback = nullptr;
		interrupter.opaque = nullptr;

		context = avformat_alloc_context();
	}

	~OutputContext()
	{
		if (context)
		{
			if (writeTrailer)
				av_write_trailer(context);
			if (context->oformat && !(context->oformat->flags & AVFMT_NOFILE) && context->pb)
				avio_closep(&context->pb);
			avformat_free_context(context);
			context = nullptr;
		}
	}
};

struct OutputStreamProperties
{
	CodecProperties CodecProps;

	AVRational input_time_base;
};

