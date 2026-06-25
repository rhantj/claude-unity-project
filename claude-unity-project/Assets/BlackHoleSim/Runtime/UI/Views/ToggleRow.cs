using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleSim.UI
{
    public class ToggleRow : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Toggle toggle;

        bool suppressFeedback;

        public void Bind(string labelText, ObservableValue<bool> value)
        {
            label.text = labelText;

            void ApplyValue(bool v)
            {
                suppressFeedback = true;
                toggle.isOn = v;
                suppressFeedback = false;
            }

            ApplyValue(value.Value);
            value.Changed += ApplyValue;

            toggle.onValueChanged.AddListener(v =>
            {
                if (suppressFeedback) return;
                value.Value = v;
            });
        }
    }
}
