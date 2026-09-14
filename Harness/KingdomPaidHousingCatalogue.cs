using System;
using System.Collections.Generic;
using System.Reflection;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>Exact-game test-only catalogue edit; never changes XML, receipt bytes, payment or frozen geometry.</summary>
	internal static class KingdomPaidHousingCatalogue
	{
		private static Dictionary<string, KingdomSocketTransition> Routes, History;
		private static string Route, Digest;
		private static KingdomSocketTransition Original, PreviousHistory;
		private static bool HadHistory;
		internal static void Change(GameObject work, KingdomArchitectureIntent before,
			KingdomArchitectureIntent after, KingdomSocketTransition paid)
		{
			Require(Original == null && ReferenceEquals(The.Game, KingdomPaidHousingNativeProvider.Owner), "catalogue scope differs");
			Require(KingdomSocketTransitionRules.TryDeclarationDigest(paid, out Digest), "paid digest absent");
			Routes = Registry("byRoute"); History = Registry("byHistoricalDigest");
			Route = KingdomSocketTransitionRules.IndexKey(paid.FromBuildKey, paid.ToBuildKey, paid.LotType, paid.LotSize);
			Require(Routes.TryGetValue(Route, out Original) && KingdomSocketTransitionRules.MatchesRoute(paid, Original),
				"current registered price differs before controlled edit");
			HadHistory = History.TryGetValue(Digest, out PreviousHistory);
			Require(!HadHistory, "fixture requires its current M price not already registered as history");
			Routes[Route] = new KingdomSocketTransition(paid.Key, paid.FromBuildKey, paid.ToBuildKey,
				paid.LotType, paid.LotSize, paid.Mode, paid.WaterDrams + 1, paid.Materials, paid.WorkTicks);
			Require(!KingdomSocketTransitions.Authorizes(work, before, after), "unknown old price authorized without retained declaration");
			History.Add(Digest, Original);
			Require(KingdomSocketTransitions.Authorizes(work, before, after), "registered paid price did not restore authority");
			Require(KingdomSocketTransitions.TryGet(paid.FromBuildKey, paid.ToBuildKey, paid.LotType, paid.LotSize,
				out var current) && current.WaterDrams == paid.WaterDrams + 1
				&& !KingdomSocketTransitions.TryResolveCurrent(paid, paid.FromBuildKey, paid.ToBuildKey,
					paid.LotType, paid.LotSize, out _), "new quote accepted obsolete price");
		}
		internal static bool Changed => Original != null;
		internal static void Restore()
		{
			if (Original == null) return;
			Routes[Route] = Original;
			if (HadHistory) History[Digest] = PreviousHistory;
			else History.Remove(Digest);
			Original = null;
		}
		private static Dictionary<string, KingdomSocketTransition> Registry(string name)
		{
			var field = typeof(KingdomSocketTransitions).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
			var registry = field?.GetValue(null) as Dictionary<string, KingdomSocketTransition>;
			Require(registry != null, "native catalogue registry unavailable: " + name);
			return registry;
		}
		private static void Require(bool value, string failure) => KingdomPaidHousingNativeProvider.Require(value, failure);
	}
}
