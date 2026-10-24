using UnityEngine;

namespace RayonixAR
{
    /// <summary>Flickers a flame mesh and its point light, and slowly spins the diya.</summary>
    public class DiyaFlame : MonoBehaviour
    {
        [SerializeField] private Transform flame;
        [SerializeField] private Light flameLight;
        [SerializeField] private Transform spinRoot;
        [SerializeField] private float spinDegreesPerSecond = 20f;
        [SerializeField] private float baseIntensity = 1.5f;

        private Vector3 flameBaseScale = Vector3.one;
        private float seed;

        private void Awake()
        {
            if (flame != null) flameBaseScale = flame.localScale;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float t = Time.time * 6f + seed;
            float n = Mathf.PerlinNoise(t, seed) - 0.5f;          // -0.5 .. 0.5
            float m = Mathf.PerlinNoise(seed, t * 1.7f) - 0.5f;

            if (flame != null)
            {
                flame.localScale = new Vector3(
                    flameBaseScale.x * (1f + n * 0.25f),
                    flameBaseScale.y * (1f + m * 0.45f),
                    flameBaseScale.z * (1f + n * 0.25f));
            }

            if (flameLight != null)
                flameLight.intensity = baseIntensity * (1f + n * 0.5f);

            if (spinRoot != null)
                spinRoot.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }
    }
}
