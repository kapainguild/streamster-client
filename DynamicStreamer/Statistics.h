#pragma once

#include "FFDynamicStreamer_i.h"

class StatisticItem
{
public:
	LONG64 Values[statisticTypeCount];
	LONG64 StartTime;

	StatisticItem() 
	{
		memset(this, 0, sizeof(StatisticItem));
	}
};

class Statistics
{
public:
	StatisticItem overall;
	StatisticItem current;

	volatile unsigned int lastError;
	CStringA lastErrorMessage;

	Statistics() :lastError(0){}
};