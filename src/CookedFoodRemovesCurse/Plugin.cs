using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

using Photon.Pun;



namespace CookedFoodRemovesCurse;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Log = Logger;



        //patch harmony functions
        _harmony = new Harmony(Id);
        _harmony.PatchAll();
        Log.LogInfo($"Plugin {Name} is loaded!");
    }
}

[HarmonyPatch(typeof(CharacterAfflictions), "SubtractStatus")]
public static class HungerSubtractPatch
{
    [HarmonyPostfix]
    public static void Postfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool fromRPC, bool decreasedNaturally)
    {
        if (statusType != CharacterAfflictions.STATUSTYPE.Hunger) return;
        if (fromRPC) return; // only react to the local, authoritative action, not a network replay

        Character character = __instance.character;
        Item currentItem = character?.data?.currentItem;
        if (currentItem == null) return;

        int cookedAmount = currentItem.GetData<IntItemData>(DataEntryKey.CookedAmount).Value;
        Plugin.Log.LogInfo($"Hunger reduced by {amount} via {currentItem.name} (cookedAmount={cookedAmount})");

        if (cookedAmount == 1 || cookedAmount == 2)
        {
            __instance.SubtractStatus(CharacterAfflictions.STATUSTYPE.Curse, .01f, false, false);
        }
        else if (cookedAmount == 3)
        {
            __instance.SubtractStatus(CharacterAfflictions.STATUSTYPE.Curse, .02f, false, false);
        }
    }
}
