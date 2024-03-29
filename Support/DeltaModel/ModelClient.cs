﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeltaModel
{
    public class ModelClient
    {
        private List<Change> _changes = new List<Change>();

        public ModelClient(DeltaModelManager manager, Filter filter)
        {
            Manager = manager;
            Filter = filter;
        }

        public Filter Filter { get; set; }

        public DeltaModelManager Manager { get; internal set; }

        public void AddChange(Change change)
        { 
            _changes.Add(change);
        }

        public string SerializeAndClearChanges()
        {
            lock (Manager)
            {
                if (_changes.Count > 0)
                {
                    var result = Change.SerializeChanges(_changes);
                    _changes.Clear();
                    return result;
                }
                return null;
            }
        }
    }
}
