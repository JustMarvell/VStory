using System.Collections.Generic;
using UnityEngine;
using VRGame.Core;

namespace VRGame.Combat
{
    public class EncounterManager : MonoBehaviour
    {
        [SerializeField] string encounterId;
        [SerializeField] List<EnemyController> enemies;

        int aliveCount;
        bool started;

        public void BeginEncounter()
        {
            if (started) return;
            started = true;

            aliveCount = enemies.Count;
            foreach (var enemy in enemies)
            {
                enemy.OnDeath += HandleEnemyDeath;
                enemy.Activate();
            }
        }

        void HandleEnemyDeath(EnemyController enemy)
        {
            enemy.OnDeath -= HandleEnemyDeath;
            aliveCount--;
            if (aliveCount <= 0) CompleteEncounter();
        }

        void CompleteEncounter()
        {
            QuestManager.SetFlag($"{encounterId}_cleared");
        }
    }
}