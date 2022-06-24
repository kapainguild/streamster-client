#include "pch.h"
#include "InputContext.h"


typedef int (*ReadInterruptCallbackFunction)();

extern "C"
{
	DLL_EXPORT(InputContext*) InputContext_Create();

	DLL_EXPORT(void) InputContext_Delete(InputContext* handle);

	DLL_EXPORT(int) InputContext_Open(InputContext* handle, char* type, char* input, char* options, AVRational* translate_to_time_base, ReadInterruptCallbackFunction readInterruptCallbackFunction);

	DLL_EXPORT(int) InputContext_Read(InputContext* handle, AVPacket* hPacket, PacketProperties* PacketProperties);

	DLL_EXPORT(int) InputContext_Analyze(InputContext* handle, int durationMs);

	DLL_EXPORT(int) InputContext_GetStreamInfo(InputContext* handle, int stream, InputStreamProperties* codecProperties);
}


DLL_EXPORT(InputContext*) InputContext_Create()
{ 
	return new InputContext(); 
}

DLL_EXPORT(void) InputContext_Delete(InputContext* handle)
{
	delete handle;
}


int CheckInterrupt(void* t)
{
	if (t)
		return ((ReadInterruptCallbackFunction)t)();
	return 0;
}

DLL_EXPORT(int) InputContext_Open(InputContext* handle, char* type, char* input, char* options, AVRational* translate_to_time_base, ReadInterruptCallbackFunction readInterruptCallbackFunction)
{
	try
	{
		AVInputFormat* format = av_find_input_format(type);
		if (!format)
			THROW("Format not found");

		AVDictionary* dict = nullptr;
		CHECK(av_dict_parse_string(&dict, options, KEY_VALUE_SEPARATOR, PAIRS_SEPARATOR, 0));

		AVDictionaryEntry* codec = av_dict_get(dict, "vcodec", NULL, 0);
		if (codec)
		{
			AVCodec* found = avcodec_find_decoder_by_name(codec->value);
			if (found)
				handle->context->video_codec_id = found->id;
			av_dict_set(&dict, "vcodec", NULL, 0);
		}

		if (readInterruptCallbackFunction)
		{
			handle->context->interrupt_callback.callback = &CheckInterrupt;
			handle->context->interrupt_callback.opaque = readInterruptCallbackFunction;
		}

		handle->desired_time_base = *translate_to_time_base;

		CHECK(avformat_open_input(&handle->context, input, format, &dict));

		if (dict)
			THROW("input options are not accepted");
		
		Info("Input opened");
	}
	catch (const streamer_exception& e)
	{
		if (input && strstr(input, "listen") && e.errorCode == -1414092869) //interuppted error code
		{
			Info("InputContext_Open listen timeout");
			return GetReturnCode(e);
		}

		return LogAndReturn(e, "InputContext_Open");
	}
	return ErrorCodes::Ok;
}

DLL_EXPORT(int) InputContext_Analyze(InputContext* handle, int durationMs)
{
	handle->context->max_analyze_duration = (int64_t)durationMs * 1000;
	try
	{
		CHECK(avformat_find_stream_info(handle->context, NULL));
		return handle->context->nb_streams;
	}
	catch (const streamer_exception& e)
	{
		return LogAndReturn(e, "InputContext_Analyze");
	}
}


DLL_EXPORT(int) InputContext_GetStreamInfo(InputContext* handle, int stream, InputStreamProperties* i)
{
	try
	{
		if (stream < 0 || stream >= (int)handle->context->nb_streams)
			THROW("Stream is out of range");

		if (!handle->context->streams)
			THROW("Streams are null");

		if (!handle->context->streams[stream])
			THROW("The stream is null");

		if (!handle->context->streams[stream]->codecpar)
			THROW("The stream's params is null");

		CodecProperties::CopyParams(&i->CodecProps, handle->context->streams[stream]->codecpar);
	}
	catch (const streamer_exception& e)
	{
		return LogAndReturn(e, "InputContext_Analyze");
	}

	return ErrorCodes::Ok;
}

DLL_EXPORT(int) InputContext_Read(InputContext* handle, AVPacket* packet, PacketProperties* PacketProperties)
{
	int res = av_read_frame(handle->context, packet);
	if (res == 0)
	{
		if (handle->desired_time_base.den != 0)
			av_packet_rescale_ts(packet, handle->context->streams[packet->stream_index]->time_base, handle->desired_time_base);
		PacketProperties::FromAVPacket(PacketProperties, packet);
	}
	return res;
}
