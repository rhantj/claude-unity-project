using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>Single gravity source. The one place mass/G/horizon live.</summary>
    public class BlackHole : MonoBehaviour
    {
        [SerializeField] float gravitationalConstant = 1f;
        [SerializeField] float mass = 4000f;
        [SerializeField] float softening = 0.5f;
        [SerializeField] float eventHorizonRadius = 2f;

        public float GravitationalConstant
        {
            get => gravitationalConstant;
            set => gravitationalConstant = value;
        }

        public float Mass
        {
            get => mass;
            set => mass = value;
        }

        public float Softening
        {
            get => softening;
            set => softening = value;
        }

        public float EventHorizonRadius
        {
            get => eventHorizonRadius;
            set => eventHorizonRadius = value;
        }

        public float Mu => gravitationalConstant * mass;

        void Awake()
        {
            // Own material instance (unlit black) so it never shares the default Lit asset.
            var r = GetComponent<Renderer>();
            if (r == null) return;
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (s != null) r.material = new Material(s) { color = Color.black };
        }

        // PW 중력의 r_s로 eventHorizonRadius를 사용한다. (softening은 광학 렌즈 셰이더 미러용으로 보존)
        public Vector3 AccelerationAt(Vector3 pos) =>
            GravityField.AccelerationAt(transform.position, Mu, eventHorizonRadius, pos);

        public float OrbitalSpeed(float radius) => GravityField.OrbitalSpeed(Mu, radius, eventHorizonRadius);

        public bool IsCaptured(Vector3 pos)
        {
            float h = eventHorizonRadius;
            return (pos - transform.position).sqrMagnitude <= h * h;
        }

        // Test/editor hook for setting tunables programmatically.
        public void Configure(float gravitationalConstant, float mass, float softening, float eventHorizonRadius)
        {
            this.gravitationalConstant = gravitationalConstant;
            this.mass = mass;
            this.softening = softening;
            this.eventHorizonRadius = eventHorizonRadius;
        }
    }
}
