using Clutch.DeltaModel;
using Serilog;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public class MainFiltersModel
    {
        private List<FilterModel> _all;
        private FilterModel _noFilter;
        private readonly CoreData _coreData;

        public List<FilterModel> Left { get; set; }

        public List<FilterModel> Top { get; set; }

        public Property<FilterModel> ActiveSliderFilter { get; } = new Property<FilterModel>();

        public Action ResetSlider { get; set; }

        public Property<bool> FlipH { get; } = new Property<bool>();

        public Property<bool> AnyFilterIsSet { get; } = new Property<bool>();

        public Property<bool> FiltersEnabled { get; } = new Property<bool>();

        public MainFiltersModel(CoreData coreData)
        {
            _coreData = coreData;

            _noFilter = new FilterModel("#nofilter", (l, v) => null);
            Left = new List<FilterModel>
            {
                new FilterModel("Contrast",     (l, v) => GetCustomFilter(l, v, "contrast", 0.5, 1, 1.8), true, "Custom"),
                new FilterModel("Brightness",   (l, v) => GetCustomFilter(l, v, "brightness", -0.25, 0, 0.25), true, "Custom"),
                new FilterModel("Saturation",   (l, v) => GetCustomFilter(l, v, "saturation", 0, 1, 3), true, "Custom"),
                new FilterModel("Gamma",        (l, v) => GetCustomFilter(l, v, "gamma", 0.5, 1, 5), true, "Custom"),
                _noFilter
            };

            Top = new List<FilterModel>
            {
                new FilterModel("Warm", (l, v) => new FilterSpecs("curves=r='0/0 .50/.53 1/1':g='0/0 0.50/0.48 1/1':b='0/0 .50/.46 1/1'", VideoInputCapabilityFormat.Raw)),
                new FilterModel("Cold", (l, v) => new FilterSpecs("curves=r='0/0 .50/.46 1/1':g='0/0 0.50/0.51 1/1':b='0/0 .50/.54 1/1'", VideoInputCapabilityFormat.Raw)),
                new FilterModel("Dark", (l, v) => new FilterSpecs("curves=preset=darker", VideoInputCapabilityFormat.Raw)),
                new FilterModel("Light", (l, v) => new FilterSpecs("curves=preset=lighter", VideoInputCapabilityFormat.Raw)),
                new FilterModel("Vintage", (l, v) => new FilterSpecs("curves=preset=vintage", VideoInputCapabilityFormat.Raw)),
                new FilterModel("Sepia", (l, v) => new FilterSpecs("colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131", VideoInputCapabilityFormat.Raw)),
                new FilterModel("Grayscale", (l, v) => new FilterSpecs("eq=saturation=0", VideoInputCapabilityFormat.MJpeg)),
            };

            _all = Left.Concat(Top).ToList();

            _all.ForEach(f =>
            {
                f.IsOn.OnChange = (o, n) => IsOnChanged(f, n);
                f.Slider.OnChange = () => SliderChanged(f);
            });

            FlipH.OnChange = (o, n) => CommitModel();

            ResetSlider = () => ActiveSliderFilter.Value.Slider.Value = 0.0;

            coreData.Subscriptions.SubscribeForProperties<ISettings>(nameof(ISettings.SelectedVideo), (o, t, p) => OnFiltersChangedFromModel());
            coreData.Subscriptions.SubscribeForProperties<IVideoInput>(nameof(IVideoInput.Filters), (o, t, p) => OnFiltersChangedFromModel());
        }

        public FilterSpecs[] GetFiltersSpec(VideoFilters filters)
        {
            List<FilterSpecs> result = new List<FilterSpecs>();
            if (filters != null)
            {
                if (filters.FlipH)
                    result.Add(new FilterSpecs("hflip"));

                if (filters.Items != null)
                    foreach (var filter in filters.Items)
                    {
                        var filterSpec = _all.FirstOrDefault(s => s.Name == filter.Name)?.Spec(result, filter);
                        if (filterSpec != null)
                            result.Add(filterSpec);
                    }
            }
            return result.OrderBy(s => s, new FilterSpecComparer()).ToArray();
        }

        private FilterSpecs GetCustomFilter(List<FilterSpecs> list, VideoFilter filter, string eqName, double min, double med, double max)
        {
            if (filter.Value == 0.0)
                return null;

            double calc;
            if (filter.Value >= 0)
                calc = ((filter.Value) / 50) * (max - med) + med;
            else
                calc = ((filter.Value) / 50) * (med - min) + med;
            string decl = string.Format(CultureInfo.InvariantCulture, "{0}={1:F2}", eqName, calc);

            var prev = list.FirstOrDefault(s => s.Spec.StartsWith("eq="));
            if (prev == null)
            {
                return new FilterSpecs("eq=" + decl, VideoInputCapabilityFormat.MJpeg);
            }
            else
            {
                prev.Spec = prev.Spec + ":" + decl;
                return null;
            }
        }

        private void OnFiltersChangedFromModel()
        {
            var videoInputId = _coreData.Settings.SelectedVideo;

            if (videoInputId == null || !_coreData.Root.VideoInputs.TryGetValue(videoInputId, out var videoInput))
            {
                FiltersEnabled.Value = false;
                AnyFilterIsSet.Value = false;
            }
            else
            {
                FiltersEnabled.Value = true;

                var model = videoInput.Filters;
                if (!Equals(model, GetFiltersModel()))
                {
                    if (model == null)
                        model = new VideoFilters();

                    FlipH.SilentValue = model.FlipH;
                    _all.ForEach(s =>
                    {
                        s.IsOn.SilentValue = false;
                        s.Slider.SilentValue = 0;
                    });
                    if (model.Items == null)
                        _noFilter.IsOn.SilentValue = true;
                    else
                    {
                        model.Items.ToList().ForEach(s =>
                        {
                            var target = _all.FirstOrDefault(f => s.Name == f.Name);
                            if (target != null)
                            {
                                target.IsOn.SilentValue = true;
                                target.Slider.SilentValue = s.Value;
                            }
                            else
                                Log.Warning($"Filter {s.Name} not found");
                        });
                    }
                }
                AnyFilterIsSet.Value = !_noFilter.IsOn.Value;
            }
        }

        private VideoFilters GetFiltersModel()
        {
            if (!FlipH.Value && _noFilter.IsOn.Value)
                return null;

            var result = new VideoFilters();
            result.FlipH = FlipH.Value;
            if (!_noFilter.IsOn.Value)
            {
                result.Items = _all.Where(s => s.IsOn.Value).Select(s => new VideoFilter
                {
                    Name = s.Name,
                    Value = s.Slider.Value
                }).ToArray();
            }
            return result;
        }

        void IsOnChanged(FilterModel filter, bool newValue)
        {
            bool isGrouped = filter.Group != null;

            var sameGroup = _all.Where(s => s.Group == filter.Group && isGrouped).ToList();

            _all.Where(s => s.HasSlider && s != filter).ToList().ForEach(s => s.IsActiveSlider.Value = false);

            _all.Where(f => f != filter && !sameGroup.Contains(f)).ToList().ForEach(f => f.IsOn.SilentValue = false);

            if (filter.HasSlider)
            {
                filter.IsActiveSlider.Value = true;
                ActiveSliderFilter.Value = filter;
                sameGroup.ForEach(f => f.IsOn.SilentValue = !f.Slider.IsDefault);
            }
            else
            {
                ActiveSliderFilter.Value = null;
            }

            if (_all.All(s => !s.IsOn.Value))
                _noFilter.IsOn.SilentValue = true;

            CommitModel();
        }

        private void CommitModel()
        {
            var selectedVideo = _coreData.Root.Settings.SelectedVideo;
            if (selectedVideo == null)
            {
                Log.Warning("Commit filters called when SelectedVideo = null");
            }
            else if (!_coreData.Root.VideoInputs.TryGetValue(selectedVideo, out var videoInput))
            {
                Log.Warning($"Commit filters called when {selectedVideo} does not exists");
            }
            else
                videoInput.Filters = GetFiltersModel();
        }

        private void SliderChanged(FilterModel f)
        {
            f.IsOn.Value = !f.Slider.IsDefault;
        }
    }

    internal class FilterSpecComparer : IComparer<FilterSpecs>
    {
        public int Compare(FilterSpecs x, FilterSpecs y)
        {
            int xx = GetPoints(x);
            int yy = GetPoints(y);
            return yy - xx;
        }

        private int GetPoints(FilterSpecs x)
        {
            if (x.InputFormats.Length == 1)
            {
                if (x.InputFormats[0] == VideoInputCapabilityFormat.MJpeg)
                    return 1;
                else
                    return 3;
            }
            else return 2;
        }
    }

    public class FilterModel
    {
        public FilterModel(string name, Func<List<FilterSpecs>, VideoFilter, FilterSpecs> spec, bool hasSlider = false, string group = null)
        {
            HasSlider = hasSlider;
            Name = name;
            Group = group;
            Spec = spec;

        }

        public Func<List<FilterSpecs>, VideoFilter, FilterSpecs> Spec { get; set; }

        public Property<bool> IsActiveSlider { get; set; } = new Property<bool>(false);

        public Property<bool> IsOn { get; set; } = new Property<bool>(false);

        public SliderProperty Slider { get; } = new SliderProperty();

        public string Name { get; set; }

        public string Group { get; set; }

        public bool HasSlider { get; set; }
    }

    public class SliderProperty : INotifyPropertyChanged
    {
        private double _value;

        public Action OnChange { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public double Start => Math.Min(0.0, _value);

        public double End => Math.Max(0.0, _value);

        public bool IsDefault => _value == 0.0;

        public double Value
        {
            get => _value;
            set
            {
                SilentValue = value;
                OnChange?.Invoke();
            }
        }

        public double SilentValue
        {
            get => _value;
            set
            {
                _value = AdjustValue(value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Value)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Start)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.End)));
            }
        }

        private double AdjustValue(double val) => (Math.Round(val / 2.5)) * 2.5;

        public override string ToString() => $"Slider:{this._value}";
    }

    public class FilterSpecs
    {
        public FilterSpecs(string spec)
        {
            Spec = spec;
            InputFormats = new [] { VideoInputCapabilityFormat.Raw, VideoInputCapabilityFormat.MJpeg };
        }

        public FilterSpecs(string spec, VideoInputCapabilityFormat format)
        {
            Spec = spec;
            InputFormats = new[] { format };
        }

        public string Spec { get; set; }

        public VideoInputCapabilityFormat[] InputFormats { get; set; }
    }
}
