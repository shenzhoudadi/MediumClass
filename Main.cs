using BlueprintCore.Blueprints.Configurators.Root;
using BlueprintCore.Utils;
using HarmonyLib;
using System;
using System.Diagnostics;
using UnityEngine;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager.ModEntry;
using Kingmaker.PubSubSystem;
using MediumClass.Utils;
using MediumClass.Features;
using MediumClass.Features.MediumSpecific;

namespace MediumClass
{
    public static class Main
    {
        public static bool Enabled;
        public static bool InitializationFailed { get; private set; }
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(Main));
        //        private static readonly LogWrapper Logger = LogWrapper.Get("AddedFeats");

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Harmony harmony = null;
            var handler = new BlueprintCacheInitHandler();
            try
            {
                harmony = new Harmony(modEntry.Info.Id);
                // Blueprint mutations cannot be safely undone during a running game.
                modEntry.OnGUI = _ => GUILayout.Label(InitializationFailed
                    ? "Medium Class initialization FAILED. Read the first MediumClass exception, then restart. Do not save."
                    : "Maintenance build: game and save compatibility are NOT YET TESTED. Restart after changing mods.");
                harmony.PatchAll();

                EventBus.Subscribe(handler);

                Logger.Log("Finished patching.");
                return true;
            }
            catch (Exception e)
            {
                InitializationFailed = true;
                Logger.LogException("Failed to patch", e);
                // Never remove another mod's Harmony patches.
                try
                {
                    EventBus.Unsubscribe(handler);
                    harmony?.UnpatchAll(modEntry.Info.Id);
                }
                catch (Exception cleanupError)
                {
                    Logger.LogException("Failed to clean up this mod's partial installation", cleanupError);
                }
                return false;
            }
        }

        class BlueprintCacheInitHandler : IBlueprintCacheInitHandler
        {
            private enum InitState { NotStarted, Running, Succeeded, Failed }
            private static InitState initialState;
            private static InitState delayedState;

            private static void RunStage(string name, Action action)
            {
                var timer = Stopwatch.StartNew();
                Logger.Log($"BEGIN {name}");
                try
                {
                    action();
                    Logger.Log($"END {name} ({timer.ElapsedMilliseconds} ms)");
                }
                catch (Exception e)
                {
                    Logger.LogException($"FAILED {name} ({timer.ElapsedMilliseconds} ms)", e);
                    throw;
                }
            }

            public void AfterBlueprintCachePatches()
            {
                try
                {
                    if (initialState != InitState.Succeeded || delayedState != InitState.NotStarted)
                    {
                        Logger.Log($"Skipping delayed initialization: initial={initialState}, delayed={delayedState}.");
                        return;
                    }
                    delayedState = InitState.Running;

                    RunStage("delayed feats", ConfigureFeatsDelayed);

                    RunStage("delayed blueprints", () => RootConfigurator.ConfigureDelayedBlueprints());
                    delayedState = InitState.Succeeded;
                }
                catch (Exception e)
                {
                    delayedState = InitState.Failed;
                    InitializationFailed = true;
                    Logger.LogException("Delayed blueprint configuration failed.", e);
                }
            }

            public void BeforeBlueprintCachePatches()
            {

            }

            public void BeforeBlueprintCacheInit()
            {

            }

            public void AfterBlueprintCacheInit()
            {
                try
                {
                    if (initialState != InitState.NotStarted)
                    {
                        Logger.Log($"Skipping blueprint initialization: {initialState}. Failed initialization requires restart.");
                        return;
                    }
                    initialState = InitState.Running;
                    LogWrapper.EnableInternalVerboseLogs();
                    // First strings
                    RunStage("localization", () => LocalizationTool.LoadEmbeddedLocalizationPacks(
                      "MediumClass.Strings.Archmage.json",
                      "MediumClass.Strings.Champion.json",
                      "MediumClass.Strings.Guardian.json",
                      "MediumClass.Strings.Hierophant.json",
                      "MediumClass.Strings.Marshal.json",
                      "MediumClass.Strings.Medium.json",
                      "MediumClass.Strings.Settings.json",
                      "MediumClass.Strings.Feats.json",
                      "MediumClass.Strings.Trickster.json"));

                    // Then settings

                    RunStage("settings", Settings.Init);
                    RunStage("classes", ConfigureClasses);
                    RunStage("homebrew", ConfigureHomebrew);
                    RunStage("feats", ConfigureFeats);
                    RunStage("spells", ConfigureSpells);
                    initialState = InitState.Succeeded;
                }
                catch (Exception e)
                {
                    initialState = InitState.Failed;
                    InitializationFailed = true;
                    Logger.LogException("Failed to initialize.", e);
                }
            }
            private static void ConfigureHomebrew()
            {
                Logger.Log("Configuring homebrew.");
            }
            private static void ConfigureClasses()
            {
                Logger.Log("Configuring Classes.");
                Medium.MediumClass.ConfigureEnabled();
            }
            private static void ConfigureClassFeats()
            {
                Logger.Log("Configuring class features.");
            }
            private static void ConfigureFeats()
            {   
                Logger.Log("Configuring features.");
                //Feint.Feint.ConfigureEnabled();
                DesnaDivineFightingTechnique.ConfigureEnabled();
                BackgroundMedium.ConfigureEnabled();
            }
            private static void ConfigureSpells()
            {
                Logger.Log("Configuring spells.");
            }
            private static void ConfigureFeatsDelayed()
            {
                Logger.Log("Configuring delayed.");
            }
        }
    }
}
