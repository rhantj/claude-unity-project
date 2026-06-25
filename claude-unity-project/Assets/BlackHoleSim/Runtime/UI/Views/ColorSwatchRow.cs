using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleSim.UI
{
    public class ColorSwatchRow : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Slider rSlider;
        [SerializeField] Slider gSlider;
        [SerializeField] Slider bSlider;
        [SerializeField] Image swatch;

        bool suppressFeedback;
        ObservableValue<Color> bound;

        public void Bind(string labelText, ObservableValue<Color> value)
        {
            label.text = labelText;
            bound = value;

            foreach (var s in new[] { rSlider, gSlider, bSlider })
            {
                s.minValue = 0f;
                s.maxValue = 1f;
            }

            ApplyValue(value.Value);
            value.Changed += ApplyValue;

            rSlider.onValueChanged.AddListener(_ => PushFromSliders());
            gSlider.onValueChanged.AddListener(_ => PushFromSliders());
            bSlider.onValueChanged.AddListener(_ => PushFromSliders());
        }

        void ApplyValue(Color c)
        {
            suppressFeedback = true;
            rSlider.value = c.r;
            gSlider.value = c.g;
            bSlider.value = c.b;
            swatch.color = c;
            suppressFeedback = false;
        }

        void PushFromSliders()
        {
            if (suppressFeedback) return;
            var c = new Color(rSlider.value, gSlider.value, bSlider.value, 1f);
            swatch.color = c;
            bound.Value = c;
        }
    }
}
