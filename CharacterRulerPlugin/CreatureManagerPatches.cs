using DataModel;
using HarmonyLib;
using static CreatureManager;

namespace CharacterRulerPlugin
{
    [HarmonyPatch(typeof(CreatureManager), nameof(CreatureManager.OnOp), typeof(DropCreatureOp), typeof(MessageInfo))]
    public class CreatureManagerDropCreatureOpPatche
    {
        static void Postfix(DropCreatureOp op, MessageInfo msgInfo)
        {
            if (!LocalClient.IsInGmMode)
                return;
            
            CharacterRuler.CharacterRulerPlugin.MoveUpdate(op.CreatureId);
        }
    }

    [HarmonyPatch(typeof(CreatureManager), nameof(CreatureManager.OnOp), typeof(DropCreaturesOp), typeof(MessageInfo))]
    public class CreatureManagerDropCreaturesOpPatche
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
}
