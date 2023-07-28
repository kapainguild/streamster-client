using DeltaModel;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace DeltaModel.Test
{
    public class ModelFilterTest
    {
        private readonly DeltaModelManager<ITestModel> _manager;
        private readonly DeltaModelManager<ITestModel> _manager2;

        public ModelFilterTest()
        {
            _manager = new DeltaModelBuilder().Build<ITestModel>();
            _manager2 = new DeltaModelBuilder().Build<ITestModel>();
        }

        [Fact]
        public void AddReplaceRemoveAndApplyChanges()
        {
            var modelClient = new ModelClient(_manager, 
                                 new FilterConfigurator(true)
                                .Allow<ILevel1>(c => c.Deny(t => t.IntValue))
                                .Allow<ILevel2>(c => c.Allow(t => t.Level3))
                                .Build()
            );
            _manager.Root.Level1 = _manager.Create<ILevel1>();
            _manager.Root.Level1.IntValue = 42;
            _manager.Root.Level1.StringValue = "43";

            _manager.Root.Level1.Level2 = _manager.Create<ILevel2>();
            _manager.Root.Level1.Level2.GuidValue = Guid.NewGuid();
            _manager.Root.Level1.Level2.Level3 = new Level3 { IntValue = 44 };

            _manager.Root.Dictionary1[10] = _manager.Create<ILevel1>();
            _manager.Root.Dictionary1[10].IntValue = 40;
            _manager.Root.Dictionary1[10].StringValue = "some";
            _manager.Root.Dictionary1[10].Dictionary2["some"] = _manager.Create<ILevel2>();
            _manager.Root.Dictionary1[10].Dictionary2["some"].GuidValue = Guid.NewGuid();

            var level1 = _manager.Create<ILevel1>();
            level1.IntValue = 50;
            level1.StringValue = "some";
            level1.Dictionary2["some"] = _manager.Create<ILevel2>();
            level1.Dictionary2["some"].GuidValue = Guid.NewGuid();
            level1.Dictionary2["some"].Level3 = new Level3 { IntValue = 44 };
            _manager.Root.Dictionary1[5] = level1;

            var changes = _manager.Register(modelClient);
            _manager2.ApplyChanges(modelClient, changes);
            Assert.Equal(0, _manager2.Root.Level1.IntValue);
            Assert.Equal("43", _manager2.Root.Level1.StringValue);
            Assert.Equal(Guid.Empty, _manager2.Root.Level1.Level2.GuidValue);

            Assert.Equal(44, _manager2.Root.Level1.Level2.Level3.IntValue);
            Assert.Equal(0, _manager2.Root.Dictionary1[10].IntValue);
            Assert.Equal("some", _manager2.Root.Dictionary1[10].StringValue);
            Assert.Equal(Guid.Empty, _manager2.Root.Dictionary1[10].Dictionary2["some"].GuidValue);

            Assert.Equal(0, _manager2.Root.Dictionary1[5].IntValue);
            Assert.Equal("some", _manager2.Root.Dictionary1[5].StringValue);
            Assert.Equal(Guid.Empty, _manager2.Root.Dictionary1[5].Dictionary2["some"].GuidValue);
            Assert.Equal(44, _manager2.Root.Dictionary1[5].Dictionary2["some"].Level3.IntValue);

        }
    }
}
