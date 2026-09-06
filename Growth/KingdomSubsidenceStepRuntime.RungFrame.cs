using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private sealed class RungFrame
		{
			internal XRLGame Game;
			internal KingdomSystem System;
			internal Zone Zone;
			internal KingdomSurvey Survey;
			internal Snapshot Owner;
			internal KingdomSubsidenceRungPlan Plan;
			internal Dictionary<string, GameObject> Subjects;
			internal long Token;
		}

		private static bool TryRungFrame(KingdomSystem system, Zone zone, KingdomSurvey survey,
			out RungFrame frame)
		{
			frame = null;
			if (The.Game == null || system == null || !system.Founded || zone == null
				|| survey == null || survey.Ground != zone || KingdomSurvey.ActiveFor(zone) != survey
				|| !TryReadOwned(system, out List<Snapshot> books)) return false;
			Snapshot owner = books.Find(item => ReferenceEquals(item.City, system.City));
			if (owner == null || !KingdomSubsidenceStepRules.TryReadRungPlan(owner.Step,
				out KingdomSubsidenceRungPlan plan) || plan.ZoneId != zone.ZoneID) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			foreach (KingdomSubsidenceRungWork work in plan.Works)
			{
				if (work.ReleasePhase == KingdomSubsidenceReleasePhase.Released) continue;
				ids.Add(work.ObjectId);
				foreach (KingdomSubsidenceRungRoof roof in work.Roofs)
					if (roof.Phase != KingdomSubsidenceEffectPhase.Proved) ids.Add(roof.BodyObjectId);
			}
			RungFrame candidate = new RungFrame { Game = The.Game, System = system, Zone = zone,
				Survey = survey, Owner = owner, Plan = plan, Token = system.MasterAppliedResumeToken };
			candidate.Subjects = new Dictionary<string, GameObject>(StringComparer.Ordinal);
			if (ids.Count != 0 && !KingdomPlots.TryCaptureGlobalLiveIds(ids, out candidate.Subjects)) return false;
			if (!RungOwnerExact(candidate)) return false;
			frame = candidate;
			return true;
		}

		private static bool RungOwnerExact(RungFrame frame)
		{
			return frame != null && ReferenceEquals(The.Game, frame.Game)
				&& ReferenceEquals(frame.Game.GetSystem<KingdomSystem>(), frame.System)
				&& frame.System.Founded && ReferenceEquals(frame.System.City, frame.Owner.City)
				&& frame.System.CurrentRealmId == frame.Plan.RealmId
				&& KingdomChronicle.SettlementId(frame.System) == frame.Plan.SettlementId
				&& frame.Owner.City.SettlementId == frame.Plan.SettlementId
				&& frame.System.MasterAppliedResumeToken == frame.Token
				&& frame.System.ClaimedZones != null && frame.System.ClaimedZones.Contains(frame.Plan.ZoneId)
				&& frame.Zone.ZoneID == frame.Plan.ZoneId && frame.Survey.Ground == frame.Zone
				&& KingdomSurvey.ActiveFor(frame.Zone) == frame.Survey
				&& frame.Owner.City.HasValidSubsidenceStorage()
				&& frame.Owner.City.SubsidenceModel == frame.Owner.Wire;
		}

		private static bool RungWorkExact(RungFrame frame, KingdomSubsidenceRungWork row, GameObject work)
		{
			if (!RungOwnerExact(frame) || !GameObject.Validate(work)
				|| work.IDIfAssigned != row.ObjectId || work.Blueprint != row.Blueprint
				|| work.CurrentZone != frame.Zone || work.CurrentCell == null
				|| work.CurrentCell.ParentZone != frame.Zone || work.CurrentCell.X != row.X
				|| work.CurrentCell.Y != row.Y || work.InInventory != null || work.Equipped != null
				|| !work.HasIntProperty("KingdomBuilt") || work.HasStringProperty("KingdomBuilt")
				|| work.GetIntProperty("KingdomBuilt") != 1
				|| !RawRungProperty(work, KingdomPlots.PlotIdProperty, row.PlotId == "" ? null : row.PlotId)
				|| !RawRungProperty(work, KingdomUpgrade.BuildKeyProperty, row.DesignStamp)) return false;
			int sameId = 0, sameReference = 0;
			foreach (GameObject item in work.CurrentCell.Objects)
			{
				if (item != null && item.IDIfAssigned == row.ObjectId) sameId++;
				if (ReferenceEquals(item, work)) sameReference++;
			}
			return sameId == 1 && sameReference == 1;
		}

		private static bool RawRungProperty(GameObject work, string key, string expected)
		{
			return !work.HasIntProperty(key) && work.HasStringProperty(key) == (expected != null)
				&& (expected == null || string.Equals(work.GetStringProperty(key), expected, StringComparison.Ordinal));
		}

		private static bool SaveRung(RungFrame frame, KingdomSubsidenceStepBook next)
		{
			if (!RungOwnerExact(frame) || !KingdomSubsidenceStepRules.TryReadRungPlan(next,
				out KingdomSubsidenceRungPlan plan) || !KingdomSubsidenceStepCodec.TryEncode(next, out string wire)
				|| !Publish(frame.System, frame.Owner, next)) return false;
			frame.Owner = new Snapshot(frame.Owner.City, wire, next);
			frame.Plan = plan;
			return RungOwnerExact(frame);
		}
	}
}
