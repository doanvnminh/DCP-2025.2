using UnityEngine;

namespace ImpactCFXDemo
{
    public class DemoTeleporterController : MonoBehaviour
    {
        public DemoTeleporterArea EntranceArea;
        public DemoTeleporterArea ExitArea;

        private void Awake()
        {
            EntranceArea.OnTriggerEntered += entranceAreaTriggerEntered;
        }

        private void entranceAreaTriggerEntered(Rigidbody r, Vector3 localNormalizedPos)
        {
            r.position = ExitArea.transform.TransformPoint(Vector3.Scale(localNormalizedPos, ExitArea.AreaSize));
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawLine(EntranceArea.transform.position, ExitArea.transform.position);
        }
    }
}

