using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public class ChartModel
    {
        private readonly Queue<ChartData> _datas = new Queue<ChartData>();
        private readonly AreaSeries _serie;

        public PlotModel PlotModel { get; private set; }

        public ChartModel()
        {
            var textColor = OxyColor.FromRgb(150, 150, 150);
            var gridLineColor = OxyColor.FromRgb(50, 50, 50);

            var plotModel = new PlotModel()
            {
                PlotAreaBorderColor = OxyColors.Transparent,
                PlotMargins = new OxyThickness(20, 0, 5, 20)
            };

            var line = new AreaSeries
            {
                TrackerFormatString = "{4}"
            };

            line.InterpolationAlgorithm = InterpolationAlgorithms.CanonicalSpline;
            line.Fill = OxyColor.FromArgb(20, 31, 251, 68);
            line.Color = OxyColor.FromRgb(31, 251, 68);
            plotModel.Series.Add(line);

            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = -120,
                Maximum = 0,
                TextColor = textColor,
                MajorStep = 60,
                MinorStep = 15,
                TicklineColor = gridLineColor,
                LabelFormatter = FormatTime
            });

            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0,
                Maximum = 100,
                MajorGridlineStyle = LineStyle.Solid,
                MajorStep = 20,
                MinorStep = 20,
                MajorGridlineColor = gridLineColor,
                TicklineColor = gridLineColor,
                TextColor = textColor,
            });

            _serie = line;
            PlotModel = plotModel;
        }

        private string FormatTime(double d)
        {
            if (d == 0.0)
                return "now";
            else if (d == -60.0)
                return "1m ago";
            else if (d == -120.0)
                return "2m ago";
            return null;
        }

        internal void Clear()
        {
            _datas.Clear();
        }

        public void AddValue(double d, double max)
        {
            var now = DateTime.Now;

            _datas.Enqueue(new ChartData
            {
                Stamp = now,
                Value = d
            });

            var limit = now - TimeSpan.FromSeconds(122);
            while (_datas.Count > 0 && _datas.Peek().Stamp < limit)
                _datas.Dequeue();

            _serie.Points.Clear();

            double dataMax = 0;
            foreach (var item in _datas)
            {
                _serie.Points.Add(new DataPoint((item.Stamp - now).TotalSeconds, item.Value));
                dataMax = Math.Max(dataMax, item.Value);
            }
            var globalMax = Math.Max(max, dataMax);

            UpdateRanges(PlotModel.Axes[1], globalMax);

            PlotModel.InvalidatePlot(true);
        }

        private void UpdateRanges(Axis axis, double globalMax)
        {
            double pwr = Math.Log10(globalMax);
            double scl = Math.Pow(10, pwr - Math.Floor(pwr));
            double majorStep;
            if (scl > 0 && scl <= 2.5)
                majorStep = 0.25f;
            else if (scl > 2.5 && scl < 5)
                majorStep = 0.5f;
            else if (scl > 5 && scl < 7.5)
                majorStep = 1f;
            else
                majorStep = 2.5f;

            majorStep = (Math.Pow(10, Math.Floor(pwr)) * majorStep);

            axis.MajorStep = majorStep;
            axis.MinorStep = majorStep;
            axis.Maximum = Math.Ceiling(globalMax / majorStep) * majorStep;
        }

        class ChartData
        {
            public DateTime Stamp { get; set; }

            public double Value { get; set; }
        }
    }

}
