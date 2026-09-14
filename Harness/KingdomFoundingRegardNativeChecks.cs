using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>Scenario-only witness: actual founding, civic changes, native reputation events
	/// and cold persistence. Synthetic reputation changes are explicit; no citizens are fabricated.</summary>
	internal static class KingdomFoundingRegardNativeChecks
	{
		private const string WitnessKey = "TAFScenarioFoundingRegardV2";

		internal static void Observe(XRLGame game, KingdomSystem system, string stage)
		{
			Require(game != null && system != null && system.Founded, "no founded realm for reputation probe");
			if (stage == "startup")
			{
				Require(!game.HasStringGameState(WitnessKey), "founding reputation probe already ran");
				int compared = 0;
				foreach (Faction faction in Factions.Loop())
				{
					if (faction == null || !system.CanReserveDirectionalRelationship(faction.Name)) continue;
					bool present = system.Standings.TryGetValue(faction.Name, out int value);
					int personal = game.PlayerReputation.Get(faction);
					Require(present && value == personal, "founding baseline differs: " + faction.Name
						+ "; present=" + present + "; standing=" + value + "; personal=" + personal
						+ "; ledger-count=" + system.Standings.Count
						+ "; observation-count=" + system.RegardSpilloverObservedReputation.Count);
					Require(system.RegardSpilloverObservedReputation.TryGetValue(faction.Name, out int observed)
						&& observed == value, "founding observation differs: " + faction.Name);
					compared++;
				}
				Require(compared > 10 && compared == system.Standings.Count &&
					system.RealmPolicyToward.Count == 0 && system.RegardSpilloverRemainders.Count == 0,
					"founding baseline absent, incomplete or confused with outgoing policy");
				Faction farmers = Factions.GetIfExists("Farmers");
				Require(farmers != null && system.Standings.TryGetValue("Farmers", out int baseline),
					"native Farmers baseline is absent");
				baseline = system.GetRegardForRealm("Farmers");
				int personalBefore = game.PlayerReputation.Get(farmers);
				Require(system.TrySetRegardForRealm("Farmers", baseline + 73), "civic regard adjustment refused");
				game.PlayerReputation.Modify(farmers, 40, "TAF native founding reputation probe");
				int personalAfter = game.PlayerReputation.Get(farmers);
				Require(personalAfter != personalBefore && KingdomStandingRules.TrySpillover(
					baseline + 73, 0, personalBefore, personalAfter, system.Stage,
					out int expected, out int carry), "native reputation event did not change reputation");
				// Explicit assignments keep compiler flow independent of the assertion helper.
				KingdomStandingRules.TrySpillover(baseline + 73, 0, personalBefore, personalAfter,
					system.Stage, out expected, out carry);
				system.ReassertFeelings();
				system.RegardSpilloverRemainders.TryGetValue("Farmers", out int actualCarry);
				Require(system.GetRegardForRealm("Farmers") == expected && actualCarry == carry &&
					system.RealmPolicyToward.Count == 0 && farmers.GetFeelingTowardsFaction(
						system.KingdomFactionName) == Reputation.GetFeeling((float)expected),
					"native spillover or projected feeling lost independent civic regard");
				var rows = Sorted(system.Standings);
				Require(KingdomFoundingRegardRules.TryEncode(2, rows, out string wire),
					"native reputation witness encoding refused");
				game.SetStringGameState(WitnessKey, wire);
				KingdomLog.Log("founding regard native change: baseline=" + baseline + "; city-delta=73"
					+ "; personal-before=" + personalBefore + "; personal-after=" + personalAfter
					+ "; standing=" + expected + "; carry=" + carry + "; compared=" + compared
					+ "; synthetic-reputation=true; synthetic-residents=false");
			}
			Require(game.HasStringGameState(WitnessKey), "founding reputation witness missing after load");
			string saved = game.GetStringGameState(WitnessKey);
			Require(KingdomFoundingRegardRules.TryDecode(saved, out int version, out var frozen)
				&& version == 2, "founding reputation witness is malformed");
			Require(KingdomFoundingRegardRules.TryEncode(2, Sorted(system.Standings), out string current)
				&& current == saved, "city reputation changed across the controlled scenario boundary");
			string digest;
			using (var sha = SHA256.Create())
				digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(saved))).Replace("-", "").ToLowerInvariant();
			KingdomLog.Log("founding regard native witness: stage=" + stage + "; entries=" + frozen.Count
				+ "; sha256=" + digest + "; preserved=true");
		}

		private static List<KeyValuePair<string, int>> Sorted(Dictionary<string, int> source)
		{
			var result = new List<KeyValuePair<string, int>>(source);
			result.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
			return result;
		}

		private static void Require(bool condition, string reason)
		{
			if (!condition) throw new InvalidOperationException(reason);
		}
	}
}
