using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleSim.UI
{
    public class FloatSliderRow : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text valueText;
        [SerializeField] Slider slider;

        bool suppressFeedback;

        public void Bind(string labelText, ObservableValue<float> value, float min, float max)
        {
            label.text = labelText;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;

            void ApplyValue(float v)
            {
                suppressFeedback = true;
                slider.value = v;
                valueText.text = v.ToString("0.###");
                suppressFeedback = false;
            }

            ApplyValue(value.Value);
            value.Changed += ApplyValue;

            slider.onValueChanged.AddListener(v =>
            {
                if (suppressFeedback) return;
                value.Value = v;
            });
        }
    }
}
