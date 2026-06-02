using System;
using UnityEngine;

namespace ImpactCFXDemo
{
    public class DemoTeleporterArea : MonoBehaviour
    {
        public event Action<Rigidbody, Vector3> OnTriggerEntered;

        public Vector3 AreaSize;

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody r = other.GetComponentInParent<Rigidbody>();

            if (r != null)
            {
                Vector3 localPos = transform.InverseTransformPoint(r.position);
                Vector3 localNormalizedPos = new Vector3(localPos.x / AreaSize.x, localPos.y / AreaSize.y, localPos.z / AreaSize.z);
                OnTriggerEntered?.Invoke(r, localNormalizedPos);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawWireCube(transform.position, AreaSize);
        }
    }
}

