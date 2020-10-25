// DynamicStreamer.h : Declaration of the CDynamicStreamer

#pragma once
#include "resource.h"       // main symbols
#include "Statistics.h"
#include "FFDynamicStreamer_i.h"

#include "ProcessorThread.h"
#include "Tasks.h"

using namespace ATL;

class CDynamicStreamer;
class CDynamicStreamerStatistics;

typedef int(*dec_func)(AVCodecContext *, AVFrame *, int *, const AVPacket *);
typedef int(*enc_func)(AVCodecContext *avctx, AVPacket *avpkt, const AVFrame *frame, int *got_packet_ptr);
#define MAX_ENCODER_ERRORS 4

#define DECODERS_COUNT ((int)2)


class Measurer
{
public:
	LONG64 Start;
	LONG64* Target;

	static void SetTime(LONG64* target)
	{
		::QueryPerformanceCounter((LARGE_INTEGER*)target);
	}

	Measurer(LONG64* target)
	{
		Target = target;
		::QueryPerformanceCounter((LARGE_INTEGER*)&Start);
	}

	~Measurer()
	{
		LARGE_INTEGER end;
		::QueryPerformanceCounter(&end);
		::InterlockedAdd64(Target, end.QuadPart - Start);
	}

};

class StreamInfo
{
public:
	int count;
	int video_idx;
	int audio_idx;

	StreamInfo(StreamInfo& source)
	{
		count = source.count;
		video_idx = source.video_idx;
		audio_idx = source.audio_idx;
	}

	StreamInfo(int streams_count, int video, int audio)
	{
		count = streams_count;
		video_idx = video;
		audio_idx = audio;
	}
};

class FilteringContext
{
public:
	AVFilterContext *buffersink_ctx;
	AVFilterContext *buffersrc_ctx;
	AVFilterGraph *filter_graph;

	FilteringContext()
	{
		buffersink_ctx = NULL;
		buffersrc_ctx = NULL;
		filter_graph = NULL;
	}
	~FilteringContext()
	{
		buffersink_ctx = NULL;
		buffersrc_ctx = NULL;
		avfilter_graph_free(&filter_graph);
	}
};

class FilterRuntime
{
public:
	FilteringContext* filter_ctx;
	StreamInfo stream_info;

	FilterRuntime(StreamInfo& streamInfo) : stream_info(streamInfo)
	{
		filter_ctx = new FilteringContext[stream_info.count];
	}

	~FilterRuntime()
	{
		if (filter_ctx)
		{
			delete[] filter_ctx;
			filter_ctx = NULL;
		}
	}
};

class OutputParametersPerStream
{
public:
	AVRational timeBase;
	AVCodecParameters* codecpar;
	AVRational input_time_base;

	uint8_t* extradata;
	int extradata_size;

	OutputParametersPerStream()
	{
		codecpar = NULL;
		extradata_size = 0;
		extradata = NULL;
	}

	~OutputParametersPerStream()
	{
		if (codecpar)
			avcodec_parameters_free(&codecpar);

		if (extradata)
			delete[] extradata;
		extradata = NULL;
		codecpar = NULL;
	}
};

class OutputParameters
{
public:
	StreamInfo stream_info;
	OutputParametersPerStream* perStream;

	OutputParameters(StreamInfo& streamInfo) : stream_info(streamInfo)
	{
		perStream = new OutputParametersPerStream[stream_info.count];
	}

	~OutputParameters()
	{
		if (perStream)
			delete[] perStream;
		perStream = NULL;
	}
};


class OutputDescriptor
{
public:
	HANDLE thread;
	AVFormatContext* output_ctx;
	CStringA type;
	CStringA options;
	CStringA output;

	int inputVersion;
	volatile BOOL continue_thread;
	int id;
	CDynamicStreamer* parent;
	volatile int readerIdx;

	int errorCounter;
	BOOL outputInitialized;

	int initialErrorCounter;

	OutputParameters* pPendingParameters;
	OutputParameters* pActualParameters;

	Statistics statistics;

	IDynamicStreamerDecoderCallback* pCallback;
	FilterRuntime* callback_filter;
	AVCodecContext** callback_decoder_ctx;

	AVFrame* frame;
	AVFrame* filter_frame;

	int64_t baseTime;

	BOOL justReinitialized;

	int packetCounter;

	OutputDescriptor()
	{
		packetCounter = 0;
		id = -1;
		parent = NULL;
		thread = NULL;
		output_ctx = NULL;
		continue_thread = TRUE;
		readerIdx = 0;
		pCallback = NULL;
		callback_filter = NULL;
		callback_decoder_ctx = NULL;
		pPendingParameters = NULL;
		pActualParameters = NULL;
		inputVersion = 0;
		errorCounter = 0;
		outputInitialized = FALSE;
		initialErrorCounter = 0;

		frame = av_frame_alloc();
		filter_frame = av_frame_alloc();

		baseTime = 0;
		justReinitialized = FALSE;
	}

	~OutputDescriptor()
	{
		if (pCallback)
		{
			pCallback->Release();
			pCallback = NULL;
		}
		av_frame_free(&frame);
		av_frame_free(&filter_frame);

		if (pPendingParameters)
			delete pPendingParameters;

		if (pActualParameters)
			delete pActualParameters;

		pPendingParameters = NULL;
		pActualParameters = NULL;
	}

	void CloseOutput()
	{
		errorCounter = 0;
		outputInitialized = FALSE;
		if (output_ctx != NULL)
		{
			if (output_ctx && !(output_ctx->oformat->flags & AVFMT_NOFILE))
				avio_closep(&output_ctx->pb);
			avformat_free_context(output_ctx);
			output_ctx = NULL;
		}

		if (callback_decoder_ctx && pActualParameters)
		{
			for (int i = 0; i < pActualParameters->stream_info.count; i++)
			{
				avcodec_free_context(&callback_decoder_ctx[i]);
			}
			delete[] callback_decoder_ctx;
			callback_decoder_ctx = NULL;
		}

		if (callback_filter)
		{
			delete callback_filter;
			callback_filter = NULL;
		}
	}
};

class EncoderParameters
{
public:
	CStringA encoder_videoCodec;
	CStringA encoder_videoOptions;
	CStringA encoder_fallback_videoCodec;
	CStringA encoder_fallback_videoOptions;
	CStringA encoder_audioCodec;
	CStringA encoder_audioOptions;
	int encoder_videoMaxBitrate;
	int encoder_audioMaxBitrate;
};

class InputParameters
{
public:
	CStringA input_type;
	CStringA input_input;
	CStringA input_options;

	int Fps = 0;
	int Width = 0;
	int Height = 0;
};

class FilterParameters 
{
public:
	CStringA video_filter;
};

class DirectFrameParamiters
{
public:
	IDynamicStreamerDecoderCallback* pCallback = NULL;
};


class InputRuntime
{
public:
	AVRational* input_time_base;
	AVFormatContext* input_fmt_ctx;
	AVCodecContext** decoder_ctx;
	StreamInfo* stream_info;

	int64_t lastVideoPacket = 0;
	int64_t lastAudioPacket = 0;

	int64_t audioDelayStatistics = 0;
	int audioDelayStatisticsCounter = 0;

	int64_t audioShiftTarget = 0;
	int64_t audioShift = 0;

	int64_t fixFpsLastPacketTime = 0;

	int Fps = 0;
	int Width = 0;
	int Height = 0;

	InputRuntime()
	{
		input_time_base = NULL;
		decoder_ctx = NULL;
		input_fmt_ctx = NULL;
		stream_info = NULL;
	}

	~InputRuntime()
	{
		if (decoder_ctx)
		{
			for (int i = 0; i < stream_info->count * DECODERS_COUNT; i++)
			{
				avcodec_free_context(&decoder_ctx[i]);
			}
			delete[] decoder_ctx;

		}
		if (input_time_base)
			delete[] input_time_base;

		if (input_fmt_ctx)
		{
			av_opt_free(input_fmt_ctx->pb);
			avio_context_free(&input_fmt_ctx->pb);
			avformat_close_input(&input_fmt_ctx);
		}

		if (stream_info)
			delete stream_info;

		input_time_base = NULL;
		decoder_ctx = NULL;
		input_fmt_ctx = NULL;
		stream_info = NULL;
	}

};

class EncoderRuntime
{
public:
	AVCodecContext** encoder_ctx;
	StreamInfo stream_info;

	AVFrame* frame;
	AVFrame* filter_frame;

	IDynamicStreamerDecoderCallback* pDirectFrameCallbackInterface;

	FilterRuntime* pFilterRuntime;

	FilterRuntime* pDirectFrameFilter;

	EncoderRuntime(StreamInfo& streamInfo) : stream_info(streamInfo)
	{
		encoder_ctx = NULL;
		frame = av_frame_alloc();
		filter_frame = av_frame_alloc();

		encoder_ctx = new AVCodecContext*[stream_info.count];
		for (int i = 0; i < stream_info.count; i++) encoder_ctx[i] = NULL;

		pFilterRuntime = new FilterRuntime(streamInfo);
		pDirectFrameCallbackInterface = NULL;
		pDirectFrameFilter = NULL;
	}

	void ResetFilter()
	{
		if (pFilterRuntime)
		{
			delete pFilterRuntime;
			pFilterRuntime = NULL;
		}
		pFilterRuntime = new FilterRuntime(stream_info);
	}

	~EncoderRuntime()
	{
		if (encoder_ctx)
		{
			for (int i = 0; i < stream_info.count; i++)
				if (encoder_ctx[i])
					avcodec_close(encoder_ctx[i]);

			delete[] encoder_ctx;
			encoder_ctx = NULL;
		}

		if (pFilterRuntime)
		{
			delete pFilterRuntime;
			pFilterRuntime = NULL;
		}

		DeleteDirectFrameFilter();

		av_frame_free(&frame);
		av_frame_free(&filter_frame);
	}

	void DeleteDirectFrameFilter()
	{
		if (pDirectFrameFilter)
		{
			delete pDirectFrameFilter;
			pDirectFrameFilter = NULL;
		}
	}
};

// CDynamicStreamer

class ATL_NO_VTABLE CDynamicStreamer :
	public CComObjectRootEx<CComSingleThreadModel>,
	public CComCoClass<CDynamicStreamer, &CLSID_DynamicStreamer>,
	public IDispatchImpl<IDynamicStreamer, &IID_IDynamicStreamer, &LIBID_FFDynamicStreamerLib, /*wMajor =*/ 1, /*wMinor =*/ 0>
{
public:

DECLARE_REGISTRY_RESOURCEID(IDR_DYNAMICSTREAMER)


BEGIN_COM_MAP(CDynamicStreamer)
	COM_INTERFACE_ENTRY(IDynamicStreamer)
	COM_INTERFACE_ENTRY(IDispatch)
	COM_INTERFACE_ENTRY_AGGREGATE(IID_IMarshal, m_ftm)
END_COM_MAP()

	CComPtr<IUnknown> m_ftm;


	DECLARE_PROTECT_FINAL_CONSTRUCT()

	HRESULT FinalConstruct()
	{
		av_register_all();
		avdevice_register_all();
		avfilter_register_all();
		avformat_network_init();

		IUnknown* pUnk = GetUnknown();
		return CoCreateFreeThreadedMarshaler(pUnk, &m_ftm);
	}

	void FinalRelease();

public:

	STDMETHOD(SetEncoder)(BSTR videoCodec, BSTR videoOptions, BSTR fallbackvideoCodec, BSTR fallbackVideoOptions, int videoMaxBitrate, BSTR audioCodec, BSTR audioOptions, int audioMaxBitrate);
	STDMETHOD(AddOutput)(BSTR type, BSTR input, BSTR options, IDynamicStreamerDecoderCallback* pCallback, int *pId);
	STDMETHOD(RemoveOutput)(int id);
	STDMETHOD(SetInput)(BSTR type, BSTR input, BSTR options, int fps, int width, int height);
	STDMETHOD(GetStatistics)(SAFEARRAY * *pStats);
	STDMETHOD(SetCallback)(IDynamicStreamerCallback* pCallback);
	STDMETHOD(SetFilter)(BSTR videoFilter);
	STDMETHOD(SetDirectFrameCallback)(IDynamicStreamerDecoderCallback* pCallback);
	STDMETHOD(GetSupportedCodecs)(int* pFlags);

	void OutputThreadRoutine(OutputDescriptor* desc);
	void InputThreadRoutine();

	int is_read_timeout();
private:
	void ResetDrc();
	void NeedsReinit(BOOL& reinitInput, BOOL& reinitEncoder, BOOL& reinitFilter, BOOL& reinitDirectFrame);
	void ReinitInput();
	void ReinitEncoder(int* audioBitrate, int* videoBitrate);
	BOOL NeedsReinitEncoderDrc(int& audioBitrate, int& videoBitrate);

	BOOL InitInputRuntime();
	BOOL IsInputParametersInitialized();
	BOOL InitEncoderRuntime(int audioBitrate, int videoBitrate);

	void UpdateOutputParameters(BOOL bOnlyIfTransmissionNotStarted);
	void UpdateOutputParameters(OutputDescriptor* desc);

	BOOL NeedsReinit(OutputDescriptor* desc);

	void UpdateDrcTarget();

	void SendPacketToOutput(OutputDescriptor* desc, AVPacket& packet);
	void SendPacketToCallback(OutputDescriptor* desc, AVPacket& packet);


	int parse_dictionary(AVDictionary **pm, const char *str);

	CStringA get_encoder_options_audio(CStringA& videoOptions, int videoBitrate);
	CStringA get_encoder_options_video(CStringA& videoOptions, int videoBitrate);
	int open_decoder_context(InputRuntime* runtime, enum AVMediaType type, int streamsCount);
	void init_filter(FilteringContext* fctx, AVCodecContext *dec_ctx, AVCodecContext *enc_ctx, const char *filter_spec);

	int open_decoder_from_encoder(AVCodecContext *enc_ctx, AVCodecContext** dec_ctx);
	void open_encoder_context(CStringA& codec, CStringA& options, AVCodecContext** c, AVCodecContext* input_decoder);
	
	void get_stat(int id, Statistics& statistics, std::vector<CDynamicStreamerStatistics*>& v, LONG64 now);

	HRESULT open_output(OutputDescriptor* desc);
	void set_thread_name(int id);
	HRESULT set_input();

	int transcode_packet(AVPacket* packet);
	int decode_packet_for_callback(AVPacket* packet, OutputDescriptor* desc);
	BOOL init_filter_for_callback(OutputDescriptor* desc, FilteringContext* fctx, AVCodecContext *dec_ctx, const char *filter_spec);
	BOOL init_filter_for_directframe(FilteringContext* fctx, AVCodecContext *dec_ctx, AVCodecContext *enc_ctx);
	void init_filter_options(FilteringContext* fctx, AVCodecContext* dec_ctx);
	
	void set_error(OutputDescriptor* desc, const char* msg, int nerrorCode, const char* cline);
	void clear_error(OutputDescriptor* desc);

	ULONGLONG m_ulStartReadOperationTime;
	BOOL m_bMeasureReadTime;
	void ResetStartOperationTime();


	std::vector<OutputDescriptor*> outputs;
	CRITICAL_SECTION allCS;

	int inputVersion;
	InputParameters* pPendingInputParameters;
	InputParameters* pActiveInputParameters;

	EncoderParameters* pPendingEncoderParameters;
	EncoderParameters* pActiveEncoderParameters;

	FilterParameters* pPendingFilterParameters;
	FilterParameters* pActiveFilterParameters;

	DirectFrameParamiters* pPendingDirectFrameParameters;
	DirectFrameParamiters* pActiveDirectFrameParameters;


	BOOL outputUpdatePending;

	InputRuntime* pInputRuntime;

	int64_t max_analyze_duration = 2000000; // 2 seconds
	EncoderRuntime* pEncoderRuntime;
	int encoderErrors;
	volatile BOOL bReinitEncoderAfterFail;
	OutputDescriptor* pDrcTarget;

	HANDLE thread;
	Statistics statistics;
	int instanceId;
	volatile BOOL continue_input_thread;

	double encoder_drc_ratio;
	int encoder_drc_buffer_size;
	int encoder_drc_buffer_measurements_counter;
	int encoder_drc_positive_counter;


	IDynamicStreamerCallback* m_pCallback;
	
	CONDITION_VARIABLE queueCV;
	std::vector<AVPacket> packetQueue;
	volatile int writerIdx;
	volatile int queueEnd;
	int queueSize;

	void Enque(AVPacket& pkt);
	void Deque(AVPacket& pkt, OutputDescriptor* desc);
	void AddReaderToQueue(OutputDescriptor* desc);

	void InitFilter();
	void ReinitDirectFrame();
	void StopInputThread();
	void StartInputThread();


	ProcessorThread* pTranscodingDecoder;
	ProcessorThread* pTranscodingFilter;
	ProcessorThread* pTranscodingEncoder;
	ProcessorThread* pTranscodingCallback;

	BOOL bPipelineStarted = FALSE;
	int packetSequence = 0;

	ProcessorPool<DecoderTask> decoderPool;
	ProcessorPool<FrameTask> encoderPool;
	ProcessorPool<FrameTask> filterPool;
	ProcessorPool<FrameTask> callbackPool;
	ProcessorPool<FrameTask> callbackTempPool;

	void StopPipeline();
	void StartPipeline();

public:
	void TranscodingDecode(void* item, int threadNo);
	void TranscodingFilter(void* item, int threadNo);
	void TranscodingEncode(void* item, int threadNo);
	void TranscodingCallback(void* item, int threadNo);
	CDynamicStreamer();

	void log(const char* msg, ...);
	void log(BOOL ffmpeg, const char* msg, va_list params);
};

OBJECT_ENTRY_AUTO(__uuidof(DynamicStreamer), CDynamicStreamer)
