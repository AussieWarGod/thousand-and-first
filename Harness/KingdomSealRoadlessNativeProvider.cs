using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomSealRoadlessNativeProvider : IKingdomScenarioVerbProvider
	{
		private const string Setup = "seal-roadless-setup", Check = "seal-roadless-check";
		private static readonly string[] Script = { "stagedigest", Setup, "advance 2400", Check,
			"advance 2400", Check, "stagedigest" };
		private static XRLGame Game;
		private static Zone Zone;
		private static KingdomSystem System;
		private static KingdomSeal Seal;
		private static string Before;
		private static bool Attempted;
		private static int Phase, NoticeBaseline;
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { Setup, Check };

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument) && (Verb == Setup || Verb == Check), "unknown verb or argument");
				Require(KingdomScenarioScript.TryRead(out var script, out _) && script.Count == Script.Length,
					"exact roadless script absent");
				for (int i = 0; i < Script.Length; i++) Require(script[i] == Script[i], "roadless script differs");
				if (Verb == Setup)
				{
					Require(!Attempted && The.Game != null && The.Player?.CurrentZone != null
						&& !(The.Game.GetSystem<KingdomSystem>()?.Founded ?? false)
						&& KingdomMaster.ConfiguredEnabled && KingdomGrowth.Enabled,
						"requires a fresh enabled scenario realm");
					Attempted = true; Game = The.Game; Zone = The.Player.CurrentZone;
					System = KingdomNativeCampFounding.Found(Game, Zone, Require);
					NoticeBaseline = Notices();
					KingdomScenarioCompletedHeart.Complete(Game, System, Zone);
					long tick = Game.TimeTicks;
					KingdomSurvey survey = KingdomSurvey.Take(Zone, System);
					Require(survey != null && ReferenceEquals(survey.Ground, Zone), "completed camp survey absent");
					using (survey.BindPass())
						Simulation.City.KingdomCity.CheckIn(System, Zone, survey, tick);
					Require(!KingdomSurvey.HasBoundPass && Game.TimeTicks == tick
						&& System.City != null && System.City.WorkIds.Count > 0,
						"completed camp check-in did not publish actual work rows without moving time");
					Seal = Game.GetSystem<KingdomSeal>();
					Require(Seal != null && System.Population == 0 && KingdomPlots.HeartRung(Zone) == 1,
						"completed empty camp or seal absent");
					Before = Seal.NativePendingStageEvidence(); Phase = 1;
				}
				else Require(Phase == 1 || Phase == 2, "roadless check is not armed");
				Require(ReferenceEquals(The.Game, Game) && ReferenceEquals(The.Player.CurrentZone, Zone)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)
					&& ReferenceEquals(Game.GetSystem<KingdomSeal>(), Seal) && !KingdomSurvey.HasBoundPass,
					"roadless owner or observation boundary changed");
				Require(Seal.NativeSpatialCaptureWaits(out string failure),
					"roadless capture did not return typed pending: " + failure);
				Require(Seal.NativePendingStageEvidence() == Before, "pending capture changed revision or staged record");
				if (Verb == Check)
				{
					Require(Notices() == NoticeBaseline + 1, "pending notice missing or repeated");
					Phase++;
				}
				Ok = true;
				return "native-seal-roadless phase=" + Phase + "; pending=true; stage-unchanged=true"
					+ "; notices=" + (Notices() - NoticeBaseline) + "; synthetic-heart-calendar=true"
						+ "; setup-production-checkin=true; ordinary-acceptance=false; save-load=untested";
			}
			catch (Exception error) { return KingdomScenarioRefusal.Message("taf-seal-roadless-refused", error.Message); }
		}

		private static int Notices()
		{
			Require(System?.Ledger?.Notes != null, "seal notice ledger unavailable");
			int found = 0;
			foreach (string note in System.Ledger.Notes)
				if (note != null && note.Contains("The kingdom's seal waits for a worn street")) found++;
			return found;
		}
		private static void Require(bool Value, string Failure)
		{
			if (!Value) throw new InvalidOperationException(Failure);
		}
	}
}
