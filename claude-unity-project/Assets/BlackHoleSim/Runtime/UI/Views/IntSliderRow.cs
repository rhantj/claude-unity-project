using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleSim.UI
{
    public class IntSliderRow : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text valueText;
        [SerializeField] Slider slider;

        bool suppressFeedback;

        public void Bind(string labelText, ObservableValue<int> value, int min, int max)
        {
            label.text = labelText;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;

            void ApplyValue(int v)
            {
                suppressFeedback = true;
                slider.value = v;
                valueText.text = v.ToString();
                suppressFeedback = false;
            }

            ApplyValue(value.Value);
            value.Changed += ApplyValue;

            slider.onValueChanged.AddListener(v =>
            {
                if (suppressFeedback) return;
                value.Value = Mathf.RoundToInt(v);
            });
        }
    }
}
