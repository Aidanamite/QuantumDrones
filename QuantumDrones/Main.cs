using HarmonyLib;
using SRML;
using SRML.Console;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using SRML.SR;
using SRML.Utils.Enum;
using SRML.SR.Translation;
using MonomiPark.SlimeRancher.Regions;

namespace QuantumDrones
{
    public class Main : ModEntryPoint
    {
        internal static Assembly modAssembly = Assembly.GetExecutingAssembly();
        internal static string modName = $"{modAssembly.GetName().Name}";
        internal static string modDir = $"{System.Environment.CurrentDirectory}\\SRML\\Mods\\{modName}";

        public override void PreLoad()
        {
            HarmonyInstance.PatchAll();
            TranslationPatcher.AddUITranslation("w.limit_reached_quantum_drone", "Drone limit reached for this Ranch Expansion. Limit: 4");
        }
        public override void Load()
        {
            var AdvancedDroneDefinition = SRSingleton<GameContext>.Instance.LookupDirector.GetGadgetDefinition(Gadget.Id.DRONE_ADVANCED);
            var ButterscotchTeleporterDefinition = SRSingleton<GameContext>.Instance.LookupDirector.GetGadgetDefinition(Gadget.Id.TELEPORTER_BUTTERSCOTCH);
            var NewDroneGadgetPrefab = AdvancedDroneDefinition.prefab.CreatePrefabCopy();
            NewDroneGadgetPrefab.GetComponent<DroneGadget>().id = Id.DRONE_QUANTUM;
            var NewDronePrefab = NewDroneGadgetPrefab.GetComponent<DroneGadget>().prefab.CreatePrefabCopy();
            NewDroneGadgetPrefab.GetComponent<DroneGadget>().prefab = NewDronePrefab;
            var OldDroneMovement = NewDronePrefab.GetComponent<DroneMovement>();
            var NewDroneMovement = NewDronePrefab.AddComponent<QuantumDroneMovement>();
            NewDronePrefab.GetComponent<Drone>().movement = NewDroneMovement;
            OldDroneMovement.CopyAllTo(NewDroneMovement);
            Object.DestroyImmediate(OldDroneMovement);
            NewDroneMovement.quantumTeleportFX = SRSingleton<GameContext>.Instance.LookupDirector.GetIdentifiable(Identifiable.Id.QUANTUM_SLIME).GetComponent<QuantumSlimeSuperposition>().SuperposeParticleFx;
            NewDroneMovement.collider = NewDronePrefab.GetComponent<Collider>();
            var cost = new List<GadgetDefinition.CraftCost>();
            foreach (var l in new GadgetDefinition.CraftCost[][] { AdvancedDroneDefinition.craftCosts, ButterscotchTeleporterDefinition.craftCosts })
                foreach (var c in l)
                    if (cost.Exists((x) => x.id == c.id))
                        cost.Find((x) => x.id == c.id).amount += c.amount * 2;
                    else
                        cost.Add(new GadgetDefinition.CraftCost() { id = c.id, amount = c.amount * 2 });
            var NewDroneGadgetDefinition = ScriptableObject.CreateInstance<GadgetDefinition>();
            NewDroneGadgetDefinition.blueprintCost = 10000;
            NewDroneGadgetDefinition.buyCountLimit = AdvancedDroneDefinition.buyCountLimit;
            NewDroneGadgetDefinition.buyInPairs = AdvancedDroneDefinition.buyInPairs;
            NewDroneGadgetDefinition.countLimit = AdvancedDroneDefinition.countLimit * 2;
            NewDroneGadgetDefinition.countOtherIds = new Gadget.Id[0];
            NewDroneGadgetDefinition.craftCosts = cost.ToArray();
            NewDroneGadgetDefinition.destroyOnRemoval = AdvancedDroneDefinition.destroyOnRemoval;
            NewDroneGadgetDefinition.icon = AdvancedDroneDefinition.icon;
            NewDroneGadgetDefinition.id = Id.DRONE_QUANTUM;
            NewDroneGadgetDefinition.pediaLink = AdvancedDroneDefinition.pediaLink;
            NewDroneGadgetDefinition.prefab = NewDroneGadgetPrefab;
            LookupRegistry.RegisterGadget(NewDroneGadgetDefinition);
            GadgetRegistry.RegisterBlueprintLock(Id.DRONE_QUANTUM, (GadgetDirector x) => x.CreateBasicLock(Id.DRONE_QUANTUM, Gadget.Id.DRONE_ADVANCED, 48));
            Id.DRONE_QUANTUM.GetTranslation().SetNameTranslation("Quantum Drone").SetDescriptionTranslation("A more advanced version of the Advanced Drone with built-in teleporter technology");
        }
    }
    [EnumHolder]
    public static class Id
    {
        [GadgetCategorization(GadgetCategorization.Rule.DRONE)]
        public static readonly Gadget.Id DRONE_QUANTUM;
    }

    public static class ExtentionMethods
    {
        public static T Find<T>(this T[] t, System.Predicate<T> predicate)
        {
            foreach (var i in t)
                if (predicate(i))
                    return i;
            return default(T);
        }
    }
    public class QuantumDroneMovement : DroneMovement
    {
        public GameObject quantumTeleportFX;
        public Collider collider;
        public void PathTowardsOverride(Vector3 position)
        {
            PlayTeleportFX();
            rigidbody.position = position;
            PlayTeleportFX();
        }
        void PlayTeleportFX()=>SRBehaviour.SpawnAndPlayFX(quantumTeleportFX, transform.position, transform.rotation);
    }

    [HarmonyPatch(typeof(DroneMovement), "PathTowards")]
    class Patch_DroneMovement_PathTowards
    {
        static bool Prefix(DroneMovement __instance, Vector3 position)
        {
            if (!(__instance is QuantumDroneMovement))
                return true;
            ((QuantumDroneMovement)__instance).PathTowardsOverride(position);
            return false;
        }
    }

    [HarmonyPatch(typeof(DroneAnimator), "SetAnimation")]
    class Patch_DroneAnimator_SetAnimation
    {
        static void Postfix(DroneAnimator __instance, DroneAnimator.Id id)
        {
            var m = __instance.parent.GetComponent<QuantumDroneMovement>();
            if (!m)
                return;
            m.collider.enabled = id == DroneAnimator.Id.REST;
        }
    }

    [HarmonyPatch(typeof(DroneFastForwarder), "FastForward_Deposit")]
    class Patch_DroneFastForwarder_FastForwardDep
    {
        static void Prefix(DroneFastForwarder.GatherGroup group)
        {
            Debug.Log($"trying to deposit {group.count} of {group.id} (from {group.GetType().FullName})");
        }
        static void Postfix(bool __result, DroneFastForwarder.GatherGroup group)
        {
            Debug.Log($"successful deposit: {__result}\nremaining: {group.count}");
        }
    }

    [HarmonyPatch(typeof(DroneFastForwarder), "FastForward")]
    class Patch_DroneFastForwarder_FastForward
    {
        static void Prefix(Drone drone, ref double startTime)
        {
            if (drone.GetComponent<QuantumDroneMovement>())
                startTime = double.NegativeInfinity;
        }
    }

    [HarmonyPatch(typeof(GadgetDirector), "GetPlacementError")]
    class Patch_GadgetDirector_GetPlacementError
    {
        public static bool replaced = false;
        static void Prefix(ref Gadget.Id gadget)
        {
            if (gadget != Id.DRONE_QUANTUM)
                return;
            gadget = Gadget.Id.DRONE_ADVANCED;
            replaced = true;
        }
        static void Postfix(GadgetDirector.PlacementError __result)
        {
            if (!replaced)
                return;
            if (__result != null && __result.message == "w.limit_reached_drone")
                __result.message = "w.limit_reached_quantum_drone";
            replaced = false;
        }
    }
    [HarmonyPatch(typeof(LookupDirector), "GetGadgetDefinition")]
    class Patch_LookupDirector_GetGadgetDefinition
    {
        static void Prefix(ref Gadget.Id id)
        {
            if (Patch_GadgetDirector_GetPlacementError.replaced)
                id = Id.DRONE_QUANTUM;
        }
    }
}