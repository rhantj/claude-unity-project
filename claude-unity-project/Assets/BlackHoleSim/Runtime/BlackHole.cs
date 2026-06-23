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

        public float Mu => gravitationalConstant * mass;
        public float EventHorizonRadius => eventHorizonRadius;

        public Vector3 AccelerationAt(Vector3 pos) =>
            GravityField.AccelerationAt(transform.position, Mu, softening, pos);

        public float OrbitalSpeed(float radius) => GravityField.OrbitalSpeed(Mu, radius);

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
