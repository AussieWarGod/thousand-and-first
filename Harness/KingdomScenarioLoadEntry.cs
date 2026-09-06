using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ConsoleLib.Console;
using HarmonyLib;
using Qud.API;
using XRL;
using XRL.Core;
using XRL.Serialization;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomScenarioLoadEntry
	{
		private static readonly KingdomScenarioLoadBarrier<XRLGame> Barrier = new KingdomScenarioLoadBarrier<XRLGame>();
		internal static KingdomScenarioLoadRequest Request;
		internal static KingdomScenarioSaveSnapshot Snapshot;
		internal static KingdomSubsidenceRungSaveSnapshot RungSnapshot;
		internal static KingdomQuickstartSaveSnapshot QuickstartSnapshot;
		internal static bool Armed;
		internal static string SnapshotWire;

		// Presence claims the menu path even when malformed: never fall back to a new game.
		internal static bool TryStart()
		{
			if (!KingdomScenarioSaveFiles.LoadPresent()) return false;
			if (!Barrier.TryClaim()) return true;
			try
			{
				Check(The.Game == null && SavesAPI.HasSavedGameInfo(), "sealed import is unavailable to the Continue dispatcher");
				foreach (MethodBase target in KingdomScenarioLoadMenuPatch.Targets())
					Check(KingdomScenarioLoadMenuPatch.Installed(target), "owned Continue interception is unavailable");
				Keyboard.PushMouseEvent("Pick:Continue");
			}
			catch (Exception error) { Refuse(error); }
			return true;
		}

		internal static bool Dispatch(ref Task<XRLGame> Result)
		{
			try
			{
				if (!Barrier.Claimed && !KingdomScenarioSaveFiles.LoadPresent()) return true;
				Result = Barrier.Pending;
				Check(Barrier.Claimed && Thread.CurrentThread == XRLCore.CoreThread,
					"sealed load must enter through the owned core-thread Continue dispatcher");
				Result = Barrier.Start(Load);
			}
			catch (Exception error) { Result = Barrier.Pending; Refuse(error); }
			return false;
		}

		private static void Refuse(Exception Error)
		{
			KingdomScenarioJournal.Append("SCRIPT-STOPPED", false,
				"native-load refused; evidence retained; " + KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message));
		}

		private static async Task Load()
		{
			bool priorPopup = Popup.Suppress;
			bool quickstartVerified = false;
			try
			{
				await The.UiContext;
				string root = KingdomScenarioSaveFiles.Root();
				Check(The.Game == null, "load profile already contains a live game");
				string local = Path.Combine(root, "Local");
				string text = KingdomScenarioSaveFiles.ReadText(Path.Combine(local, KingdomScenarioLoadRules.FileName),
					KingdomScenarioLoadRules.MaxChars);
				Check(KingdomScenarioLoadRules.TryParse(text, out Request), "sealed load request is malformed");
				SnapshotWire = KingdomScenarioSaveFiles.ReadText(Path.Combine(local,
					KingdomScenarioSaveFiles.LoadedSnapshotFile), Math.Max(KingdomScenarioSaveSnapshotCodec.MaxWireChars,
						Math.Max(KingdomSubsidenceRungSaveSnapshotCodec.MaxWireChars,
							KingdomQuickstartSaveSnapshotCodec.MaxWireChars)));
				Check(KingdomScenarioSaveFiles.HashText(SnapshotWire) == Request.SnapshotSha256,
					"sealed snapshot hash differs");
				if (SnapshotWire.StartsWith(KingdomQuickstartSaveSnapshotCodec.Prefix, StringComparison.Ordinal))
					Check(KingdomQuickstartSaveSnapshotCodec.TryDecode(SnapshotWire, out QuickstartSnapshot)
						&& QuickstartSnapshot.GameId == Request.GameId, "sealed Quickstart snapshot does not bind selected save");
				else if (KingdomSubsidenceRungSaveSnapshotCodec.MatchesPrefix(SnapshotWire))
					Check(KingdomSubsidenceRungSaveSnapshotCodec.MatchesCurrentPrefix(SnapshotWire)
						&& KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(SnapshotWire, out RungSnapshot)
						&& RungSnapshot.GameId == Request.GameId, "sealed rung snapshot does not bind the selected save");
				else
					Check(KingdomScenarioSaveSnapshotCodec.TryDecode(SnapshotWire, out Snapshot)
						&& Snapshot.GameId == Request.GameId, "sealed snapshot does not bind the selected save");
				string save = KingdomScenarioSaveFiles.SaveDirectory(root, Request.GameId);
				string[] files = Directory.GetFiles(save);
				Check(files.Length == 3 && Directory.GetDirectories(save).Length == 0,
					"load directory must contain only primary, metadata and cache; no backup fallback");
				Check(KingdomScenarioSaveFiles.HashFile(Path.Combine(save, "Primary.sav.gz"),
					KingdomScenarioSaveFiles.MaxSaveBytes) == Request.PrimarySha256
					&& KingdomScenarioSaveFiles.HashFile(Path.Combine(save, "Primary.json"), 1048576) == Request.InfoSha256
					&& KingdomScenarioSaveFiles.HashFile(Path.Combine(save, "Cache.db"),
						KingdomScenarioSaveFiles.MaxSaveBytes) == Request.CacheSha256, "imported save hashes differ");
				Check(KingdomScenarioJournal.Append("LOAD-BEGIN", true,
					"exact sealed save; game-id=" + Request.GameId + "; new-game=false; mod-restore=false") == null,
					"load intent could not be journalled");
				Popup.Suppress = true;
				Armed = true;
				// Same worker-load route as vanilla SaveGameInfo, without mod restoration or Continue selection.
				XRLGame loaded = await Task.Run(() => XRLGame.LoadGame(Path.Combine(save, "Primary"),
					Session: false, ShowPopup: false));
				Check(loaded != null && ReferenceEquals(The.Game, loaded) && loaded.GameID == Request.GameId,
					"loader did not return the exact selected game");
				if (QuickstartSnapshot != null)
				{
					KingdomQuickstartLoadTest.VerifyLoaded(loaded);
					quickstartVerified = true;
					return;
				}
				string route = RungSnapshot == null ? KingdomScenarioLoadWitness.VerifyRecovered(loaded, Snapshot)
					: KingdomSubsidenceRungLoadWitness.VerifyRecovered(loaded, RungSnapshot);
				Check(!KingdomScenarioLoadReaderWitness.HadErrors, "engine reported deserialization errors");
				Check(KingdomScenarioJournal.Append("SCRIPT-COMPLETE", true,
					"native-load cases=2 passed=2 failed=0; real-save-quit-load=true; new-game-script-replayed=false"
					+ "; recovery=" + route + "; ordinary-acceptance=false; profiles-and-effects-retained=true") == null,
					"load completion could not be journalled");
			}
			catch (Exception error) { Refuse(error); }
			finally
			{
				if (RungSnapshot != null) KingdomSubsidenceRungReleaseCut.Disarm();
				if (!priorPopup && Popup.Suppress) Popup.Suppress = false;
				try { if (QuickstartSnapshot != null) KingdomQuickstartLoadTest.Finish(quickstartVerified); }
				finally { Armed = false; }
				// The Continue task stays parked until the bounded owned runner stops this terminal fixture.
			}
		}

		private static void Check(bool Condition, string Failure)
		{
			KingdomScenarioSaveFiles.Require(Condition, Failure);
		}
	}

	[HarmonyPatch]
	internal static class KingdomScenarioLoadMenuPatch
	{
		[HarmonyTargetMethods]
		internal static IEnumerable<MethodBase> Targets()
		{
			yield return AccessTools.Method(typeof(Qud.UI.SaveManagement), "ContinueMenu", Type.EmptyTypes);
			yield return AccessTools.Method(typeof(XRLCore), "SaveManagement", Type.EmptyTypes);
		}

		internal static bool Installed(MethodBase Target)
		{
			MethodInfo method = Target as MethodInfo;
			if (method == null || method.ReturnType != typeof(Task<XRLGame>)) return false;
			Patches patches = Harmony.GetPatchInfo(Target);
			if (patches == null) return false;
			MethodInfo expected = AccessTools.Method(typeof(KingdomScenarioLoadMenuPatch), "Prefix");
			foreach (Patch patch in patches.Prefixes) if (patch.PatchMethod == expected) return true;
			return false;
		}

		[HarmonyPrefix]
		internal static bool Prefix(ref Task<XRLGame> __result)
		{
			return KingdomScenarioLoadEntry.Dispatch(ref __result);
		}
	}

	[HarmonyPatch(typeof(SerializationReader), "Clear")]
	internal static class KingdomScenarioLoadReaderWitness
	{
		internal static int Releases;
		internal static bool HadErrors;

		// Observe the real shared reader before ReleaseShared erases its error count.
		[HarmonyPrefix]
		internal static void Prefix(SerializationReader __instance)
		{
			if (!KingdomScenarioLoadEntry.Armed || !ReferenceEquals(__instance.Cache, FastSerialization.SharedCache)) return;
			Releases++;
			HadErrors |= __instance.Errors != 0;
		}
	}
}
