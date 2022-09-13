using OWML.Common;
using OWML.ModHelper;

namespace ModTemplate
{
    public class ModTemplate : ModBehaviour
    {
        public static ModTemplate Instance;
        
        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            // Starting here, you'll have access to OWML's mod helper.
            ModHelper.Console.WriteLine($"My mod {nameof(ModTemplate)} is loaded!", MessageType.Success);

            // Example of accessing game code.
            LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
            {
                if (loadScene != OWScene.SolarSystem) return;
                var playerBody = FindObjectOfType<PlayerBody>();
                ModHelper.Console.WriteLine($"Found player body, and it's called {playerBody.name}!",
                    MessageType.Success);
            };
        }
    }
}
