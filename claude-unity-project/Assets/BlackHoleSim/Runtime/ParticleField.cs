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
        [SerializeField, Range(0.3f, 0.95f)] float infallSpeedFactor = 0.65f;
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
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var emission = ps.emission;
            emission.enabled = false;

            EnsureRenderMaterial();

            Reinitialize(count);
        }

        public void Reinitialize(int newCount)
        {
            if (ps == null) ps = GetComponent<ParticleSystem>();
            count = Mathf.Max(1, newCount);

            var main = ps.main;
            main.maxParticles = count;

            pos = new Vector3[count];
            vel = new Vector3[count];
            rendered = new ParticleSystem.Particle[count];
            for (int i = 0; i < count; i++) Spawn(i);
        }

        public int Count { get => count; set => Reinitialize(value); }
        public float InnerRadius { get => innerRadius; set => innerRadius = value; }
        public float OuterRadius { get => outerRadius; set => outerRadius = value; }
        public float DiskThickness { get => diskThickness; set => diskThickness = value; }
        public float SpeedJitter { get => speedJitter; set => speedJitter = value; }
        public float MaxRadius { get => maxRadius; set => maxRadius = value; }
        public float InfallSpeedFactor { get => infallSpeedFactor; set => infallSpeedFactor = value; }
        public float ParticleSize { get => particleSize; set => particleSize = value; }

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

        // 호라이즌에 빨려든(또는 멀리 날아간) 물질은 영영 사라진다 — 그 자리는 바깥 먼 곳에서
        // 새 물질이 아임계 속도로 떨어져 들어와 채운다(외부 공급 강착). 슬롯만 재사용, 새 할당 없음.
        void SpawnInfalling(int i)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float rad = Mathf.Lerp(outerRadius * 1.6f, maxRadius * 0.9f, Random.value);
            Vector3 bh = blackHole.transform.position;
            Vector3 p = bh + new Vector3(
                Mathf.Cos(ang) * rad,
                (Random.value - 0.5f) * diskThickness,
                Mathf.Sin(ang) * rad);

            Vector3 radial = (p - bh).normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
            // 원궤도보다 느리게 → 안쪽으로 낙하해 원반/블랙홀로 향한다.
            float speed = blackHole.OrbitalSpeed(rad) * infallSpeedFactor * (1f + (Random.value - 0.5f) * 0.4f);

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
                    SpawnInfalling(i);
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
