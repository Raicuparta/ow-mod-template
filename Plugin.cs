using OWML.ModHelper;

using System.Reflection;

namespace owmlPlugin
{
    public class Plugin : ModBehaviour
    {
        public void Start()
        {
            // Plugin startup logic
            ModHelper.Console.WriteLine($"{Assembly.GetExecutingAssembly().GetName().Name} loaded");
        }
    }
}
