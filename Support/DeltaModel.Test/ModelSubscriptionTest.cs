using DeltaModel;
using System;
using Xunit;

namespace DeltaModel.Test
{
    public class ModelSubscriptionTest
    {
        private DeltaModelManager<ITestModel> _manager;

        public ModelSubscriptionTest()
        {
            _manager = new DeltaModelBuilder().Build<ITestModel>();
        }

        [Fact]
        public void SubscribeUnsubscribeTest()
        {
            int counter = 0;

            int notifications = 0;
            _manager.Subscriptions.OnChangeForSubscriptions = () => notifications++;

            var sub = _manager.Subscriptions.SubscribeForType<ILevel1>((o, c) => counter++);
            _manager.Root.Level1 = _manager.Create<ILevel1>();

            _manager.Subscriptions.GetAndClearNotifications().ForEach(p => p());
            Assert.Equal(1, counter);
            Assert.Equal(1, notifications);


            sub.Unsubscribe();
            _manager.Root.Level1 = _manager.Create<ILevel1>();
            _manager.Subscriptions.GetAndClearNotifications().ForEach(p => p());

            Assert.Equal(1, counter);
            Assert.Equal(1, notifications);
        }

        [Fact]
        public void SecondLevelTest()
        {
            int counter = 0;
            int counter2 = 0;

            int notifications = 0;
            _manager.Subscriptions.OnChangeForSubscriptions = () => notifications++;

            _manager.Subscriptions.SubscribeForType<ILevel2>((o, c) => counter++);
            _manager.Subscriptions.SubscribeForProperties<ILevel2>((o, c, p) => counter2++, nameof(ILevel2.GuidValue));
            var level1 = _manager.Create<ILevel1>();
            level1.Level2 = _manager.Create<ILevel2>();
            level1.Level2.GuidValue = Guid.NewGuid();

            _manager.Root.Level1 = level1;

            _manager.Subscriptions.GetAndClearNotifications().ForEach(p => p());

            Assert.Equal(1, counter);
            Assert.Equal(1, counter2);
            Assert.Equal(1, notifications);
        }

        [Fact]
        public void ForPropertyTest()
        {
            int counter1 = 0;
            int counter2 = 0;

            int notifications = 0;
            _manager.Subscriptions.OnChangeForSubscriptions = () => notifications++;


            _manager.Subscriptions.SubscribeForProperties<ITestModel>((o, c, p) => counter1++, nameof(ITestModel.Level1));
            _manager.Subscriptions.SubscribeForAnyProperty<ITestModel>((o, c,e,r) => counter2++);
            _manager.Root.Level1 = _manager.Create<ILevel1>();

            _manager.Subscriptions.GetAndClearNotifications().ForEach(p => p());


            Assert.Equal(1, counter1);
            Assert.Equal(1, counter2);
            Assert.Equal(1, notifications);
        }
    }
}
