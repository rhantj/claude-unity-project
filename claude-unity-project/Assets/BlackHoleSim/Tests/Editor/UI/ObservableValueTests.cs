using NUnit.Framework;
using BlackHoleSim.UI;

namespace BlackHoleSim.Tests.UI
{
    public class ObservableValueTests
    {
        [Test]
        public void SettingValue_FiresChangedEvent_WithNewValue()
        {
            var ov = new ObservableValue<float>(1f);
            float? received = null;
            ov.Changed += v => received = v;

            ov.Value = 2f;

            Assert.That(received, Is.EqualTo(2f));
        }

        [Test]
        public void SettingSameValue_DoesNotFireChangedEvent()
        {
            var ov = new ObservableValue<float>(1f);
            int callCount = 0;
            ov.Changed += _ => callCount++;

            ov.Value = 1f;

            Assert.That(callCount, Is.EqualTo(0));
        }

        [Test]
        public void Value_ReturnsCurrentValue_AfterConstruction()
        {
            var ov = new ObservableValue<int>(42);
            Assert.That(ov.Value, Is.EqualTo(42));
        }
    }
}
