#pragma once

#include <stack>
#include <vector>
#include <algorithm>
#include "Tasks.h"

typedef void(WINAPI *PROCESS_ITEM)(void* lpInstance, void* data, int instanceNo);

template <class T>
class ProcessorPool
{
	CRITICAL_SECTION cs;
	std::stack<T*> items;

public:
	ProcessorPool()
	{
		::InitializeCriticalSection(&cs);
	}

	T* Rent()
	{
		T* result;
		::EnterCriticalSection(&cs);
		if (items.empty())
			result = new T();
		else
		{
			result = items.top();
			items.pop();
		}
		::LeaveCriticalSection(&cs);
		return result;
	}

	void Release(T* item)
	{
		::EnterCriticalSection(&cs);
		items.push(item);
		::LeaveCriticalSection(&cs);
	}

	~ProcessorPool()
	{
		::EnterCriticalSection(&cs);

		while (!items.empty())
		{
			T* result = result = items.top();
			items.pop();
			delete result;
		}

		::LeaveCriticalSection(&cs);
	}
};


class CDynamicStreamer;

class ProcessorThread
{
private:
	BaseTask** buffer;

	BOOL continueThread = TRUE;
	int relativePriority = 0;
	HANDLE* hThreads = 0;

	CONDITION_VARIABLE BufferNotFull;
	CONDITION_VARIABLE BufferNotEmpty;
	CRITICAL_SECTION queueCS;

	PROCESS_ITEM function;
	void* instance;

	int threadsCount;
	int bufferSize;

	volatile int lastSequence;
	volatile int queueStartOffset;
	volatile int queueSize;

	std::vector<BaseTask*> forSorting;
	CDynamicStreamer* pDynamicStreamer = NULL;

public:
	int GetQueueSize();

	ProcessorThread(PROCESS_ITEM afunction, void* ainstance, int athreadsCount, int arelativePriority, CDynamicStreamer* pStramer, int bufferSize);
	~ProcessorThread();

	void Run(int instance);
	void StartThread();

	void Enque(BaseTask* data);

	void EnqueWithSorting(BaseTask* data);

	BOOL StopThread();
};


class ProcessorThreadInstance
{
public:
	ProcessorThread* Processor;
	int Instance;

	ProcessorThreadInstance(ProcessorThread* processor, int instance)
	{
		Processor = processor;
		Instance = instance;
	}
};