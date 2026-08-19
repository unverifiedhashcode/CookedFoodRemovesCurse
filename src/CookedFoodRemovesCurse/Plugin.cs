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

[HarmonyPatch(typeof(Item), "Consume")]
public static class ItemConsumePatch
{
    [HarmonyPostfix]
    public static void PostFix(Item __instance, int consumerID)
    {
        var modifyStatusComponents = __instance.GetComponents<Action_ModifyStatus>();
        bool isFood = false;

        foreach (var comp in modifyStatusComponents)
        {
            if (comp.statusType == CharacterAfflictions.STATUSTYPE.Hunger && comp.changeAmount < 0f)
            {
                isFood = true;
                break;
            }
        }

        PhotonView pv = PhotonNetwork.GetPhotonView(consumerID);
        if (pv == null) return;

        Character currCharacter = pv.GetComponent<Character>();
        if (currCharacter == null) return;

        // Always log full detail, regardless of isFood result
        string statusList = string.Join(", ", modifyStatusComponents.Select(c => $"{c.statusType}:{c.changeAmount}"));
        Plugin.Log.LogInfo(
            $"ItemConsume called. Consumed: {__instance.name} | tags={__instance.itemTags} | " +
            $"modifyStatusCount={modifyStatusComponents.Length} | statuses=[{statusList}]"
        );

        if (isFood)
        {
            Plugin.Log.LogInfo($"Consumed item {__instance.name} detected as FOOD.");
            int cookedAmount = __instance.GetData<IntItemData>(DataEntryKey.CookedAmount).Value;
            if ((cookedAmount == 1) || (cookedAmount == 2))
            {
                currCharacter.refs.afflictions.SubtractStatus(statusType: CharacterAfflictions.STATUSTYPE.Curse, amount: .01f, fromRPC: false, decreasedNaturally: false);
            }
            else if (cookedAmount == 3)
            {
                currCharacter.refs.afflictions.SubtractStatus(statusType: CharacterAfflictions.STATUSTYPE.Curse, amount: .02f, fromRPC: false, decreasedNaturally: false);
            }
        }
        else
        {
            Plugin.Log.LogInfo($"Consumed item {__instance.name} detected as NOT FOOD.");
        }
    }
}