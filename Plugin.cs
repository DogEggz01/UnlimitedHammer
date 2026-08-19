using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace UnlimitedHammer;

[BepInPlugin("DogEggz.unlimitedhammer", "Unlimited hammer", "1.0.1")]
public class Plugin : BaseUnityPlugin
{
    private Harmony harmony;

    internal static ConfigEntry<bool> ModEnabled { get; private set; }

    internal static bool IsModEnabled
    {
        get
        {
            if (ModEnabled != null)
                return ModEnabled.Value;

            return true;
        }
    }

    private void Awake()
    {
        ModEnabled = Config.Bind(
            "Settings",
            "Mod enabled",
            true,
            "Toggle all Unlimited hammer changes on or off.");

        harmony = new Harmony("DogEggz.unlimitedhammer");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }
}
