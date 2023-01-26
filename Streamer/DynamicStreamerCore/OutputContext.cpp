#include "pch.h"
#include "OutputContext.h"

typedef int (*ReadInterruptCallbackFunction)();

extern "C"
{
	DLL_EXPORT(OutputContext*) OutputContext_Create();
	DLL_EXPORT(void) OutputContext_Delete(OutputContext* handle);
	DLL_EXPORT(int) OutputContext_Open(OutputContext* handle, char* type, char* input, char* options, int streamCount, OutputStreamProperties* codecProps, ReadInterruptCallbackFunction readInterruptCallbackFunction);
	DLL_EXPORT(int) OutputContext_Write(OutputContext* handle, AVPacket* hPacket, int stream);
}


DLL_EXPORT(OutputContext*) OutputContext_Create()
{
	return new OutputContext();
}

DLL_EXPORT(void) OutputContext_Delete(OutputContext* handle)
{
	delete handle;
}

int CheckOutputInterrupt(void* t)
{
	if (t)
		return ((ReadInterruptCallbackFunction)t)();
	return 0;
}

DLL_EXPORT(int) OutputContext_Open(OutputContext* handle, char* type, char* output, char* options, int streamCount, OutputStreamProperties* streamProps, ReadInterruptCallbackFunction readInterruptCallbackFunction)
{
	auto codecpar = avcodec_parameters_alloc();
	try
	{
		CHECK(avformat_alloc_output_context2(&handle->context, nullptr, type, output));

		handle->time_bases.clear();
		
		for (int i = 0; i < streamCount; i++)
		{
			AVStream* out_stream = avformat_new_stream(handle->context, nullptr);
			if (!out_stream)
				THROW("Failed allocating output stream"); 

			CodecProperties::CopyParams(codecpar, &streamProps[i].CodecProps);
			CHECK(avcodec_parameters_copy(out_stream->codecpar, codecpar));

			handle->time_bases.push_back(streamProps->input_time_base);
		}

		AVDictionary* dict = nullptr;
		if (options)
			CHECK(av_dict_parse_string(&dict, options, KEY_VALUE_SEPARATOR, PAIRS_SEPARATOR, 0));

		Info("Opening out %s", output);

		handle->interrupter.opaque = readInterruptCallbackFunction;
		handle->interrupter.callback = &CheckOutputInterrupt;

		
		if (!(handle->context->oformat->flags & AVFMT_NOFILE))
			CHECK(avio_open2(&handle->context->pb, output, AVIO_FLAG_WRITE, &handle->interrupter, &dict));

		if (dict)
			THROW("Options are not accepted");

		CHECK(avformat_write_header(handle->context, NULL));
		handle->writeTrailer = true;
		avcodec_parameters_free(&codecpar);
	}
	catch (const streamer_exception& e)
	{
		avcodec_parameters_free(&codecpar);
		return LogAndReturn(e, "OutputContext_Open");
	}
	return ErrorCodes::Ok;
}

DLL_EXPORT(int) OutputContext_Write(OutputContext* handle, AVPacket* hPacket, int stream)
{
	if (stream < handle->time_bases.size())
	{
		hPacket->stream_index = stream;
		if (handle->baseTime == 0)
		{
			handle->baseTime = hPacket->pts;
		}
		hPacket->pts -= handle->baseTime;
		hPacket->dts -= handle->baseTime;

		av_packet_rescale_ts(hPacket, handle->time_bases[stream], handle->context->streams[stream]->time_base);

		return av_write_frame(handle->context, hPacket);
	}
	else
	{
		Warning("Attempt to write packet to stream #%d", stream); // audio ?
		return ErrorCodes::Ok;
	}
}
