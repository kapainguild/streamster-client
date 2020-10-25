using Clutch.DeltaModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeltaModel.Test
{
    public interface ILevel1
    {
        int IntValue { get; set; }

        string StringValue { get; set; }

        public ILevel2 Level2 { get; set; }

        public IDictionary<string, ILevel2> Dictionary2 { get; set; }
    }


    public class Level2Local
    {
        public Level2Local(DeltaModelManager manager, ILevel2 model)
        {
            Manager = manager;
            Model = model;
        }

        public DeltaModelManager Manager { get; }

        public ILevel2 Model { get; }

        public int IntValue { get; set; }
    }


    public interface ILevel2
    {
        Guid GuidValue { get; set; }

        Level3 Level3 { get; set; }
    }

    public class Level3
    {
        public int IntValue { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Level3 level)
            {
                return IntValue == level.IntValue;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IntValue);
        }
    }

    public interface ITestModel
    {
        public ILevel1 Level1 { get; set; }

        public IDictionary<int, ILevel1> Dictionary1 { get; set; }
    }
}
