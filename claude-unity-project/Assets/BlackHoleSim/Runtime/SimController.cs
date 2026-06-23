using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackHoleSim
{
    /// <summary>Input + camera orbit. Left-drag aims a throw; release launches a body.
    /// Right-drag orbits the camera; scroll zooms. Uses the new Input System.</summary>
    public class SimController : MonoBehaviour
    {
        [SerializeField] BlackHole blackHole;
        [SerializeField] Camera cam;
        [SerializeField] GameObject throwablePrefab;
        [SerializeField] float spawnDistance = 24f;
        [SerializeField] float throwSpeedScale = 0.05f;
        [SerializeField] float bodyMaxRadius = 80f;

        [Header("Camera orbit")]
        [SerializeField] float orbitSpeed = 0.2f;
        [SerializeField] float zoomSpeed = 2f;

        Vector2 dragStart;
        bool dragging;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            HandleThrow(mouse);
            HandleCameraOrbit(mouse);
        }

        void HandleThrow(Mouse mouse)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                dragStart = mouse.position.ReadValue();
                dragging = true;
            }
            else if (mouse.leftButton.wasReleasedThisFrame && dragging)
            {
                dragging = false;
                Vector2 dragVec = mouse.position.ReadValue() - dragStart;
                Vector3 spawnPos = cam.transform.position + cam.transform.forward * spawnDistance;
                Vector3 worldDir = cam.transform.right * dragVec.x + cam.transform.up * dragVec.y;
                Throw(spawnPos, worldDir * throwSpeedScale);
            }
        }

        void HandleCameraOrbit(Mouse mouse)
        {
            Vector3 pivot = blackHole.transform.position;
            if (mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                cam.transform.RotateAround(pivot, Vector3.up, d.x * orbitSpeed);
                cam.transform.RotateAround(pivot, cam.transform.right, -d.y * orbitSpeed);
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Vector3 dir = (cam.transform.position - pivot).normalized;
                cam.transform.position += dir * (-Mathf.Sign(scroll) * zoomSpeed);
            }
        }

        void Throw(Vector3 position, Vector3 velocity)
        {
            var go = Instantiate(throwablePrefab, position, Quaternion.identity);
            go.GetComponent<ThrowableBody>().Launch(blackHole, position, velocity, bodyMaxRadius);
        }

        // MCP/debug hook: spawn a body on a sub-orbital trajectory so the throw can be verified headlessly.
        public static void DebugThrowInScene()
        {
            var ctrl = Object.FindAnyObjectByType<SimController>();
            if (ctrl == null) { Debug.LogWarning("[SimController] none in scene"); return; }

            Vector3 pos = ctrl.blackHole.transform.position + new Vector3(16f, 0f, 0f);
            float orbit = ctrl.blackHole.OrbitalSpeed(16f);
            Vector3 v = new Vector3(0f, 0f, orbit * 0.7f); // sub-orbital -> elliptical infall
            ctrl.Throw(pos, v);
            Debug.Log("[SimController] DebugThrow spawned");
        }
    }
}
