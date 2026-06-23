using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>A single body that shares the gravity core, with a LineRenderer trail.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ThrowableBody : MonoBehaviour
    {
        BlackHole blackHole;
        Vector3 vel;
        float maxRadius;
        System.Func<Vector3, Vector3> accelFn;

        LineRenderer trail;
        Vector3[] trailPts;
        int trailCount;
        const int TrailLength = 256;

        public void Launch(BlackHole bh, Vector3 position, Vector3 velocity, float maxRadius)
        {
            blackHole = bh;
            transform.position = position;
            vel = velocity;
            this.maxRadius = maxRadius;
            accelFn = blackHole.AccelerationAt;

            ConfigureBodyMaterial();
            trail = GetComponent<LineRenderer>();
            ConfigureTrail();
            trailPts = new Vector3[TrailLength];
            trailCount = 0;
            trail.positionCount = 0;
        }

        void ConfigureBodyMaterial()
        {
            // Own material instance so it never mutates the shared default Lit asset.
            var mesh = GetComponent<MeshRenderer>();
            if (mesh == null) return;
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (s != null) mesh.material = new Material(s) { color = new Color(1f, 0.8f, 0.3f, 1f) };
        }

        void ConfigureTrail()
        {
            // URP-compatible unlit material (default LineRenderer material renders magenta under URP).
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null) trail.material = new Material(shader) { color = new Color(1f, 0.8f, 0.3f, 1f) };
            trail.useWorldSpace = true;
            trail.startWidth = 0.12f;
            trail.endWidth = 0.04f;
            trail.numCapVertices = 2;
            trail.startColor = new Color(1f, 0.85f, 0.4f, 1f);
            trail.endColor = new Color(1f, 0.5f, 0.1f, 0.2f);
        }

        void FixedUpdate()
        {
            if (blackHole == null) return;

            Vector3 p = transform.position;
            GravityIntegrator.Step(ref p, ref vel, accelFn, Time.fixedDeltaTime);
            transform.position = p;
            PushTrail(p);

            float d = (p - blackHole.transform.position).magnitude;
            if (blackHole.IsCaptured(p) || d > maxRadius)
                Destroy(gameObject);
        }

        void PushTrail(Vector3 p)
        {
            if (trailCount < TrailLength)
            {
                trailPts[trailCount++] = p;
            }
            else
            {
                System.Array.Copy(trailPts, 1, trailPts, 0, TrailLength - 1);
                trailPts[TrailLength - 1] = p;
            }
            trail.positionCount = trailCount;
            trail.SetPositions(trailPts);
        }
    }
}
