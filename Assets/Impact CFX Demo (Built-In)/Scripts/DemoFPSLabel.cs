using UnityEngine;
using UnityEngine.UI;

namespace ImpactCFXDemo
{
    public class DemoFPSLabel : MonoBehaviour
    {
        public Text text;
        public float UpdateInterval = 0.5f;

        private float timer = float.MaxValue;

        private void Update()
        {
            if (timer > UpdateInterval)
            {
                timer = 0;

                int fps = Mathf.RoundToInt(1 / Time.deltaTime);
                text.text = $"{fps} FPS";
            }

            timer += Time.deltaTime;
        }
    }
}

