using UnityEngine;

namespace VRGame.Combat
{
    public class TestDummy : MonoBehaviour
    {
        public EnemyController enemy;

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                enemy.Activate();

                Destroy(this);
            }
        }
    }
}