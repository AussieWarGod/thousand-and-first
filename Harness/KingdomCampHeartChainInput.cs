using System;
using System.Linq;
using System.Threading;
using HarmonyLib;
using XRL;

namespace ThousandAndFirst
{
	public sealed partial class KingdomScenarioAutoRunner
	{
		[NonSerialized] private volatile bool ChainInputOwned;
		[NonSerialized] private long ChainInputSkipped;
		[NonSerialized] private long ChainInputLeaked;

		private void ArmChainInput()
		{
			ChainInputOwned = Harness.KingdomScenarioScript.TryRead(out var script, out _)
				&& Harness.KingdomCampHeartChainScript.Matches(script);
		}

		internal static KingdomScenarioAutoRunner ChainInputOwner()
		{
			var runner = The.Game?.GetSystem<KingdomScenarioAutoRunner>();
			return runner?.ChainInputOwned == true ? runner : null;
		}

		internal void ObserveChainInput(bool OriginalRan)
		{
			if (OriginalRan) Interlocked.Increment(ref ChainInputLeaked);
			else Interlocked.Increment(ref ChainInputSkipped);
		}

		internal void RearmLoadedChainInput(XRLGame Game)
		{
			Harness.KingdomCampHeartNativeProvider.Require(!ChainInputOwned && HasConsideredScript
				&& ReferenceEquals(The.Game, Game) && ReferenceEquals(Game.GetSystem<KingdomScenarioAutoRunner>(), this)
				&& Harness.KingdomScenarioLoadEntry.Armed && Harness.KingdomScenarioLoadEntry.ChainSnapshot?.GameId == Game.GameID
				&& Harness.KingdomScenarioScript.TryRead(out var script, out _)
				&& Harness.KingdomCampHeartChainScript.Matches(script, true), "loaded higher-heart input ownership is not exact");
			ChainInputOwned = true;
			if (VerifyChainInput(true)) return;
			ChainInputOwned = false;
			Harness.KingdomCampHeartNativeProvider.Require(false, "loaded higher-heart input isolation failed");
		}

		private bool VerifyChainInput(bool Loaded = false)
		{
			if (!Loaded && !Harness.KingdomCampHeartChainScript.Matches(Verbs)) return true;
			var target = AccessTools.Method(typeof(GameManager), nameof(GameManager.UpdateInput));
			var patches = Harmony.GetPatchInfo(target);
			var prefix = AccessTools.Method(typeof(Harness.KingdomCampHeartChainInputPatch), "Prefix");
			bool installed = patches != null && patches.Prefixes.Any(patch => patch.PatchMethod == prefix);
			bool owned = ReferenceEquals(ChainInputOwner(), this);
			long before = Interlocked.Read(ref ChainInputSkipped);
			if (installed && owned && GameManager.Instance != null) GameManager.Instance.UpdateInput();
			bool skipped = Interlocked.Read(ref ChainInputSkipped) > before;
			bool ok = installed && owned && skipped && Interlocked.Read(ref ChainInputLeaked) == 0;
			Harness.KingdomScenarioJournal.Append(Loaded ? "camp-heart-chain-load-input" : "camp-heart-chain-input", ok,
				"original-update-skipped=" + (skipped ? "true" : "false")
				+ "; scope=dedicated-game-lifetime; installed=" + installed + "; owned=" + owned
				+ "; original-ran=" + Interlocked.Read(ref ChainInputLeaked));
			return ok;
		}
	}
}

namespace ThousandAndFirst.Harness
{
	[HarmonyPatch(typeof(GameManager), nameof(GameManager.UpdateInput))]
	internal static class KingdomCampHeartChainInputPatch
	{
		[HarmonyPrefix, HarmonyPriority(Priority.First)]
		internal static bool Prefix(out KingdomScenarioAutoRunner __state)
		{
			__state = KingdomScenarioAutoRunner.ChainInputOwner();
			return __state == null;
		}

		[HarmonyPostfix]
		internal static void Postfix(KingdomScenarioAutoRunner __state, bool __runOriginal)
		{
			__state?.ObserveChainInput(__runOriginal);
		}
	}
}
