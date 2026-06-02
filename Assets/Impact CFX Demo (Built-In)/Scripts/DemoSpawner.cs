using System.Collections.Generic;
using UnityEngine;

namespace ImpactCFXDemo
{
    public class DemoSpawner : MonoBehaviour
    {
        public GameObject[] Prefabs;

        public Vector3 SpawnArea = new Vector3(1, 1, 1);
        public int SpawnCount;
        public int SpawnLimit;

        public Vector3 InitialVelocity;

        public bool RandomRotation;
        public Vector3 StartRotation;

        public bool AutoSpawn;
        public float AutoSpawnRate;

        private float timer;

        private List<GameObject> spawnedInstances = new List<GameObject>();

        public void StartAutoSpawn()
        {
            AutoSpawn = true;
        }

        public void StopAutoSpawn()
        {
            AutoSpawn = false;
        }

        public void Clear()
        {
            foreach (var item in spawnedInstances)
            {
                Destroy(item);
            }

            spawnedInstances.Clear();
        }

        private void Update()
        {
            if (AutoSpawn)
            {
                if (timer > AutoSpawnRate)
                {
                    Spawn();
                    timer = 0;
                }

                timer += Time.deltaTime;
            }
        }

        public void Spawn()
        {
            if (SpawnLimit > 0 && spawnedInstances.Count >= SpawnLimit)
                return;

            for (int i = 0; i < SpawnCount; i++)
            {
                Vector3 pos = transform.position + new Vector3(Random.Range(-SpawnArea.x, SpawnArea.x), Random.Range(-SpawnArea.y, SpawnArea.y), Random.Range(-SpawnArea.z, SpawnArea.z)) / 2f;
                Quaternion rot = RandomRotation ? Random.rotation : Quaternion.Euler(StartRotation);

                int randomIndex = Random.Range(0, Prefabs.Length);
                GameObject instance = Instantiate(Prefabs[randomIndex], pos, rot);

                Rigidbody r = instance.GetComponent<Rigidbody>();
                if (r != null)
                {
                    r.AddForce(InitialVelocity, ForceMode.VelocityChange);
                }

                spawnedInstances.Add(instance);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, SpawnArea);

            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position, InitialVelocity);

            if (!RandomRotation)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, Quaternion.Euler(StartRotation) * Vector3.up);
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, Quaternion.Euler(StartRotation) * Vector3.right);
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, Quaternion.Euler(StartRotation) * Vector3.forward);

            }
        }
    }
}
