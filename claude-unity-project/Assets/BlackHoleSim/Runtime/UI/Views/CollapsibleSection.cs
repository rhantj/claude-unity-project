using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleSim.UI
{
    public class CollapsibleSection : MonoBehaviour
    {
        [SerializeField] Button headerButton;
        [SerializeField] GameObject content;

        public void Bind(ObservableValue<bool> expanded)
        {
            content.SetActive(expanded.Value);
            expanded.Changed += content.SetActive;
            headerButton.onClick.AddListener(() => expanded.Value = !expanded.Value);
        }
    }
}
