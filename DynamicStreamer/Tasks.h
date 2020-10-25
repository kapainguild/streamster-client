#pragma once

class BaseTask
{
public:
	int StreamIndex;
	int PacketNumber;
	ULONGLONG StartTime;

	void CopyFrom(BaseTask* other)
	{
		StreamIndex = other->StreamIndex;
		PacketNumber = other->PacketNumber;
		StartTime = other->StartTime;
	}
};

class FrameTask : public BaseTask
{
public:
	AVFrame* Frame;
	AVPacket Packet;

	FrameTask()
	{
		Frame = av_frame_alloc();
		av_init_packet(&Packet);
	}

	~FrameTask()
	{
		av_frame_free(&Frame);
	}
};

class DecoderTask : public BaseTask
{
public:
	AVPacket Packet;
};

