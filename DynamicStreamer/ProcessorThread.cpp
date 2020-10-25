#include "stdafx.h"
#include "ProcessorThread.h"
#include "DynamicStreamer.h"



DWORD WINAPI Thread(LPVOID lpParam) 
{ 
	ProcessorThreadInstance* me = (ProcessorThreadInstance*)lpParam;
	me->Processor->Run(me->Instance); 
	delete me;
	return 0; 
}


ProcessorThread::ProcessorThread(PROCESS_ITEM afunction, void* ainstance, int athreadsCount, int arelativePriority, CDynamicStreamer* pStreamer, int size)
{
	::InitializeConditionVariable(&BufferNotFull);
	::InitializeConditionVariable(&BufferNotEmpty);
	::InitializeCriticalSection(&queueCS);
	queueSize = 0;
	queueStartOffset = 0;
	function = afunction;
	instance = ainstance;
	threadsCount = athreadsCount;
	relativePriority = arelativePriority;
	pDynamicStreamer = pStreamer;
	buffer = new BaseTask* [size];
	bufferSize = size;
}

int ProcessorThread::GetQueueSize()
{
	return queueSize + forSorting.size();
}

ProcessorThread::~ProcessorThread()
{
	delete[] buffer;
	::DeleteCriticalSection(&queueCS);
}


void ProcessorThread::StartThread()
{
	lastSequence = 0;
	continueThread = TRUE;
	if (hThreads == NULL)
	{
		hThreads = new HANDLE[threadsCount];
		for (int q = 0; q < threadsCount; q++)
		{
			hThreads[q] = ::CreateThread(NULL, 0, Thread, new ProcessorThreadInstance(this, q), 0, NULL);
			if (relativePriority)
			{
				int val = ::GetThreadPriority(hThreads[q]);
				::SetThreadPriority(hThreads[q], val + relativePriority);
			}
		}
	}
}

bool Comparer(BaseTask* i, BaseTask* j) { return (i->PacketNumber > j->PacketNumber); }

void ProcessorThread::Enque(BaseTask* data)
{
	EnterCriticalSection(&queueCS);

	while (queueSize == bufferSize)
	{
		// Buffer is full - sleep so consumers can get items.
		SleepConditionVariableCS(&BufferNotFull, &queueCS, INFINITE);
	}
	// Insert the item at the end of the queue and increment size.
	
	buffer[(queueStartOffset + queueSize) % bufferSize] = data;
	queueSize++;
	lastSequence++;

	LeaveCriticalSection(&queueCS);

	// If a consumer is waiting, wake it.
    WakeConditionVariable(&BufferNotEmpty);
}

void ProcessorThread::EnqueWithSorting(BaseTask* data)
{
	EnterCriticalSection(&queueCS);

	while (true)
	{
		if (data->PacketNumber == lastSequence && (queueSize + forSorting.size()) < bufferSize)
			break;

		if (data->PacketNumber != lastSequence && (queueSize + forSorting.size()) < (bufferSize / 2))
			break;

		//pDynamicStreamer->log("Block %d %d %d %d", data->PacketNumber, data->PacketNumber != lastSequence, queueSize, forSorting.size());
		// Buffer is full - sleep so consumers can get items.
		SleepConditionVariableCS(&BufferNotFull, &queueCS, INFINITE);
	}
	// Insert the item at the end of the queue and increment size.

	bool wake = false;
	if (data->PacketNumber == lastSequence)
	{
		wake = true;
		buffer[(queueStartOffset + queueSize) % bufferSize] = data;
		queueSize++;
		lastSequence++;

		if (forSorting.size() > 0)
		{
			std::sort(forSorting.begin(), forSorting.end(), Comparer);

			while (forSorting.size() > 0)
			{
				BaseTask* min = forSorting.back();
				if (min->PacketNumber == lastSequence)
				{
					buffer[(queueStartOffset + queueSize) % bufferSize] = min;
					queueSize++;
					lastSequence++;
					forSorting.pop_back();
				}
				else
					break;
			}
		}
	}
	else
	{
		//pDynamicStreamer->log("push_back %d", data->PacketNumber);
		forSorting.push_back(data);
	}

	LeaveCriticalSection(&queueCS);

	// If a consumer is waiting, wake it.
	if (wake)
		WakeConditionVariable(&BufferNotEmpty);
}

BOOL ProcessorThread::StopThread()
{
	BOOL bResult = TRUE;
	EnterCriticalSection(&queueCS);
	continueThread = FALSE;
	LeaveCriticalSection(&queueCS);

	WakeAllConditionVariable(&BufferNotFull);
	WakeAllConditionVariable(&BufferNotEmpty);

	if (hThreads != NULL)
	{
		if (::WaitForMultipleObjects(threadsCount, hThreads, TRUE, 5000) == WAIT_TIMEOUT)
		{
			for (int q = 0; q < threadsCount; q++)
				::TerminateThread(hThreads[q], 0);
			bResult = FALSE;
		}
		for (int q = 0; q < threadsCount; q++)
			::CloseHandle(hThreads[q]);
		delete[] hThreads;
		hThreads = NULL;
	}
	return bResult;
}

void ProcessorThread::Run(int instanceNo)
{
	while (true)
	{
		EnterCriticalSection(&queueCS);

		while (queueSize == 0 && continueThread)
		{
			// Buffer is empty - sleep so producers can create items.
			SleepConditionVariableCS(&BufferNotEmpty, &queueCS, INFINITE);
		}

		if (!continueThread && queueSize == 0)
		{
			LeaveCriticalSection(&queueCS);
			break;
		}

		// Consume the first available item.
		BaseTask* item = buffer[queueStartOffset];

		queueSize--;
		queueStartOffset++;

		if (queueStartOffset == bufferSize)
			queueStartOffset = 0;

		LeaveCriticalSection(&queueCS);

		// If a producer is waiting, wake it.
		WakeAllConditionVariable(&BufferNotFull);

		function(instance, item, instanceNo);
	}
}