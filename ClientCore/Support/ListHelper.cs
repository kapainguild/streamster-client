using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Streamster.ClientCore.Models
{
    class ListHelper
    {
        public static (bool ChengeSelection, TTarget Selection) UpdateCollectionWithSelection<TSource, TTarget>(CoreData coreData, List<TSource> source, ObservableCollection<TTarget> targets, Property<TTarget> selectedProperty,
            Func<TTarget, string> getId, Func<TSource, string, TTarget> creator)
        {
            var selected = selectedProperty.Value;
            var selectedIndex = -1;
            if (selected != null)
                selectedIndex = targets.IndexOf(selected);

            int position = 0;
            foreach (var model in source)
            {
                var vm = position < targets.Count ? targets[position] : default;
                var modelId = coreData.GetId(model);
                if (vm == null || getId(vm) != modelId)
                {
                    var oldAnotherPosition = targets.FirstOrDefault(s => getId(s) == modelId);
                    if (oldAnotherPosition != null)
                    {
                        targets.Remove(oldAnotherPosition);
                        targets.Insert(position, oldAnotherPosition);
                    }
                    else
                        targets.Insert(position, creator(model, modelId));
                }
                position++;
            }

            while (targets.Count > source.Count)
                targets.RemoveAt(targets.Count - 1);

            // try update selection
            if (selected != null)
            {
                if (!targets.Contains(selected) && selectedIndex >= 0)
                {
                    if (targets.Count == 0)
                        return (true, default);
                    return (true, targets[Math.Min(selectedIndex, targets.Count - 1)]);
                }
            }
            return (false, default);
        }

        public static void UpdateCollection<TSource, TTarget>(CoreData coreData, List<TSource> source, ObservableCollection<TTarget> targets,
            Func<TTarget, string> getId, Func<TSource, string, TTarget> creator, Func<TTarget, TSource> getSource = null)
        {
            int position = 0;
            foreach (var model in source)
            {
                var vm = position < targets.Count ? targets[position] : default;
                var modelId = coreData.GetId(model);
                bool referenceEqual = getSource == null || vm == null ? true : Object.ReferenceEquals(getSource(vm), model);
                if (vm == null || getId(vm) != modelId || !referenceEqual)
                {
                    var oldAnotherPosition = getSource == null ?
                                             targets.FirstOrDefault(s => getId(s) == modelId):
                                             targets.FirstOrDefault(s => getId(s) == modelId && Object.ReferenceEquals(getSource(s), model));
                    if (oldAnotherPosition != null)
                    {
                        targets.Remove(oldAnotherPosition);
                        targets.Insert(position, oldAnotherPosition);
                    }
                    else
                        targets.Insert(position, creator(model, modelId));
                }
                position++;
            }

            while (targets.Count > source.Count)
                targets.RemoveAt(targets.Count - 1);
        }

        public static void UpdateCollectionNoId<TSource, TTarget>(List<TSource> source, ObservableCollection<TTarget> targets, Func<TSource, TTarget, bool> isTheSame, Func<TSource, TTarget> creator)
        {
            int position = 0;
            foreach (var model in source)
            {
                var vm = position < targets.Count ? targets[position] : default;
                if (vm == null || !isTheSame(model, vm))
                {
                    var oldAnotherPosition = targets.FirstOrDefault(s => isTheSame(model, s));
                    if (oldAnotherPosition != null)
                    {
                        targets.Remove(oldAnotherPosition);
                        targets.Insert(position, oldAnotherPosition);
                    }
                    else
                        targets.Insert(position, creator(model));
                }
                position++;
            }

            while (targets.Count > source.Count)
                targets.RemoveAt(targets.Count - 1);
        }
    }
}
