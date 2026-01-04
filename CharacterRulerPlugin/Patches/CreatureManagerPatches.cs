using DataModel;
using HarmonyLib;
using static CreatureManager;

namespace CharacterRulerPlugin.Patches
{
    // Patches to handle creature drop ato update rulers as needed.
    [HarmonyPatch(typeof(CreatureManager), nameof(CreatureManager.OnOp), typeof(DropCreatureOp), typeof(MessageInfo))]
    public class CreatureManagerDropCreatureOpPatch
    {
        static void Postfix(DropCreatureOp op, MessageInfo msgInfo)
        {
            if (!LocalClient.IsInGmMode)
                return;

            CharacterRuler.CharacterRulerPlugin.MoveUpdate(op.CreatureId);
        }
    }

    [HarmonyPatch(typeof(CreatureManager), nameof(CreatureManager.OnOp), typeof(DropCreaturesOp), typeof(MessageInfo))]
    public class CreatureManagerDropCreaturesOpPatch
    {
        static void Postfix(DropCreaturesOp op, MessageInfo msgInfo)
        {
            if (!LocalClient.IsInGmMode)
                return;

            foreach (CreatureGuid creatureId in op.CreatureIds)
            {
                CharacterRuler.CharacterRulerPlugin.MoveUpdate(creatureId);
            }
        }
    }


    // Patches to handle creature deletion to clean up rulers as needed.
    [HarmonyPatch(typeof(CreatureManager), nameof(CreatureManager.OnOp), typeof(DeleteCreatureOp), typeof(MessageInfo))]
    public class CreatureManagerDeleteCreatureOpPatch
    {
        static void Postfix(DeleteCreatureOp op, MessageInfo msgInfo)
        {
            CharacterRuler.CharacterRulerPlugin.RemoveCreatureTracking(op.CreatureId);
        }
    }

    [HarmonyPatch(typeof(CreatureManager), nameof(CreatureManager.OnOp), typeof(DeleteCreaturesOp), typeof(MessageInfo))]
    public class CreatureManagerDeleteCreaturesOpPatch
    {
        static void Postfix(DeleteCreaturesOp op, MessageInfo msgInfo)
        {
            foreach (var creatureId in op.CreatureIds)
            {
                CharacterRuler.CharacterRulerPlugin.RemoveCreatureTracking(creatureId);
            }
        }
    }

}
