using UnityEngine;

namespace ImpactCFXDemo
{
    public class DemoExit : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Application.Quit();
            }
        }
    }
}