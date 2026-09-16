using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Which dedicated stockpile the lifecycle pays from, decided engine-free so it is proved by
	/// value (DevTests/KingdomQuickstartLifecycleStoreRulesTests.cs, both public projects).
	/// <para>
	/// NATIVE RUN 38 (2fa563c). lifecycle-grown landed built=1 for the first time, then
	/// lifecycle-save refused: "more than one dedicated stockpile stands here". Both stores are
	/// lawful production artefacts: the Quickstart bootstrap's camp materials chest
	/// (World/KingdomQuickstartBootstrap.Materials.cs:34, the one lifecycle-open read) and the
	/// heart's own dry store, <c>r_KingdomHeartStockpile</c> (RuntimeData/ObjectBlueprints.xml
	/// KingdomStockpile=1; Architecture/KingdomArchitectures-CivicFaith.xml palette
	/// civic-heart-natural slot "store"), stamped when the settlement raised heart rung 1
	/// (heartbasin) during the 7200-turn advance. Neither is removed or undedicated: the
	/// harness had bound itself to a SCAN ("exactly one") instead of to the store it opened with.
	/// </para>
	/// <para>
	/// RULE. lifecycle-open records the store it read into the durable opened receipt
	/// (KingdomQuickstartLifecycleSteps.OpenedKey, <c>zone|storeId|turns</c>, a string game state
	/// that survives the save into session 2). Every later step resolves THAT id among the
	/// dedicated stores standing now: bound id present among candidates -> that store, however
	/// many others stand; bound id absent -> refuse, naming what does stand; no binding yet (the
	/// open itself) -> exactly one candidate resolves, more refuse as ambiguous, none refuses with
	/// <see cref="NoneFailure" /> (the exact text lifecycle-open's bounded wait keys on).
	/// </para>
	/// </summary>
	internal static class KingdomQuickstartLifecycleStoreRules
	{
		/// <summary>The exact reading TryStockpile reports when no store is dedicated yet -- the
		/// one case lifecycle-open's bounded wait is for.</summary>
		internal const string NoneFailure = "this settlement has no dedicated stockpile to pay from";

		internal const char OpenedSeparator = '|';

		/// <summary>The store id an opened receipt (<c>zone|storeId|turns</c>) binds, or null when
		/// there is no receipt or it is not three non-empty fields.</summary>
		internal static string BoundStoreId(string Opened)
		{
			if (string.IsNullOrEmpty(Opened)) return null;
			string[] parts = Opened.Split(OpenedSeparator);
			if (parts.Length != 3) return null;
			for (int i = 0; i < parts.Length; i++)
				if (string.IsNullOrEmpty(parts[i])) return null;
			return parts[1];
		}

		/// <summary>The opened receipt text, the one spelling both the writer and the reader use.</summary>
		internal static string OpenedReceipt(string ZoneId, string StoreId, long Turns)
		{
			return ZoneId + OpenedSeparator + StoreId + OpenedSeparator + Turns;
		}

		/// <summary>
		/// Picks the store. <paramref name="CandidateIds" /> are the ids of every dedicated store
		/// standing now, in scan order; <paramref name="BoundId" /> is the opened receipt's store
		/// or null before the open. False carries a refusal in <paramref name="Failure" />.
		/// </summary>
		internal static bool Select(string BoundId, IReadOnlyList<string> CandidateIds,
			out string Selected, out string Failure)
		{
			Selected = null;
			Failure = null;
			int count = CandidateIds == null ? 0 : CandidateIds.Count;
			if (count == 0)
			{
				Failure = BoundId == null ? NoneFailure
					: "the stockpile bound at lifecycle-open (" + BoundId
						+ ") no longer stands here, and no dedicated stockpile does";
				return false;
			}
			if (BoundId != null)
			{
				for (int i = 0; i < count; i++)
					if (CandidateIds[i] == BoundId) { Selected = BoundId; return true; }
				Failure = "the stockpile bound at lifecycle-open (" + BoundId
					+ ") no longer stands here; " + count + " dedicated store(s) stand: "
					+ Join(CandidateIds);
				return false;
			}
			if (count == 1) { Selected = CandidateIds[0]; return true; }
			Failure = "more than one dedicated stockpile stands here (" + Join(CandidateIds)
				+ ") and none is bound yet; the lifecycle refuses to guess which one the "
				+ "settlement pays from";
			return false;
		}

		private static string Join(IReadOnlyList<string> Ids)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			for (int i = 0; i < Ids.Count; i++)
			{
				if (i > 0) text.Append(',');
				text.Append(string.IsNullOrEmpty(Ids[i]) ? "unassigned" : Ids[i]);
			}
			return text.ToString();
		}
	}
}
