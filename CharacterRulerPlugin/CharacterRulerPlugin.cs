using BepInEx;
using Bounce.Unmanaged;
using GLTFast.Schema;
using HarmonyLib;
using ModdingTales;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CharacterRuler
{
    [BepInPlugin(Guid, "Character Ruler Plugin", Version)]
    [BepInDependency(RadialUI.RadialUIPlugin.Guid)]
    public class CharacterRulerPlugin : BaseUnityPlugin
    {
        // constants
        public const string Guid = "org.hollofox.plugins.CharacterRulerPlugin";
        public const string Version = "0.0.0.0";

        /// <summary>
        /// Awake plugin
        /// </summary>
        void Awake()
        {
            Debug.Log("Character Ruler loaded");

            ModdingUtils.AddPluginToMenuList(this);
            var harmony = new Harmony(Guid);
            harmony.PatchAll();

            RadialUI.RadialUIPlugin.AddCustomButtonAttacksSubmenu($"{Guid}.AddRuler", new MapMenu.ItemArgs
            {
                Title = "Measure Distance",
                Action = CreateLineRuler,
                CloseMenuOnActivate = true,
            }, NoRulerBetween);

            RadialUI.RadialUIPlugin.AddCustomButtonAttacksSubmenu($"{Guid}.RemoveRuler", new MapMenu.ItemArgs
            {
                Title = "Remove Ruler",
                Action = RemoveLineRuler,
                CloseMenuOnActivate = true,
            }, HasRulerBetween);
        }

        private void RemoveLineRuler(MapMenuItem arg1, object arg2)
        {
            var a = LocalClient.SelectedCreatureId;
            var b = new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature());

            // Find all rulers involving both creatures
            var rulers = ActiveRulers[a].Where(r => ActiveRulers[b].Contains(r)).ToArray();
            
            foreach (var ruler in rulers)
            {
                RemoveRulerTracking(ruler);
                ruler.Dispose();
            }
        }

        private void CreateLineRuler(MapMenuItem arg1, object arg2)
        {
            CreaturePerceptionManager.OnLineOfSightUpdated -= LOSUpdate;
            CreaturePerceptionManager.OnLineOfSightUpdated += LOSUpdate;

            if (LocalClient.SelectedCreatureId == null)
                return;

            CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out var asset);
            CreaturePresenter.TryGetAsset(new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), out var asset2);

            var pos = new Unity.Mathematics.float3[]{
                asset.LastPlacedPosition,
                asset2.LastPlacedPosition,
                };
            var npos = new NativeArray<Unity.Mathematics.float3>(pos, Allocator.TempJob);
            var r = Ruler.Spawn("Rulers/PhotonRuler", 1, npos.AsReadOnly());
            npos.Dispose();

            if (ActiveRulers.ContainsKey(LocalClient.SelectedCreatureId))
                ActiveRulers[LocalClient.SelectedCreatureId].Add(r);
            else
                ActiveRulers[LocalClient.SelectedCreatureId] = new List<Ruler>() { r };

            if (ActiveRulers.ContainsKey(new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature())))
                ActiveRulers[new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature())].Add(r);
            else
                ActiveRulers[new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature())] = new List<Ruler>() { r };

            Rulers[r] = new List<CreatureGuid>() { 
                new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()),
                LocalClient.SelectedCreatureId
            };
        }

        internal static DictionaryList<Ruler, List<CreatureGuid>> Rulers = new DictionaryList<Ruler, List<CreatureGuid>>();
        internal static DictionaryList<CreatureGuid, List<Ruler>> ActiveRulers = new DictionaryList<CreatureGuid, List<Ruler>>();

        internal static bool NoRulerBetween(NGuid first, NGuid second) => !HasRulerBetween(first, second);
        
        internal static bool HasRulerBetween(NGuid first, NGuid second)
        {
            var a = new CreatureGuid(first);
            var b = new CreatureGuid(second);

            // If either creature has no active rulers, there can't be one between them
            if (!ActiveRulers.TryGetValue(a, out var rulersForA))
                return false;

            // Check only rulers that already involve creature A
            foreach (var ruler in rulersForA)
            {
                if (!Rulers.TryGetValue(ruler, out var creatures))
                    continue;

                // A ruler between exactly means it contains both endpoints
                if (creatures.Contains(b))
                    return true;
            }

            return false;
        }


        internal static void LOSUpdate(CreatureGuid id, LineOfSightManager.LineOfSightResult result)
        {
            if (!ActiveRulers.TryGetValue(id, out var rulersForCreature))
                return;

            // Phase 1: evaluation (NO side effects)
            var rulersToRemove = new HashSet<Ruler>();
            var rulersToUpdate = new Dictionary<Ruler, List<CreatureGuid>>();

            foreach (var ruler in rulersForCreature)
            {
                var targets = Rulers[ruler];
                bool losBroken = false;

                foreach (var target in targets)
                {
                    if (target == id)
                        continue;

                    if (!CreaturePresenter.TryGetAsset(target, out var asset) || (!result.HasLineOfSightTo(asset) && !LocalClient.IsInGmMode))
                    {
                        losBroken = true;
                        break;
                    }
                }

                if (losBroken)
                {
                    rulersToRemove.Add(ruler);
                }
                else
                {
                    rulersToUpdate.Add(ruler, targets);
                }
            }

            // Phase 2: remove rulers with broken LOS
            foreach (var ruler in rulersToRemove)
            {
                RemoveRulerTracking(ruler);
                ruler.Dispose();
            }

            // Phase 3: update rulers (replace + dispose originals)
            foreach (var kvp in rulersToUpdate)
            {
                var oldRuler = kvp.Key;
                var creatures = kvp.Value;

                Debug.Log($"Updating ruler for {id} assets moved");

                var positions = creatures
                    .Select(g =>
                    {
                        CreaturePresenter.TryGetAsset(g, out var ca);
                        return ca.LastPlacedPosition;
                    })
                    .ToArray();

                var nativePos = new NativeArray<float3>(positions, Allocator.TempJob);
                var newRuler = Ruler.Spawn(
                    "Rulers/PhotonRuler",
                    oldRuler.CurrentModeIndex,
                    nativePos.AsReadOnly()
                );
                nativePos.Dispose();

                // Remove old ruler from tracking BEFORE disposing
                RemoveRulerTracking(oldRuler);

                // Add new ruler to tracking
                Rulers[newRuler] = creatures;
                foreach (var creature in creatures)
                {
                    if (!ActiveRulers.TryGetValue(creature, out var list))
                    {
                        list = new List<Ruler>();
                        ActiveRulers[creature] = list;
                    }
                    list.Add(newRuler);
                }

                // Dispose original ruler AFTER replacement is registered
                oldRuler.Dispose();
            }
        }


        private static void RemoveRulerTracking(Ruler ruler)
        {
            if (!Rulers.TryGetValue(ruler, out var creatures))
                return;

            Rulers.Remove(ruler);

            foreach (var creature in creatures)
            {
                if (!ActiveRulers.TryGetValue(creature, out var list))
                    continue;

                list.Remove(ruler);

                if (list.Count == 0)
                    ActiveRulers.Remove(creature);
            }
        }

    }
}
