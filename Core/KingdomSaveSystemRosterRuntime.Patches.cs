#if !TAF_TESTS
using System;
using HarmonyLib;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>
	/// ImportGameState is the only exact load seam: serialized IntGameState is installed at
	/// XRL/XRLGame.cs:1823-1828, imported overrides are applied at :1910 and this method's body is
	/// :2476-2508. Registration and AfterGameLoadedEvent do not begin until :1946-1954. A postfix
	/// therefore sees the final marker but runs before any loaded system receives gameplay work.
	/// </summary>
	[HarmonyPatch(typeof(XRLGame), "ImportGameState")]
	internal static class KingdomSaveSystemRosterImportPatch
	{
		private static void Postfix(XRLGame __instance)
		{
			KingdomSaveSystemRosterLoadEvidence.Consume(out bool evidenceKnown,
				out bool modWasPresent, out bool inheritanceAuthorityUnreadable);
			if (!KingdomSaveSystemRosterRuntime.ValidateAfterImport(__instance,
				evidenceKnown, modWasPresent, inheritanceAuthorityUnreadable,
				out string failure))
				KingdomLog.Log("save roster: load entered recovery (" + failure + ")");
		}
	}

	/// <summary>New games have a direct proof unavailable to loaded saves. This mutator runs after
	/// embark game systems are registered and atomically creates the mandatory roster and marker.</summary>
	[PlayerMutator]
	public sealed class KingdomSaveSystemRosterNewGameLoader : IPlayerMutator
	{
		public void mutate(XRL.World.GameObject player)
		{
			if (!KingdomSaveSystemRosterRuntime.TryInitializeNewGame(The.Game,
				out string failure))
				throw new InvalidOperationException(
					"ThousandAndFirst could not initialize its save-system roster ("
					+ failure + ").");
		}
	}

	/// <summary>
	/// SaveSystems begins by removing flagged systems, then calls each surviving BeforeSave
	/// (XRL/XRLGame.cs:1580-1590). The prefix performs that first public operation early, proves the
	/// resulting exact roster, and lets the original no-op removal run again. The marker is then
	/// serialized with IntGameState at :2324-2328, before FinalizeWrite and primary replacement at
	/// :2335-2356. Throwing here leaves the existing primary untouched.
	/// </summary>
	[HarmonyPatch(typeof(XRLGame), "SaveSystems")]
	internal static class KingdomSaveSystemRosterSavePatch
	{
		[HarmonyPriority(Priority.Last)]
		private static void Prefix(XRLGame __instance)
		{
			if (__instance == null)
				throw new InvalidOperationException(
					"ThousandAndFirst: refusing to save without a game-system registry.");
			__instance.RemoveFlaggedSystems();
			if (!KingdomSaveSystemRosterRuntime.TryPrepareBeforeSave(__instance,
				out string failure))
				throw new InvalidOperationException(
					"ThousandAndFirst: refusing to save because the saved game-system roster "
					+ "cannot be proved (" + failure + "). Quit without saving and keep the "
					+ "existing save.");
		}
	}
}
#endif
