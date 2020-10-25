using Clutch.DeltaModel;
using System;
using Xunit;

namespace DeltaModel.Test
{
    public class BsonSerializationTest
    {
        private readonly DeltaModelManager<ITestModel> _manager;

        static BsonSerializationTest()
        {
            DeltaModelManager.RegisterDiscriminatorConvention(typeof(Level3));
        }

        public BsonSerializationTest()
        {
            
            _manager = new DeltaModelBuilder().Build<ITestModel>();
        }

        [Fact]
        public void SerializeDeserializeWithFilter()
        {
            var filter = new FilterConfigurator(true)
                                .Allow<ILevel1>(c => c.Deny(t => t.IntValue))
                                .Allow<ILevel2>(c => c.Allow(t => t.Level3))
                                .Build();

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

            var document = _manager.SerializeBson(_manager.Root, filter);
            var result = _manager.DeserializeBson<ITestModel>(document);


            Assert.Equal(0, result.Level1.IntValue);
            Assert.Equal("43", result.Level1.StringValue);
            Assert.Equal(Guid.Empty, result.Level1.Level2.GuidValue);

            Assert.Equal(44, result.Level1.Level2.Level3.IntValue);
            Assert.Equal(0, result.Dictionary1[10].IntValue);
            Assert.Equal("some", result.Dictionary1[10].StringValue);
            Assert.Equal(Guid.Empty, result.Dictionary1[10].Dictionary2["some"].GuidValue);

            Assert.Equal(0, result.Dictionary1[5].IntValue);
            Assert.Equal("some", result.Dictionary1[5].StringValue);
            Assert.Equal(Guid.Empty, result.Dictionary1[5].Dictionary2["some"].GuidValue);
            Assert.Equal(44, result.Dictionary1[5].Dictionary2["some"].Level3.IntValue);

        }
    }
}
