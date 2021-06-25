using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class SceneEditingEffects
    {
        private readonly SceneEditingModel _model;
        private readonly SceneItemModel _item;

        private FilterTypeDescriptor[] _typeDescriptors = new[]
        {
            new FilterTypeDescriptor("Warm", SceneItemFilterType.Warm             , FilterCategory.Quick, SliderType.No),
            new FilterTypeDescriptor("Cold", SceneItemFilterType.Cold             , FilterCategory.Quick, SliderType.No),
            new FilterTypeDescriptor("Dark", SceneItemFilterType.Dark             , FilterCategory.Quick, SliderType.No),
            new FilterTypeDescriptor("Light", SceneItemFilterType.Light           , FilterCategory.Quick, SliderType.No),
            new FilterTypeDescriptor("Grayscale", SceneItemFilterType.Grayscale   , FilterCategory.Quick, SliderType.ZeroPlus, 1.0),


            new FilterTypeDescriptor("Azure", SceneItemFilterType.Azure             , FilterCategory.Creative, SliderType.ZeroPlus, 0.0),
            new FilterTypeDescriptor("B&W", SceneItemFilterType.B_W                 , FilterCategory.Creative, SliderType.ZeroPlus, 1.0),
            new FilterTypeDescriptor("Chill", SceneItemFilterType.Chill             , FilterCategory.Creative, SliderType.ZeroPlus, 1.0),
            
            new FilterTypeDescriptor("Pastel", SceneItemFilterType.Pastel           , FilterCategory.Creative, SliderType.ZeroPlus, 1.0),
            new FilterTypeDescriptor("Romantic", SceneItemFilterType.Romantic       , FilterCategory.Creative, SliderType.ZeroPlus, 1.0),
            new FilterTypeDescriptor("Sapphire", SceneItemFilterType.Sapphire       , FilterCategory.Creative, SliderType.ZeroPlus, 1.0),
            new FilterTypeDescriptor("Sepia", SceneItemFilterType.Sepia             , FilterCategory.Creative, SliderType.ZeroPlus, 1.0),
            new FilterTypeDescriptor("Vintage", SceneItemFilterType.Vintage         , FilterCategory.Creative, SliderType.ZeroPlus, 1.0),
            new FilterTypeDescriptor("Wine", SceneItemFilterType.Wine               , FilterCategory.Creative, SliderType.ZeroPlus, 0.0),

            new FilterTypeDescriptor("Brightness", SceneItemFilterType.Brightness   , FilterCategory.Basic, SliderType.MinusPlus),
            new FilterTypeDescriptor("Contrast", SceneItemFilterType.Contrast       , FilterCategory.Basic, SliderType.MinusPlus),
            new FilterTypeDescriptor("Saturation", SceneItemFilterType.Saturation   , FilterCategory.Basic, SliderType.MinusPlus),
            new FilterTypeDescriptor("Sharpness", SceneItemFilterType.Sharpness     , FilterCategory.Basic, SliderType.ZeroPlus),
            new FilterTypeDescriptor("Opacity", SceneItemFilterType.Opacity         , FilterCategory.Basic, SliderType.ZeroPlus),
        };

        public Property<bool> HFlip { get; } = new Property<bool>();

        public Action ToggleHFlip { get; }

        public List<FilterSourceModel> BasicSources { get; }

        public List<FilterSourceModel> QuickSources { get; }

        public List<FilterSourceModel> CreativeSources { get; }

        public List<FilterSourceModel> AllSources { get; }

        public ObservableCollection<FilterActiveModel> Filters { get; } = new ObservableCollection<FilterActiveModel>();

        public Property<bool> AnyActive { get; } = new Property<bool>();

        public Action RemoveAll { get; }

        public Action<string, byte[]> AddLut { get; }

        public ObservableCollection<LutModel> RecentLuts { get; } = new ObservableCollection<LutModel>();

        public Property<string> Message { get; } = new Property<string>();

        public SceneEditingEffects(SceneEditingModel model, SceneItemModel item)
        {
            _model = model;
            _item = item;

            ToggleHFlip = () =>
            {
                var present = GetHflip();
                if (present != null)
                    RemoveFilter(present);
                else
                    AddFilter(new SceneItemFilter { Type = SceneItemFilterType.HFlip, Enabled = true });
            };

            RemoveAll = () =>
            {
                var present = GetHflip();
                if (present != null)
                    _item.Model.Filters = new SceneItemFilters { Filters = new[] { present } };
                else
                    _item.Model.Filters = null;
            };

            BasicSources = _typeDescriptors.Where(s => s.Category == FilterCategory.Basic).Select(s => new FilterSourceModel { Desc = s }).ToList();
            QuickSources = _typeDescriptors.Where(s => s.Category == FilterCategory.Quick).Select(s => new FilterSourceModel { Desc = s }).ToList();
            CreativeSources = _typeDescriptors.Where(s => s.Category == FilterCategory.Creative).Select(s => new FilterSourceModel { Desc = s }).ToList();


            AllSources = BasicSources.Concat(QuickSources).Concat(CreativeSources).ToList();

            AllSources.ForEach(s => s.InUse.OnChange = (o, n) => SourceChanged(s));

            AddLut = (f, d) => _ = AddResource(f, d);

            UpdateContent(EditingUpdateType.Content);
        }

        private async Task AddResource(string fileName, byte[] data)
        {
            if (data == null)
            {
                Message.Value = "Unsupported format";
                await Task.Delay(3500);
                Message.Value = null;
            }
            else
            {
                var id = _model.ResourceService.AddResource(fileName, data, ResourceType.LutPng);
                AddFilter(new SceneItemFilter { Type = SceneItemFilterType.UserLut, Enabled = true, LutResourceId = id, Value = 1.0});
            }
        }

        private void SourceChanged(FilterSourceModel model)
        {
            var filters = GetFiltersModel();
            var existent = filters.FirstOrDefault(s => s.Type == model.Desc.Type);
            if (existent != null)
                RemoveFilter(existent);
            else
                AddFilter(new SceneItemFilter { Type = model.Desc.Type, Enabled = true, Value = model.Desc.DefaultValue });
        }

        private SceneItemFilter GetHflip() => _item.Model.Filters?.Filters?.FirstOrDefault(s => s.Type == SceneItemFilterType.HFlip);

        private void RemoveFilter(SceneItemFilter filter)
        {
            if (_item.Model.Filters?.Filters != null && filter != null)
            {
                var res = _item.Model.Filters.Filters.Where(s => s != filter).ToArray();
                if (res.Length == 0)
                    _item.Model.Filters = null;
                else
                    _item.Model.Filters = new SceneItemFilters { Filters = res };
            }
        }

        private void AddFilter(SceneItemFilter filter)
        {
            if (_item.Model.Filters?.Filters != null)
                _item.Model.Filters = new SceneItemFilters { Filters = _item.Model.Filters.Filters.Concat(new[] { filter }).ToArray() };
            else
                _item.Model.Filters = new SceneItemFilters { Filters = new[] { filter } };
        }

        private SceneItemFilter[] GetFiltersModel() => _item.Model.Filters?.Filters ?? new SceneItemFilter[0];

        public void UpdateContent(EditingUpdateType updateType)
        {
            HFlip.SilentValue = GetHflip() != null;

            var filters = GetFiltersModel();

            AllSources.ForEach(s => s.InUse.SilentValue = filters.Any(r => r.Type == s.Desc.Type));

            ListHelper.UpdateCollectionNoId(filters.Where(s => s.Type != SceneItemFilterType.HFlip).ToList(),
                        Filters,
                        (s, t) => t.IsTheSame(s),
                        s => CreateActive(s));

            foreach(var s in  Filters)
            {
                ApplyFilter(s, filters);
            }

            AnyActive.Value = Filters.Any(s => s.IsEnabled.Value);

            UpdateLuts();
        }

        private void UpdateLuts()
        {
            var core = _model.CoreData;

            var active = GetFiltersModel().Where(s => s.Type == SceneItemFilterType.UserLut).Select(s => 
            {
                core.Root.Resources.TryGetValue(s.LutResourceId, out var res);
                return (id: s.LutResourceId, res: res, lastUse: res?.LastUse ?? DateTime.MaxValue);
             }).ToArray();

            var toTake = Math.Max(5 - active.Length, 0);

            
            var recent = core.Root.Resources.Select(s => new { Key = s.Key, Value = s.Value }).OrderByDescending(s => s.Value.LastUse)
                .Where(s => (s.Value.Info.Type == ResourceType.LutCube || s.Value.Info.Type == ResourceType.LutPng) && _model.ResourceService.GetResource(s.Key) != null && !active.Any(r => r.id == s.Key))
                .Take(toTake)
                .Select(s => (id: s.Key, res: s.Value, lastUse: s.Value.LastUse))
                .ToArray();

            var final = active.Concat(recent).OrderByDescending(s => s.lastUse).ToList();

            ListHelper.UpdateCollectionNoId(final, RecentLuts, (s, t) => s.id == t.ResourceId, s => new LutModel { Name = s.res?.Info?.Name ?? "not found", ResourceId = s.id });

            var filters = GetFiltersModel();
            foreach (var lut in RecentLuts)
            {
                lut.InUse.SilentValue = filters.Any(s => s.LutResourceId == lut.ResourceId);
                lut.InUse.OnChange = (o, n) => AddRemoveLut(lut, n);
            }
        }

        private void AddRemoveLut(LutModel lut, bool add)
        {
            if (add)
            {
                if (_model.CoreData.Root.Resources.TryGetValue(lut.ResourceId, out var res))
                    res.LastUse = DateTime.UtcNow;
                AddFilter(new SceneItemFilter { Type = SceneItemFilterType.UserLut, Value = 1.0, Enabled = true, LutResourceId = lut.ResourceId });
            }
            else
                RemoveFilter(GetFiltersModel().FirstOrDefault(s => s.LutResourceId == lut.ResourceId));
        }

        private FilterActiveModel CreateActive(SceneItemFilter source)
        {
            if (source.Type == SceneItemFilterType.UserLut)
            {
                _model.CoreData.Root.Resources.TryGetValue(source.LutResourceId, out var lut);
                string name = lut?.Info.Name ?? "not found";
                return new FilterActiveModel { Source = source, Name = name, SliderType = SliderType.ZeroPlus, Delete = () => RemoveFilter(source) };
            }
            else
            {
                var desc = _typeDescriptors.First(s => s.Type == source.Type);
                var name = desc?.Name ?? "???";
                return new FilterActiveModel { Source = source, SliderType = desc.SliderType, Name = name, Delete = () => RemoveFilter(source) };
            }
        }

        private void ApplyFilter(FilterActiveModel a, SceneItemFilter[] filters)
        {
            var source = filters.First(s => a.IsTheSame(s));

            a.Amount.SilentValue = source.Value;
            a.IsEnabled.SilentValue = source.Enabled;

            a.IsEnabled.OnChange = (o, n) => { source.Enabled = n; UpdateSourceArray(); };
            a.Amount.OnChange = (o, n) => { source.Value = n; UpdateSourceArray(); };
        }

        private void UpdateSourceArray()
        {
            _item.Model.Filters = _item.Model.Filters?.Filters != null ? new SceneItemFilters { Filters = _item.Model.Filters.Filters.ToArray() } : null;
        }
    }

    public class FilterTypeDescriptor
    {
        public FilterTypeDescriptor(string name, SceneItemFilterType type, FilterCategory category, SliderType sliderType, double defaultValue = 0.0)
        {
            Name = name;
            Type = type;
            SliderType = sliderType;
            DefaultValue = defaultValue;
            Category = category;
        }

        public string Name { get; set; }

        public SceneItemFilterType Type { get; set; }

        public FilterCategory Category { get; set; }

        public SliderType SliderType {get;set;}

        public double DefaultValue { get; }
    }

    public enum SliderType { No, MinusPlus, ZeroPlus }

    public enum FilterCategory { Basic, Quick, Creative }

    public class FilterSourceModel
    {
        public FilterTypeDescriptor Desc { get; set; }

        public Property<bool> InUse { get; } = new Property<bool>();
    }

    public class LutModel
    {
        public string Name { get; set; }

        public string ResourceId { get; set; }

        public Property<bool> InUse { get; } = new Property<bool>();
    }


    public class FilterActiveModel
    {
        public SceneItemFilter Source { get; set; }

        public string Name { get; set; }

        public Property<bool> IsEnabled { get; } = new Property<bool>();

        public Action Delete { get; set; }

        public SliderType SliderType { get; set; }

        public Property<double> Amount { get; } = new Property<double>();

        public bool IsTheSame(SceneItemFilter s)
        {
            return Source.Type == s.Type && Source.LutResourceId == s.LutResourceId;
        }
    }
}
