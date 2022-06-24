using Clutch.DeltaModel;
using System;
using System.ComponentModel;
using Xunit;

namespace DeltaModel.Test
{
    public class ModelTest
    {
        private readonly DeltaModelManager<ITestModel> _manager1;
        private readonly DeltaModelManager<ITestModel> _manager2;
        private readonly DeltaModelManager<ITestModel> _manager3;
        private readonly ModelClient _modelClient12;
        private readonly ModelClient _modelClient21;
        private readonly ModelClient _modelClient32;
        private readonly ModelClient _modelClient23;


        public ModelTest()
        {
            var b = new DeltaModelBuilder();
            b.Config<ILevel2>(c => { c.HasLocal((s, m, p) => new Level2Local(m, s)); });
            _manager1 = b.Build<ITestModel>();

            var b2 = new DeltaModelBuilder();
            b2.Config<ILevel2>(c => { c.HasLocal((s, m, p) => new Level2Local(m, s)); });
            _manager2 = b2.Build<ITestModel>();

            var b3 = new DeltaModelBuilder();
            _manager3 = b3.Build<ITestModel>();

            _modelClient12 = new ModelClient(_manager1, new FilterConfigurator(true).Build());
            _modelClient21 = new ModelClient(_manager2, new FilterConfigurator(true).Build());
            _modelClient23 = new ModelClient(_manager2, new FilterConfigurator(true).Build());
            _modelClient32 = new ModelClient(_manager3, new FilterConfigurator(true).Build());

            _manager1.Register(_modelClient12);

            _manager2.Register(_modelClient21);
            _manager2.Register(_modelClient23);

            _manager3.Register(_modelClient32);

        }

        [Fact]
        public void AddReplaceRemoveAndApplyChanges()
        {
            _manager1.Root.Level1 = _manager1.Create<ILevel1>();
            _manager1.Root.Level1.IntValue = 42;
            _manager1.Root.Level1.StringValue = "43";

            _manager1.Root.Level1.Level2 = _manager1.Create<ILevel2>();
            _manager1.Root.Level1.Level2.GuidValue = Guid.NewGuid();
            _manager1.Root.Level1.Level2.Level3 = new Level3 { IntValue = 44 };

            _manager1.Root.Dictionary1[10] = _manager1.Create<ILevel1>();
            _manager1.Root.Dictionary1[10].IntValue = 40;

            var level1 = _manager1.Create<ILevel1>();
            level1.IntValue = 50;
            level1.Level2 = _manager1.Create<ILevel2>();
            level1.Dictionary2["some"] = _manager1.Create<ILevel2>();
            level1.Dictionary2["some"].GuidValue = Guid.NewGuid();

            _manager1.Root.Dictionary1[5] = level1;

            var a = _modelClient12.SerializeAndClearChanges();
            _manager2.ApplyChanges(_modelClient21, a);
            var b = _modelClient23.SerializeAndClearChanges();
            _manager3.ApplyChanges(_modelClient32, b);
            Assert.True(_manager1.IsDeepEqual(_manager1.Root, _manager2.Root));
            Assert.True(_manager1.IsDeepEqual(_manager1.Root, _manager3.Root));

            // replace
            _manager1.Root.Level1.IntValue = 43;
            _manager1.Root.Level1.Level2 = _manager1.Create<ILevel2>();
            _manager1.Root.Dictionary1[10] = _manager1.Create<ILevel1>();

            _manager2.ApplyChanges(_modelClient21, _modelClient12.SerializeAndClearChanges());
            _manager3.ApplyChanges(_modelClient32, _modelClient23.SerializeAndClearChanges());
            Assert.True(_manager1.IsDeepEqual(_manager1.Root, _manager2.Root));
            Assert.True(_manager1.IsDeepEqual(_manager1.Root, _manager3.Root));

            //remove
            _manager1.Root.Level1.StringValue = null;
            _manager1.Root.Level1.Level2 = null;
            _manager1.Root.Dictionary1.Remove(10);

            _manager2.ApplyChanges(_modelClient21, _modelClient12.SerializeAndClearChanges());
            _manager3.ApplyChanges(_modelClient32, _modelClient23.SerializeAndClearChanges());
            Assert.True(_manager1.IsDeepEqual(_manager1.Root, _manager2.Root));
            Assert.True(_manager1.IsDeepEqual(_manager1.Root, _manager3.Root));
        }

        [Fact]
        public void LocalsAreCreated()
        {
            _manager1.Root.Level1 = _manager1.Create<ILevel1>();
            _manager1.Root.Level1.Level2 = _manager1.Create<ILevel2>();

            var local = (Level2Local)((ILocalHolder)_manager1.Root.Level1.Level2).Local;

            local.IntValue = 42;

            string changes = _modelClient12.SerializeAndClearChanges();
            _manager2.ApplyChanges(_modelClient21, changes);

            var local2 = (Level2Local)((ILocalHolder)_manager2.Root.Level1.Level2).Local;

            Assert.Equal(0, local2.IntValue);
        }

        [Fact]
        public void NotifyPropertyChangedWorks()
        {
            _manager1.Root.Level1 = _manager1.Create<ILevel1>();
            _manager1.Root.Level1.Level2 = _manager1.Create<ILevel2>();

            string level1Property = null;
            string level2Property = null;

            ((INotifyPropertyChanged)_manager1.Root.Level1).PropertyChanged += (s, e) => level1Property = e.PropertyName;
            ((INotifyPropertyChanged)_manager1.Root.Level1.Level2).PropertyChanged += (s, e) => level2Property = e.PropertyName;

            _manager2.ApplyChanges(_modelClient21, _modelClient12.SerializeAndClearChanges());

            string level1Property2 = null;
            string level2Property2 = null;

            ((INotifyPropertyChanged)_manager2.Root.Level1).PropertyChanged += (s, e) => level1Property2 = e.PropertyName;
            ((INotifyPropertyChanged)_manager2.Root.Level1.Level2).PropertyChanged += (s, e) => level2Property2 = e.PropertyName;

            _manager1.Root.Level1.IntValue = 42;
            _manager1.Root.Level1.Level2.GuidValue = Guid.NewGuid();

            Assert.Equal("IntValue", level1Property);
            Assert.Equal("GuidValue", level2Property);

            _manager2.ApplyChanges(_modelClient21, _modelClient12.SerializeAndClearChanges());

            Assert.Equal("IntValue", level1Property2);
            Assert.Equal("GuidValue", level2Property2);
        }
    }
}
