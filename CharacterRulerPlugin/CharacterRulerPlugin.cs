using BepInEx;
using Bounce.Unmanaged;
using HarmonyLib;
using PluginUtilities;
using RadialUI;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CharacterRuler
{
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInDependency(SetInjectionFlag.Guid)]
    [BepInDependency(RadialUIPlugin.Guid)]
    public class CharacterRulerPlugin : DependencyUnityPlugin<CharacterRulerPlugin>
    {
        // constants
        public const string PluginName = "Character Ruler Plugin";
        public const string Guid = "org.hollofox.plugins.CharacterRulerPlugin";
        public const string Version = "0.0.0.0";

        // static datastructure to track rulers
        internal static DictionaryList<Ruler, List<CreatureGuid>> Rulers = new DictionaryList<Ruler, List<CreatureGuid>>();
        internal static DictionaryList<CreatureGuid, List<Ruler>> ActiveRulers = new DictionaryList<CreatureGuid, List<Ruler>>();

        private Harmony harmony;
        
        /// <summary>
        /// Awake plugin
        /// </summary>
        protected override void OnAwake()
        {
            Logger.LogDebug("Character Ruler loaded");

            harmony = new Harmony(Guid);
            harmony.PatchAll();

            RadialUIPlugin.AddCustomButtonAttacksSubmenu($"{Guid}.AddRuler", new MapMenu.ItemArgs
            {
                Title = "Measure Distance",
                Action = CreateLineRuler,
                CloseMenuOnActivate = true,
            }, NoRulerBetween);

            RadialUIPlugin.AddCustomButtonAttacksSubmenu($"{Guid}.RemoveRuler", new MapMenu.ItemArgs
            {
                Title = "Remove Ruler",
                Action = RemoveLineRuler,
                CloseMenuOnActivate = true,
            }, HasRulerBetween);
        }

        /// <summary>
        /// Cleanup on destroy
        /// </summary>
        protected override void OnDestroyed()
        {
            Logger.LogDebug("Unloading Character Ruler");

            // Dispose all active rulers
            Ruler[] rulers = Rulers.Keys.ToArray();
            foreach (Ruler ruler in rulers)
            {
                ruler.Dispose();
            }

            // Unregister Radial UI buttons
            RadialUIPlugin.RemoveCustomButtonAttacksSubmenu($"{Guid}.AddRuler");
            RadialUIPlugin.RemoveCustomButtonAttacksSubmenu($"{Guid}.RemoveRuler");

            // Unregister LOS update handler
            CreaturePerceptionManager.OnLineOfSightUpdated -= LOSUpdate;

            // Unpatch Harmony patches
            harmony?.UnpatchSelf();

            Logger.LogDebug("Character Ruler unloaded");
        }

        // Removes a line ruler between selected creature(s) and target creature
        private void RemoveLineRuler(MapMenuItem arg1, object arg2)
        {
            if (LocalClient.SelectedCreatureId == null && !LocalClient.HasLassoedCreatures)
                return;

            CreatureGuid[] selectedAssets;
            if (LocalClient.HasLassoedCreatures)
            {
                LocalClient.TryGetLassoedCreatureIds(out selectedAssets);
            }
            else
            {
                selectedAssets = new[] { LocalClient.SelectedCreatureId };
            }

            CreatureGuid targetId = new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature());

            foreach (CreatureGuid selectedAssetId in selectedAssets) {
                // Find all rulers involving both creatures
                Ruler[] rulers = ActiveRulers[selectedAssetId].Where(r => ActiveRulers[targetId].Contains(r)).ToArray();

                foreach (Ruler ruler in rulers)
                {
                    ruler.Dispose();
                }
            }
        }

        // Creates a line ruler between selected creature(s) and target creature
        private void CreateLineRuler(MapMenuItem arg1, object arg2)
        {
            if (LocalClient.SelectedCreatureId == null && !LocalClient.HasLassoedCreatures)
                return;

            // Register for LOS updates
            CreaturePerceptionManager.OnLineOfSightUpdated -= LOSUpdate;
            CreaturePerceptionManager.OnLineOfSightUpdated += LOSUpdate;

            CreatureGuid[] assets;
            if (LocalClient.HasLassoedCreatures)
            {
                LocalClient.TryGetLassoedCreatureIds(out assets);
            }
            else
            {
                assets = new  [] { LocalClient.SelectedCreatureId};
            }

            CreaturePresenter.TryGetAsset(new CreatureGuid(RadialUIPlugin.GetLastRadialTargetCreature()), out CreatureBoardAsset asset2);
            CreatureGuid targetId = new CreatureGuid(RadialUIPlugin.GetLastRadialTargetCreature());
            foreach (CreatureGuid assetId in assets)
            {
                CreaturePresenter.TryGetAsset(assetId, out CreatureBoardAsset asset);

                float3[] pos = new float3[]{
                    asset.LastPlacedPosition,
                    asset2.LastPlacedPosition,
                };
                NativeArray<float3> npos = new NativeArray<float3>(pos, Allocator.TempJob);
                Ruler newRuler = Ruler.Spawn("Rulers/PhotonRuler", 1, npos.AsReadOnly());
                npos.Dispose();

                if (ActiveRulers.ContainsKey(assetId))
                    ActiveRulers[assetId].Add(newRuler);
                else
                    ActiveRulers[assetId] = new List<Ruler>() { newRuler };

                if (ActiveRulers.ContainsKey(targetId))
                    ActiveRulers[targetId].Add(newRuler);
                else
                    ActiveRulers[targetId] = new List<Ruler>() { newRuler };

                Rulers[newRuler] = new List<CreatureGuid>() {
                    targetId,
                    assetId
                };
            }
        }

        // Checks if there is NO active ruler between two creatures
        internal static bool NoRulerBetween(NGuid first, NGuid second) => !HasRulerBetween(first, second);

        // Checks if there is an active ruler between two creatures
        internal static bool HasRulerBetween(NGuid first, NGuid second)
        {
            CreatureGuid a = new CreatureGuid(first);
            CreatureGuid b = new CreatureGuid(second);

            // If either creature has no active rulers, there can't be one between them
            if (!ActiveRulers.TryGetValue(a, out List<Ruler> rulersForA))
                return false;

            // Check only rulers that already involve creature A
            foreach (Ruler ruler in rulersForA)
            {
                if (!Rulers.TryGetValue(ruler, out List<CreatureGuid> creatures))
                    continue;

                // A ruler between exactly means it contains both endpoints
                if (creatures.Contains(b))
                    return true;
            }

            return false;
        }

        // Handles move updates for all active rulers
        internal static void MoveUpdate(CreatureGuid id)
        {
            if (!ActiveRulers.TryGetValue(id, out List<Ruler> rulersForCreature))
                return;

            // Phase 1: evaluation (NO side effects)
            Dictionary<Ruler, List<CreatureGuid>> rulersToUpdate = new Dictionary<Ruler, List<CreatureGuid>>();

            foreach (Ruler ruler in rulersForCreature)
            {
                List<CreatureGuid> targets = Rulers[ruler];
                rulersToUpdate.Add(ruler, targets);
            }

            // Phase 2: update rulers (replace + dispose originals)
            UpdateRulers(id, rulersToUpdate);
        }

        // Handles LOS updates for all active rulers
        internal static void LOSUpdate(CreatureGuid id, LineOfSightManager.LineOfSightResult result)
        {
            // We use the GM mode bypass to avoid LOS checks for GMs
            if (LocalClient.IsInGmMode)
                return;

            if (!ActiveRulers.TryGetValue(id, out List<Ruler> rulersForCreature))
                return;

            // Phase 1: evaluation (NO side effects)
            HashSet<Ruler> rulersToRemove = new HashSet<Ruler>();
            Dictionary<Ruler, List<CreatureGuid>> rulersToUpdate = new Dictionary<Ruler, List<CreatureGuid>>();

            foreach (Ruler ruler in rulersForCreature)
            {
                List<CreatureGuid> targets = Rulers[ruler];
                bool losBroken = false;

                foreach (CreatureGuid target in targets)
                {
                    if (target == id)
                        continue;

                    if (!CreaturePresenter.TryGetAsset(target, out CreatureBoardAsset asset) || !result.HasLineOfSightTo(asset))
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
            foreach (Ruler ruler in rulersToRemove)
            {
                ruler.Dispose();
            }

            // Phase 3: update rulers (replace + dispose originals)
            UpdateRulers(id, rulersToUpdate);
        }

        internal static void UpdateRulers(CreatureGuid id, Dictionary<Ruler, List<CreatureGuid>> rulersToUpdate)
        {
            foreach (KeyValuePair<Ruler, List<CreatureGuid>> kvp in rulersToUpdate)
            {
                Ruler oldRuler = kvp.Key;
                List<CreatureGuid> creatures = kvp.Value;

                Debug.Log($"Updating ruler for {id} assets moved");

                float3[] positions = creatures
                    .Select(g =>
                    {
                        CreaturePresenter.TryGetAsset(g, out CreatureBoardAsset ca);
                        return ca.LastPlacedPosition;
                    })
                    .ToArray();

                NativeArray<float3> nativePos = new NativeArray<float3>(positions, Allocator.TempJob);
                Ruler newRuler = Ruler.Spawn(
                    "Rulers/PhotonRuler",
                    oldRuler.CurrentModeIndex,
                    nativePos.AsReadOnly()
                );
                nativePos.Dispose();

                // Add new ruler to tracking
                Rulers[newRuler] = creatures;
                foreach (CreatureGuid creature in creatures)
                {
                    if (!ActiveRulers.TryGetValue(creature, out List<Ruler> list))
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

        // Removes a ruler from all tracking dictionaries
        internal static void RemoveRulerTracking(Ruler ruler)
        {
            if (!Rulers.TryGetValue(ruler, out List<CreatureGuid> creatures))
                return;

            Rulers.Remove(ruler);

            foreach (CreatureGuid creature in creatures)
            {
                if (!ActiveRulers.TryGetValue(creature, out List<Ruler> list))
                    continue;

                list.Remove(ruler);

                if (list.Count == 0)
                    ActiveRulers.Remove(creature);
            }
        }

        // Removes all rulers associated with a creature
        internal static void RemoveCreatureTracking(CreatureGuid id)
        {
            if (!ActiveRulers.ContainsKey(id))
                return;
            Ruler[] rulers = ActiveRulers[id].ToArray();

            foreach (Ruler ruler in rulers)
            {
                ruler.Dispose();
            }
        }

    }
}
