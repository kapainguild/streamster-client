// DynamicStreamerStatistics.cpp : Implementation of CDynamicStreamerStatistics

#include "stdafx.h"
#include "DynamicStreamerStatistics.h"


// CDynamicStreamerStatistics



STDMETHODIMP CDynamicStreamerStatistics::get_Interval(LONGLONG* pVal)
{
	// TODO: Add your implementation code here
	*pVal = Item.StartTime;

	return S_OK;
}


STDMETHODIMP CDynamicStreamerStatistics::get_Overall(BOOL* pVal)
{
	*pVal = overall;

	return S_OK;
}


STDMETHODIMP CDynamicStreamerStatistics::get_Id(int* pVal)
{
	*pVal = id;

	return S_OK;
}

STDMETHODIMP CDynamicStreamerStatistics::GetError(BSTR *errorMsg, int *err)
{
	*err = error;
	*errorMsg = CComBSTR(errorMessage).Detach();
	return S_OK;
}

STDMETHODIMP CDynamicStreamerStatistics::GetValues(SAFEARRAY ** pValues)
{
	CComSafeArray<VARIANT> items(statisticTypeCount);
	for (int i = 0; i < items.GetCount(); i++)
	{
		CComVariant var(Item.Values[i]);
		items.SetAt(i, var);
	}

	*pValues = items.Detach();
	return S_OK;
}
