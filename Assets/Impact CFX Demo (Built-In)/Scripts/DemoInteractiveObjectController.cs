using UnityEngine;

namespace ImpactCFXDemo
{
    public class DemoInteractiveObjectController : MonoBehaviour
    {
        private DemoInteractiveObject[] interactiveObjects;

        private void Start()
        {
            interactiveObjects = GetComponentsInChildren<DemoInteractiveObject>();
        }

        public void ResetAll()
        {
            foreach (var item in interactiveObjects)
            {
                item.ResetObject();
            }
        }
    }
}

