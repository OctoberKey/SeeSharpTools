﻿using System;
using System.Collections;
using System.Collections.Generic;
using SeeSharpTools.JY.GUI.StripChartXUtility;

namespace SeeSharpTools.JY.GUI.StripChartXData
{
    internal abstract class DataEntityBase
    {
        public DataEntityInfo DataInfo { get; }

        // 当前XPlotBuffer中数据的起始和结束值
        private int _lastXStart;
        private int _lastXEnd;
        private int _lastXSparseRatio;

        // 当前YPlotBuffer中各个线条的起始和结束值
        protected int[] LastYStartIndex;
        protected int[] LastYEndIndex;

        // 每个线条当前的稀疏比
        protected int[] SparseRatio;

        protected readonly PlotManager ParentManager;

        public StripChartX.FitType FitType { get; set; }

        public abstract int PlotCount { get; set; }

        protected ParallelHandler ParallelHandler;

        public abstract int SamplesInChart { get; }
        
        protected DataEntityBase(PlotManager plotManager, DataEntityInfo dataInfo)
        {
            this.ParentManager = plotManager;
            this.FitType = plotManager.FitType;
            this.DataInfo = dataInfo;
            this.ParallelHandler = new ParallelHandler(ParentManager.DataCheckParams);

            _lastXStart = int.MinValue;
            _lastXEnd = int.MinValue;
            _lastXSparseRatio = int.MinValue;
            LastYStartIndex = new int[DataInfo.LineCount];
            LastYEndIndex = new int[DataInfo.LineCount];
            SparseRatio = new int[DataInfo.LineCount];
            for (int i = 0; i < DataInfo.LineCount; i++)
            {
                LastYStartIndex[i] = int.MinValue;
                LastYEndIndex[i] = int.MinValue;
                SparseRatio[i] = int.MaxValue;
            }
        }

        public virtual void Initialize(int sampleCount)
        {
        }

        public abstract void AddPlotData(IList<string> xData, Array lineData);

        public abstract void AddPlotData(DateTime[] startTime, Array lineData);

        public abstract void AddPlotData(Array lineData, int sampleCount);

        public abstract List<int> GetXPlotBuffer();

        public abstract string GetXValue(int xIndex);
        public abstract object GetYValue(int xIndex, int seriesIndex);

        public abstract IList<TDataType> GetPlotDatas<TDataType>(int startIndex, int endIndex);

        public abstract void GetMaxAndMinYValue(int seriesIndex, out double maxYValue, out double minYValue);

        public abstract void GetMaxAndMinYValue(out double maxYValue, out double minYValue);

//        protected void RefreshSamplesInChart(int plotSamples)
//        {
//            if (SamplesInChart >= ParentManager.DisplayPoints)
//            {
//                return;
//            }
//            SamplesInChart += plotSamples;
//            if (SamplesInChart > ParentManager.DisplayPoints)
//            {
//                SamplesInChart = ParentManager.DisplayPoints;
//            }
//        }

        public virtual void Clear()
        {
            _lastXStart = int.MinValue;
            _lastXEnd = int.MinValue;
            _lastXSparseRatio = int.MinValue;
            LastYStartIndex = new int[DataInfo.LineCount];
            LastYEndIndex = new int[DataInfo.LineCount];
            SparseRatio = new int[DataInfo.LineCount];
            for (int i = 0; i < DataInfo.LineCount; i++)
            {
                LastYStartIndex[i] = int.MinValue;
                LastYEndIndex[i] = int.MinValue;
                SparseRatio[i] = int.MaxValue;
            }
        }

        public abstract IList GetXData();

        public abstract IList GetYData();

        public abstract bool FillYPlotDatas(int beginXIndex, int endXIndex, bool forceRefresh, int seriesIndex, int newSparseRatio, int plotCount);

        public bool FillPlotDataInRange(int beginXIndex, int endXIndex, bool forceRefresh, int seriesIndex)
        {
            bool plotParamChanged = false;
            int plotCount = 0;
            int newSparseRatio = GetSparseRatio(beginXIndex, endXIndex, out plotCount);
            if (-1 == seriesIndex)
            {
                // 如果更新所有的线条，则依次更新所有线条的PlotBuffer
                FillXPlotDatas(beginXIndex, endXIndex, newSparseRatio, plotCount);
                for (int i = 0; i < DataInfo.LineCount; i++)
                {
                    plotParamChanged |= FillYPlotDatas(beginXIndex, endXIndex, forceRefresh, i, newSparseRatio, plotCount);
                }
            }
            else
            {
                FillXPlotDatas(beginXIndex, endXIndex, newSparseRatio, plotCount);
                plotParamChanged = FillYPlotDatas(beginXIndex, endXIndex, forceRefresh, seriesIndex, newSparseRatio, plotCount);
            }
            this.PlotCount = plotCount;
            return plotParamChanged;
        }

        // 为了保证效率，X轴的buffer是倒着放的，最后一个元素放在容器最后一个位置，然后依次向前延伸
        private void FillXPlotDatas(int beginXIndex, int endXIndex, int newSparseRatio, int plotCount)
        {
            // 如果当前起始位置等于上次起始位置、当前结束位置小于等于上次结束位置、新的SparseRatio等于上次的SpaseRatio时无需更新数据。
            if (_lastXStart == beginXIndex && _lastXEnd >= endXIndex && _lastXSparseRatio == newSparseRatio)
            {
                return;
            }
            List<int> xPlotBuffer = GetXPlotBuffer();
            int bufSize = xPlotBuffer.Count;
            int xValue = 0;
            // 如果X轴起点相同，稀疏比相同，则直接在前面添加
            if (_lastXStart == beginXIndex && _lastXSparseRatio == newSparseRatio)
            {
                xValue = beginXIndex - SamplesInChart - newSparseRatio;
                int startAddIndex = bufSize - plotCount;
                int endAddIndex = bufSize - plotCount + (endXIndex - _lastXEnd)/newSparseRatio;
                for (int i = startAddIndex; i < endAddIndex; i++)
                {
                    xValue += newSparseRatio;
                    xPlotBuffer[i] = xValue;
                }
            }
            else
            {
                // X值终点为-1
                xValue = beginXIndex - SamplesInChart - newSparseRatio;
                for (int i = bufSize - plotCount; i < bufSize; i++)
                {
                    xValue += newSparseRatio;
                    xPlotBuffer[i] = xValue;
                }
            }
            _lastXSparseRatio = newSparseRatio;
            _lastXStart = beginXIndex;
            _lastXEnd = endXIndex;
        }

        private static int GetSparseRatio(int start, int end, out int plotCount)
        {
            int count = end - start + 1;
            int sparseRatio = 1;
            while (count > (Constants.MaxPointsInSingleSeries * sparseRatio))
            {
                sparseRatio *= 2;
            }

            plotCount = (count + sparseRatio - 1) / sparseRatio;
            return sparseRatio;
        }
    }
}