// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using System;
using System.Reflection;

namespace nsCDEngine.ViewModels
{
    /// <summary>
    /// Interface to create a charts factory for NMI Chart controls
    /// </summary>
    public interface ICDEChartsFactory
    {
        /// <summary>
        /// Resets the Chart and clears out all series
        /// </summary>
        void Reset();

        /// <summary>
        /// Creates a new Chart Info first resetting all
        /// </summary>
        /// <param name="pDef"></param>
        /// <param name="pZoomType"></param>
        /// <param name="xAxisType"></param>
        void CreateChartInfo(TheChartDefinition pDef, string pZoomType, string xAxisType);
        /// <summary>
        /// Sets generic information and settings on a chart
        /// </summary>
        /// <param name="pDef">Chart Definition as the CDE knows it</param>
        /// <param name="pZoomType">Zoom Type of the chart</param>
        /// <param name="xAxisType">Axis Type of the chart</param>
        void SetInfo(TheChartDefinition pDef, string pZoomType, string xAxisType);
        /// <summary>
        /// Adds a new series to the chart
        /// </summary>
        /// <param name="pName"></param>
        void AddSeries(string pName);

        /// <summary>
        /// Enables the Chart Factory to add chart data.
        /// </summary>
        /// <param name="pData">The chart data data.</param>
        /// <returns><c>true</c> if the factory used this method, it should return true and AddPointToSeries will not be called, <c>false</c> otherwise.</returns>
        bool AddChartData(TheChartData pData);
        /// <summary>
        /// Adds a new poit to a series of a chart
        /// </summary>
        /// <param name="pName">Name of the series</param>
        /// <param name="pX">XLabel of the point</param>
        /// <param name="point">Value of the point</param>
        /// <returns></returns>
        bool AddPointToSeries(string pName, string pX, double point);

        /// <summary>
        /// Send Chart Data to the plugin
        /// </summary>
        /// <param name="pTarget">Target Definition for the chart (using cdeN and cdeMID)</param>
        void SendChartData(TheDataBase pTarget);

        /// <summary>
        /// Allows to overide the default built in Charts Update push by a Charts Plugin
        /// </summary>
        /// <param name="pChartGuid">Chart Definition GUID</param>
        /// <param name="pOriginator">Requesting Node</param>
        /// <param name="IChartFactory">AssemblyQualifiedName of the ChartFactory Type that should handle the Chart Data conversion</param>
        /// <returns></returns>
        bool PushChartData(Guid pChartGuid, Guid pOriginator, string IChartFactory = null);
    }

    /// <summary>
    /// Chart Factory for sending Chart Data to the User Interface
    /// </summary>
    public class TheChartFactory : ICDEChartsFactory    //New in 4.302: is this ok?
    {
        /// <summary>
        /// A Charts factory that allows to send data to chart controls
        /// </summary>
        public static ICDEChartsFactory MyDefaultChartsFactory =null;

        /// <summary>
        /// Resets the Chart factory
        /// </summary>
        public void Reset()
        {
            MyDefaultChartsFactory?.Reset();
        }

        /// <summary>
        /// Creates a new ChartInfo
        /// </summary>
        /// <param name="pDef"></param>
        /// <param name="pZoomType"></param>
        /// <param name="xAxisType"></param>
        public void CreateChartInfo(TheChartDefinition pDef, string pZoomType, string xAxisType)
        {
            MyDefaultChartsFactory?.Reset();
            MyDefaultChartsFactory?.SetInfo(pDef, pZoomType, xAxisType);
        }

        /// <summary>
        /// Sets a chart infor without a reset
        /// </summary>
        /// <param name="pDef">Chart Definition</param>
        /// <param name="pZoomType">Zoom type</param>
        /// <param name="xAxisType">Axis Type</param>
        public void SetInfo(TheChartDefinition pDef, string pZoomType, string xAxisType)
        {
            MyDefaultChartsFactory?.SetInfo(pDef, pZoomType, xAxisType);
        }

        /// <summary>
        /// Adds a new series to the Chart Info
        /// </summary>
        /// <param name="pName"></param>
        public void AddSeries(string pName)
        {
            MyDefaultChartsFactory?.AddSeries(pName);
        }

        /// <summary>
        /// Enables the Chart Factory to add chart data.
        /// </summary>
        /// <param name="pData">The chart data data.</param>
        /// <returns><c>true</c> if the factory used this method, it should return true and AddPointToSeries will not be called, <c>false</c> otherwise.</returns>
        public bool AddChartData(TheChartData pData)
        {
            return MyDefaultChartsFactory?.AddChartData(pData) == true;
        }

        /// <summary>
        /// Adds new points to the chart info
        /// </summary>
        /// <param name="pName">Name of the series</param>
        /// <param name="pX">X Value</param>
        /// <param name="point">Value of the point</param>
        /// <returns></returns>
        public bool AddPointToSeries(string pName, string pX, double point)
        {
            return MyDefaultChartsFactory?.AddPointToSeries(pName, pX, point)==true;
        }

        /// <summary>
        /// Sends the Chart Data to a target node
        /// </summary>
        /// <param name="pTarget"></param>
        public void SendChartData(TheDataBase pTarget)
        {
            MyDefaultChartsFactory?.SendChartData(pTarget);
        }

        /// <summary>
        /// Pushes chart Data to a node
        /// </summary>
        /// <param name="pChartGuid"></param>
        /// <param name="pOriginator"></param>
        /// <param name="IChartFactory"></param>
        /// <returns></returns>
        public bool PushChartData(Guid pChartGuid, Guid pOriginator, string IChartFactory = null)
        {
            return MyDefaultChartsFactory?.PushChartData(pChartGuid, pOriginator, IChartFactory) == true;
        }

        /// <summary>
        /// Send the requested Charts data information back to the Originator
        /// </summary>
        /// <param name="pChartGuid">Guid of the defined Charts Data</param>
        /// <param name="pOriginator">Originator to send the data to</param>
        /// <param name="IChartFactory">AssemblyQualifiedName of the ChartFactory Type that should handle the Chart Data conversion</param>
        public static bool PushChartsData(Guid pChartGuid, Guid pOriginator, string IChartFactory = null)
        {
            TheStorageUtilities.PushChartsData(pChartGuid, pOriginator, IChartFactory);
            return true;
        }
    }

    /// <summary>
    /// Definition of a chart point
    /// </summary>
    public class TheChartPoint
    {
        /// <summary>
        /// Name of the Point
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// Value of the point
        /// </summary>
        public double value { get; set; }
    }

    /// <summary>
    /// Chart Data to be sent to the NMI
    /// </summary>
    public class TheChartData
    {
        /// <summary>
        /// Amount of series
        /// </summary>
        public int Series { get; set; }
        /// <summary>
        /// Array of values
        /// </summary>
        public double[] MyValue { get; set; }
        /// <summary>
        /// Aray of value counts
        /// </summary>
        public int[] ValCnt { get; set; }
        /// <summary>
        /// Array of bools . a true value means that the corresponding value in the MyValue array is valid
        /// </summary>
        public bool[] IsValidValue { get; set; }
        /// <summary>
        /// Timestamp of the Chart Data
        /// </summary>
        public DateTimeOffset TimeStamp { get; set; }
        /// <summary>
        /// If the chart data is part of a bigger storage table, this value contains the virtual block number in the storage
        /// </summary>
        public int BlockNo { get; set; }

        /// <summary>
        /// Each Value can have a different name
        /// </summary>
        public string [] Name { get; set; }

        /// <summary>
        /// Public constructor for deserialization
        /// </summary>
        public TheChartData() { }

        /// <summary>
        /// Constructor setting all arrays
        /// </summary>
        /// <param name="series"></param>
        public TheChartData(int series)
        {
            Series = series;
            MyValue = new double[series];
            ValCnt = new int[series];
            IsValidValue = new bool[series];
            Name = new string[series];
        }
    }
}
