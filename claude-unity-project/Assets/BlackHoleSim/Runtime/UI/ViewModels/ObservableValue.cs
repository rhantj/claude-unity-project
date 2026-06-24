using System;

namespace BlackHoleSim.UI
{
    /// <summary>값이 바뀔 때만 Changed를 발생시키는 경량 ViewModel 셀.</summary>
    public class ObservableValue<T>
    {
        T value;

        public ObservableValue(T initial) => value = initial;

        public T Value
        {
            get => value;
            set
            {
                if (Equals(this.value, value)) return;
                this.value = value;
                Changed?.Invoke(value);
            }
        }

        public event Action<T> Changed;
    }
}
