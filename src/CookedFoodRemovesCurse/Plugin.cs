using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

using Photon.Pun;



namespace CookedFoodRemovesCurse;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony _harmony = null!;

    //Configs
    public static ConfigEntry<float> cookedOnceRemoval;
    public static ConfigEntry<float> cookedTwiceRemoval;
    public static ConfigEntry<float> cookedThriceRemoval;
    public static ConfigEntry<bool> canOverEat;



    private void Awake()
    {
        Log = Logger;

        canOverEat = Config.Bind(
            "General",
            "Applies on over-eat",
            false,
            "Is curse removed if you eat food when you're already full?"

        );

        cookedOnceRemoval = Config.Bind(
            "Stat Tweaks",
            "cookedOnceRemoval",
            1.0f,
            "Curse removed for normal cooked food (float)"
        );

        cookedTwiceRemoval = Config.Bind(
            "Stat Tweaks",
            "cookedTwiceRemoval",
            1.0f,
            "Curse removed for double cooked food (float)"
        );

        cookedThriceRemoval = Config.Bind(
            "Stat Tweaks",
            "cookedThriceRemoval",
            1.0f,
            "Curse removed for burned food (float)"
        );




        //patch harmony functions
        _harmony = new Harmony(Id);
        _harmony.PatchAll();
        Log.LogInfo($"Plugin {Name} loaded, V1.3c");
    }
}

//track current hunger value
//needs to be prefix patched onto hunger tracking so that it checks BEFORE the item is consumed (which would immediately remove hunger)
[HarmonyPatch(typeof(CharacterAfflictions), "SubtractStatus")]
public static class HungerTrackerPatch
{
    public static float lastHungerBeforeSubtract = -1f;

    [HarmonyPrefix]
    public static void Prefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType)
    {
        if (statusType != CharacterAfflictions.STATUSTYPE.Hunger) return;
        lastHungerBeforeSubtract = __instance.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Hunger);
    }
}


[HarmonyPatch(typeof(Item), "Consume")]
public static class ItemConsumePatch
{
    [HarmonyPostfix]
    public static void Postfix(Item __instance, int consumerID)
    {
        //get item data
        float currentHunger = HungerTrackerPatch.lastHungerBeforeSubtract;
        bool isFood = __instance.GetComponents<Action_ModifyStatus>()
            .Any(c => c.statusType == CharacterAfflictions.STATUSTYPE.Hunger && c.changeAmount < 0f)
            || __instance.GetComponent<Action_RestoreHunger>() != null;
        int cookedAmount = __instance.GetData<IntItemData>(DataEntryKey.CookedAmount).Value;

        //char data
        PhotonView pv = PhotonNetwork.GetPhotonView(consumerID);
        Character currCharacter = pv?.GetComponent<Character>();
        if (currCharacter == null) {return;}

        //cooked bandaids should not count
        if (!isFood) 
        {
            Plugin.Log.LogInfo($"{__instance.name} detected as NOT FOOD.");
            return;
        }

        //actual food consumed
        Plugin.Log.LogInfo($"Consumed FOOD: {__instance.name} | cookedAmount={cookedAmount}");

        //overeat check
        if ( ! Plugin.canOverEat.Value && currentHunger < 0.025f)
        {
            Plugin.Log.LogInfo($"Player is not hungry (hunger = {currentHunger}) and canOverEat is FALSE. No curse removal applicable.");
            return;
        }
        
        //1 is normal, 2 then 3 is burned
        if (cookedAmount == 1) 
        {
            Plugin.Log.LogInfo($"Food cooked 1x. Decreasing curse by {Plugin.cookedOnceRemoval.Value}");
            currCharacter.refs.afflictions.SubtractStatus(CharacterAfflictions.STATUSTYPE.Curse, .025f * (Plugin.cookedOnceRemoval.Value), false, false);
        }
        else if (cookedAmount == 2)
        {
            Plugin.Log.LogInfo($"Food cooked 2x. Decreasing curse by {Plugin.cookedTwiceRemoval.Value}");
            currCharacter.refs.afflictions.SubtractStatus(CharacterAfflictions.STATUSTYPE.Curse, .025f * (Plugin.cookedTwiceRemoval.Value), false, false);
        }
        else if (cookedAmount == 3) 
        {
            Plugin.Log.LogInfo($"Food cooked 3x. Decreasing curse by {Plugin.cookedThriceRemoval.Value}");
            currCharacter.refs.afflictions.SubtractStatus(CharacterAfflictions.STATUSTYPE.Curse, .025f * (Plugin.cookedThriceRemoval.Value), false, false);
        }
        
    }
}