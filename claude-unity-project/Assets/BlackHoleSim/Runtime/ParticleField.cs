using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>CPU-integrated test particles rendered through a ParticleSystem.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleField : MonoBehaviour
    {
        [SerializeField] BlackHole blackHole;
        [SerializeField] int count = 1500;
        [SerializeField] float innerRadius = 5f;
        [SerializeField] float outerRadius = 14f;
        [SerializeField] float diskThickness = 0.4f;
        [SerializeField, Range(0f, 0.5f)] float speedJitter = 0.12f;
        [SerializeField] float maxRadius = 45f;
        [SerializeField] float particleSize = 0.18f;

        ParticleSystem ps;
        ParticleSystem.Particle[] rendered;
        Vector3[] pos;
        Vector3[] vel;
        System.Func<Vector3, Vector3> accelFn;

        void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            accelFn = blackHole.AccelerationAt;

            var main = ps.main;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var emission = ps.emission;
            emission.enabled = false;

            EnsureRenderMaterial();

            pos = new Vector3[count];
            vel = new Vector3[count];
            rendered = new ParticleSystem.Particle[count];
            for (int i = 0; i < count; i++) Spawn(i);
        }

        // The default ParticleSystem material is not URP-compatible (renders magenta).
        // Assign an unlit additive material so particles render correctly under URP.
        void EnsureRenderMaterial()
        {
            var renderer = GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Sprites/Default");
            if (shader == null) return;

            var mat = new Material(shader) { color = Color.white };
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        void Spawn(int i)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float rad = Mathf.Lerp(innerRadius, outerRadius, Random.value);
            Vector3 bh = blackHole.transform.position;
            Vector3 p = bh + new Vector3(
                Mathf.Cos(ang) * rad,
                (Random.value - 0.5f) * diskThickness,
                Mathf.Sin(ang) * rad);

            Vector3 radial = (p - bh).normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
            float speed = blackHole.OrbitalSpeed(rad) * (1f + (Random.value - 0.5f) * 2f * speedJitter);

            pos[i] = p;
            vel[i] = tangent * speed;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 bh = blackHole.transform.position;
            float maxSq = maxRadius * maxRadius;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = pos[i], v = vel[i];
                GravityIntegrator.Step(ref p, ref v, accelFn, dt);
                pos[i] = p; vel[i] = v;

                if (blackHole.IsCaptured(p) || (p - bh).sqrMagnitude > maxSq)
                    Spawn(i);
            }
        }

        void LateUpdate()
        {
            for (int i = 0; i < count; i++)
            {
                rendered[i].position = pos[i];
                rendered[i].startSize = particleSize;
                rendered[i].startColor = Color.white;
                rendered[i].startLifetime = 1f;
                rendered[i].remainingLifetime = 1f;
            }
            ps.SetParticles(rendered, count);
        }
    }
}
