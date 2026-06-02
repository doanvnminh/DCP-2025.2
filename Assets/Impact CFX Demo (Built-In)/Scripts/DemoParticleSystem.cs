using UnityEngine;

namespace ImpactCFXDemo
{
    public class DemoParticleSystem : MonoBehaviour
    {
        public ParticleSystem[] ParticleSystems;

        public void Toggle()
        {
            foreach (var item in ParticleSystems)
            {
                if (item.isPlaying)
                    item.Stop();
                else
                    item.Play();
            }
        }
    }
}