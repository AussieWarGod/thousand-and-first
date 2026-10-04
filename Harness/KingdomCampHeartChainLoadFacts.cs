using System;
using System.Collections.Generic;
using System.IO;
using XRL;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartChainLoadFacts
	{
		internal static void Retain(XRLGame Game, string Phase, Dictionary<string, string> Records)
		{
			Require(Game != null && ReferenceEquals(The.Game, Game), "higher-heart facts lost their active game");
			Require(Phase == "preactivation" || Phase == "activated" || Phase == "completed",
				"unknown higher-heart observation phase");
			Require(Records != null && Records.Count == 4, "higher-heart fact domains missing");
			string root = KingdomScenarioSaveFiles.Root();
			foreach (string domain in new[] { "jobs", "residents", "support", "custody" })
			{
				Require(Records.TryGetValue(domain, out string wire) && wire != null,
					"higher-heart fact domain missing: " + domain);
				KingdomScenarioSaveFiles.WriteNew(Path.Combine(root,
					"camp-heart-chain-" + Phase + "-" + domain + "-facts.txt"), wire);
			}
		}

		internal static void RequireExact(KingdomCampHeartChainSnapshot Observed, string ExpectedWire)
		{
			Require(KingdomCampHeartChainSnapshotCodec.TryDecode(ExpectedWire, out var expected),
				"higher-heart expected snapshot is malformed");
			Require(KingdomCampHeartChainSnapshotCodec.TryEncode(Observed, out string wire),
				"higher-heart observed snapshot is malformed");
			Require(wire == ExpectedWire, "higher-heart state differs: expected="
				+ KingdomScenarioSaveFiles.HashText(ExpectedWire) + "; observed=" + KingdomScenarioSaveFiles.HashText(wire)
				+ "; jobs=" + (Observed.JobsDigest == expected.JobsDigest)
				+ "; residents=" + (Observed.ResidentsDigest == expected.ResidentsDigest)
				+ "; support=" + (Observed.SupportDigest == expected.SupportDigest)
				+ "; custody=" + (Observed.CustodyDigest == expected.CustodyDigest));
		}

		internal static string Identity(KingdomCampHeartChainSnapshot Snapshot)
			=> "rung=" + Snapshot.Rung + "; heart=" + Snapshot.Heart.Id + "; basin=" + Snapshot.Basin.Id
				+ "; store=" + Snapshot.Store.Id + "; track=" + Snapshot.Track.Id + "; resident=" + Snapshot.ResidentId
				+ "; job=" + Snapshot.JobId + "; population=" + Snapshot.Population
				+ "; turns=" + Snapshot.Turns + "; time-ticks=" + Snapshot.TimeTicks;
		private static void Require(bool Value, string Failure) => KingdomCampHeartNativeProvider.Require(Value, Failure);
	}
}
