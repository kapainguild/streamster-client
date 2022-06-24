using Clutch.DeltaModel;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace DeltaModel.Test
{
    public class ModelInit
    {
        private readonly DeltaModelManager<ITestModel> _manager1;
        private readonly DeltaModelManager<ITestModel> _manager2;
        private readonly ModelClient _modelClient12;
        private readonly ModelClient _modelClient21;

        public ModelInit()
        {
            var b = new DeltaModelBuilder();
            _manager1 = b.Build<ITestModel>();

            var b2 = new DeltaModelBuilder();
            _manager2 = b2.Build<ITestModel>();


            _modelClient12 = new ModelClient(_manager1, new FilterConfigurator(true).Build());
            _modelClient21 = new ModelClient(_manager2, new FilterConfigurator(true).Build());
        }

        [Fact]
        public void InitAfterChanges()
        {
            _manager1.Root.Level1 = _manager1.Create<ILevel1>();
            _manager1.Root.Level1.IntValue = 42;
            _manager1.Root.Level1.StringValue = "43";

            _manager1.Root.Level1.Level2 = _manager1.Create<ILevel2>();
            _manager1.Root.Level1.Level2.GuidValue = Guid.NewGuid();
            _manager1.Root.Level1.Level2.Level3 = new Level3 { IntValue = 44 };

            _manager2.Register(_modelClient21);
            _manager2.ApplyChanges(_modelClient21, _manager1.Register(_modelClient12));

            Assert.True(_manager1.IsDeepEqual(_manager1.Root, _manager2.Root));

        }
    }
}
