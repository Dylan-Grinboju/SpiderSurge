using Silk;
using Logger = Silk.Logger;

namespace StatsMod
{

    public class EnemiesTracker
    {
        private static EnemiesTracker _instance;
        public static EnemiesTracker Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new EnemiesTracker();
                    Logger.LogInfo("Enemies tracker created via singleton access");
                }
                return _instance;
            }
        }

        public int EnemiesKilled { get; private set; }

        public EnemiesTracker()
        {
            EnemiesKilled = 0;
            Logger.LogInfo("Enemies tracker initialized");
        }

        public void IncrementEnemiesKilled()
        {
            EnemiesKilled++;
        }

        public void ResetEnemiesKilled()
        {
            EnemiesKilled = 0;
        }

    }

}
