#include "pch.h"
#include "Common.h"



extern "C"
{
	DLL_EXPORT(AVPacket*) Packet_Create();
	DLL_EXPORT(void) Packet_Delete(AVPacket* packet);
	DLL_EXPORT(void) Packet_Unref(AVPacket* packet);
	DLL_EXPORT(void) Packet_CopyContentFrom(AVPacket* packet, AVPacket* from);
	DLL_EXPORT(void) Packet_InitFromBuffer(AVPacket* packet, uint8_t* from, int length);
	DLL_EXPORT(void) Packet_InitFromBuffer2(AVPacket* packet, uint8_t* from, int length, int64_t pts);
	DLL_EXPORT(void) Packet_InitFromBuffer5(AVPacket* packet, uint8_t* from, int length, int64_t pts, int streamIndex);
	DLL_EXPORT(void) Packet_InitFromBuffer3(AVPacket* packet, uint8_t* from, int length, int64_t pts, PacketProperties* PacketProperties);
	DLL_EXPORT(int) Packet_InitFromBuffer4(AVPacket* packet, uint8_t* from, int bitPerPixel, int width, int height, int sourceWidth, int64_t pts, int checkForZero);
	DLL_EXPORT(void) Packet_RescaleTimebase(AVPacket* packet, AVRational* from, AVRational* to, PacketProperties* PacketProperties);
	DLL_EXPORT(void) Packet_RescaleTimebase(AVPacket* packet, AVRational* from, AVRational* to, PacketProperties* PacketProperties);
	DLL_EXPORT(void) Packet_SetPts(AVPacket* packet, int64_t pts);

	DLL_EXPORT(AVFrame*) Frame_Create();
	DLL_EXPORT(void) Frame_Delete(AVFrame* frame);
	DLL_EXPORT(void) Frame_Unref(AVFrame* frame);
	DLL_EXPORT(void) Frame_CopyContentFrom(AVFrame* frame, AVFrame* from);
	DLL_EXPORT(void) Frame_RescaleTimebase(AVFrame* frame, AVRational* from, AVRational* to, FrameProperties* FrameProperties);
	DLL_EXPORT(void) Frame_CopyContentFromAndSetPts(AVFrame* frame, AVFrame* from, int64_t pts);
	DLL_EXPORT(void) Frame_Init(AVFrame* frame, int widht, int height, int pix_fmt, int64_t pts, int planesCount, FramePlaneDesc* planes, FrameProperties* FrameProperties);
	DLL_EXPORT(void) Frame_GenerateSilence(AVFrame* frame, int64_t pts, FrameProperties* FrameProperties);
	DLL_EXPORT(void) Frame_SetPts(AVFrame* frame, int64_t pts);
}


DLL_EXPORT(AVPacket*) Packet_Create()
{
	return new AVPacket();
}

DLL_EXPORT(void) Packet_Delete(AVPacket* packet)
{
	delete packet;
}

DLL_EXPORT(AVFrame*) Frame_Create()
{
	return av_frame_alloc();
}

DLL_EXPORT(void) Frame_Delete(AVFrame* frame)
{
	av_frame_free(&frame);
}

DLL_EXPORT(void) Frame_Unref(AVFrame* frame)
{
	av_frame_unref(frame);
}

DLL_EXPORT(void) Frame_CopyContentFrom(AVFrame* frame, AVFrame* from)
{
	av_frame_ref(frame, from);
}

DLL_EXPORT(void) Frame_CopyContentFromAndSetPts(AVFrame* frame, AVFrame* from, int64_t pts)
{
	av_frame_ref(frame, from);
	frame->pts = pts;
}

DLL_EXPORT(void) Frame_SetPts(AVFrame* frame, int64_t pts)
{
	frame->pts = pts;
}


DLL_EXPORT(void) Packet_Unref(AVPacket* packet)
{
	av_packet_unref(packet);
}

DLL_EXPORT(void) Frame_RescaleTimebase(AVFrame* frame, AVRational* from, AVRational* to, FrameProperties* frameProperties)
{
	if (frame->pts != AV_NOPTS_VALUE)
		frame->pts = av_rescale_q(frame->pts, *from, *to);
	FrameProperties::FromAVFrame(frameProperties, frame);
}

float* silence = nullptr;


DLL_EXPORT(void) Frame_GenerateSilence(AVFrame* frame, int64_t pts, FrameProperties* FrameProperties)
{
	if (!silence)
	{
		silence = new float[441];
		for (int q = 0; q < 441; q++)
			silence[q] = 0.0;
	}

	frame->pts = pts;
	frame->format = 8;
	frame->nb_samples = 441;
	frame->sample_rate = 44100;
	frame->channel_layout = 3;
	av_frame_get_buffer(frame, 0);

	for (int q = 0; q < 2; q++)
	{
		memcpy(frame->data[q], &silence[0], 441 * 4);
	}
	FrameProperties::FromAVFrame(FrameProperties, frame);
}

DLL_EXPORT(void) Frame_Init(AVFrame* frame, int width, int height, int pix_fmt, int64_t pts, int planesCount, FramePlaneDesc* planes, FrameProperties* FrameProperties)
{
	frame->width = width;
	frame->height = height;
	frame->format = pix_fmt;
	frame->pts = pts;

	av_frame_get_buffer(frame, 0);

	for (int q = 0; q < planesCount; q++)
	{
		if (frame->linesize[q] == planes[q].Stride)
			memcpy(frame->data[q], planes[q].DataPtr, (size_t)planes[q].Stride * planes[q].StrideCount);
		else
		{
			int linesize = frame->linesize[q];
			uint8_t* target = frame->data[q];
			uint8_t* source = (uint8_t*)planes[q].DataPtr;
			int sourceStride = planes[q].Stride;

			int strideCount = planes[q].StrideCount;

			for (int w = 0; w < strideCount; w++)
			{
				memcpy(target, source, linesize);

				target += linesize;
				source += sourceStride;
			}
		}
	}
	FrameProperties::FromAVFrame(FrameProperties, frame);
}

DLL_EXPORT(void) Packet_CopyContentFrom(AVPacket* packet, AVPacket* from)
{
	av_packet_ref(packet, from);
}

DLL_EXPORT(void) Packet_InitFromBuffer(AVPacket* packet, uint8_t* from, int length)
{
	uint8_t* buffer_data = (uint8_t*)av_malloc(length);
	memcpy(buffer_data, from, length);
	av_packet_from_data(packet, buffer_data, length);
}

DLL_EXPORT(int) Packet_InitFromBuffer4(AVPacket* packet, uint8_t* from, int bitPerPixel, int width, int height, int sourceWidth, int64_t pts, int checkForZero)
{
	int stride = width * bitPerPixel;
	int strideInInteger = stride >> 2;

	int length = stride * height;
	uint8_t* buffer_data = (uint8_t*)av_malloc(length);

	uint8_t* target = buffer_data;
	uint8_t* source = from;

	//auto start = CurrentTime();

	for (int q = 0; q < height; q++)
	{
		memcpy(target, source, stride);

		if (checkForZero)
		{
			int* intArray = (int*)target;
			for (int w = 0; w < strideInInteger; w++)
			{
				if (*intArray != 0)
				{
					checkForZero = 0;
					break;
				}
				intArray++;
			}
		}

		target += stride;
		source += sourceWidth;
	}

	//auto copy = (CurrentTime() - start) / 10000;

	av_packet_from_data(packet, buffer_data, length);
	packet->dts = pts;
	packet->pts = pts;

	return !checkForZero;
}

DLL_EXPORT(void) Packet_InitFromBuffer2(AVPacket* packet, uint8_t* from, int length, int64_t pts)
{
	Packet_InitFromBuffer(packet, from, length);
	packet->dts = pts;
	packet->pts = pts;
}

DLL_EXPORT(void) Packet_InitFromBuffer5(AVPacket* packet, uint8_t* from, int length, int64_t pts, int streamIndex)
{
	Packet_InitFromBuffer(packet, from, length);
	packet->dts = pts;
	packet->pts = pts;
	packet->stream_index = streamIndex;
}


DLL_EXPORT(void) Packet_SetPts(AVPacket* packet, int64_t pts)
{
	packet->dts = pts;
	packet->pts = pts;
}

DLL_EXPORT(void) Packet_InitFromBuffer3(AVPacket* packet, uint8_t* from, int length, int64_t pts, PacketProperties* PacketProperties)
{
	Packet_InitFromBuffer(packet, from, length);
	packet->dts = pts;
	packet->pts = pts;
	PacketProperties::FromAVPacket(PacketProperties, packet);
}


DLL_EXPORT(void) Packet_RescaleTimebase(AVPacket* packet, AVRational* from, AVRational* to, PacketProperties* packetProperties)
{
	av_packet_rescale_ts(packet, *from, *to);
	PacketProperties::FromAVPacket(packetProperties, packet);
}
