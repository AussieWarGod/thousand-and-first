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
		internal static bool TryPrepareRung(KingdomSystem system, Zone zone, KingdomSurvey survey,
			long now, out string refusal)
		{
			refusal = "The settlement's fall waits for an exact account of its works and roofs.";
			try
			{
				if (The.Game == null || system == null || !system.Founded || zone == null || survey == null
					|| survey.Ground != zone || KingdomSurvey.ActiveFor(zone) != survey
					|| !TryReadOwned(system, out List<Snapshot> books)) return false;
				XRLGame game = The.Game;
				long token = system.MasterAppliedResumeToken;
				if (!ReferenceEquals(game.GetSystem<KingdomSystem>(), system)) return false;
				Snapshot owner = books.Find(item => ReferenceEquals(item.City, system.City));
				KingdomSubsidenceStepOperation active = owner?.Step.Active;
				if (active == null || active.Phase != KingdomSubsidenceStepPhase.Settling
					|| active.RungModel != KingdomSubsidenceStepRules.UnplannedRungs
					|| !owner.City.TryReadExact(out _, out _)) return false;
				List<GameObject> candidates = new List<GameObject>(survey.Built);
				List<KingdomSubsidenceRungWork> works = new List<KingdomSubsidenceRungWork>();
				Dictionary<string, r_KingdomWear> parts = new Dictionary<string, r_KingdomWear>(StringComparer.Ordinal);
				HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
				Dictionary<string, GameObject> residents = new Dictionary<string, GameObject>(StringComparer.Ordinal);
				foreach (GameObject work in candidates)
				{
					if (!GameObject.Validate(work) || string.IsNullOrEmpty(work.IDIfAssigned)
						|| !ids.Add(work.IDIfAssigned)) return false;
					if (!KingdomSubsidenceRules.RollRuin(owner.Step.SettlementId, work.IDIfAssigned,
						(ulong)active.DueTick, active.FromStage)) continue;
					if (works.Count >= KingdomSubsidenceRungRules.MaxWorks || work.CurrentCell == null
						|| !FreeRungWear(work, out r_KingdomWear wear)) return false;
					int before = wear == null ? 0 : wear.Wear;
					int after = KingdomMaterialRules.AddWear(before, KingdomSubsidenceRules.RolledRuinIncrement(
						owner.Step.SettlementId, work.IDIfAssigned, (ulong)active.DueTick));
					string plot = work.GetStringProperty(KingdomPlots.PlotIdProperty) ?? "";
					List<KingdomSubsidenceRungRoof> roofs = new List<KingdomSubsidenceRungRoof>();
					if (KingdomLodging.Enabled && plot != "" && !KingdomLodgingRules.IsCondemned(before)
						&& KingdomLodgingRules.IsCondemned(after)
						&& !CaptureRungRoofs(system, owner.City, survey, plot, roofs, residents)) return false;
					string name = KingdomDesign.ReferenceFor(work, work.ShortDisplayName);
					parts.Add(work.IDIfAssigned, wear);
					works.Add(new KingdomSubsidenceRungWork(KingdomCityRules.StableId(work.IDIfAssigned),
						work.IDIfAssigned, work.Blueprint, plot, work.GetStringProperty(KingdomUpgrade.BuildKeyProperty),
						name, work.CurrentCell.X, work.CurrentCell.Y, wear != null, before, after,
						KingdomSubsidenceEffectPhase.Prepared, roofs));
				}
				works.Sort((left, right) => string.CompareOrdinal(left.ObjectId, right.ObjectId));
				KingdomSubsidenceRungPlan plan = new KingdomSubsidenceRungPlan(active.Id, owner.Step.RealmId,
					owner.Step.SettlementId, zone.ZoneID, active.FromStage, active.ReachedStage,
					active.DueTick, now, active.Completed, works);
				if (!KingdomSubsidenceRungRules.Matches(plan, owner.Step)) return false;
				RungFrame frame = new RungFrame { Game = game, System = system, Zone = zone,
					Survey = survey, Owner = owner, Plan = plan, Token = token };
				foreach (KingdomSubsidenceRungWork work in works)
					foreach (KingdomSubsidenceRungRoof roof in work.Roofs) ids.Add(roof.BodyObjectId);
				if (!KingdomPlots.TryCaptureGlobalLiveIds(ids, out frame.Subjects) || !RungOwnerExact(frame)
					|| candidates.Count != survey.Built.Count) return false;
				foreach (KeyValuePair<string, GameObject> resident in residents)
					if (!frame.Subjects.TryGetValue(resident.Key, out GameObject body)
						|| !ReferenceEquals(body, resident.Value)) return false;
				for (int i = 0; i < candidates.Count; i++)
					if (!ReferenceEquals(candidates[i], survey.Built[i])
						|| !frame.Subjects.TryGetValue(candidates[i].IDIfAssigned, out GameObject exact)
						|| !ReferenceEquals(exact, candidates[i])) return false;
				foreach (KingdomSubsidenceRungWork work in works)
				{
					if (!frame.Subjects.TryGetValue(work.ObjectId, out GameObject body)
						|| !RungWorkExact(frame, work, body) || !FreeRungWear(body, out r_KingdomWear wear)
						|| !ReferenceEquals(wear, parts[work.ObjectId])
						|| (wear == null ? 0 : wear.Wear) != work.BeforeWear) return false;
					List<KingdomSubsidenceRungRoof> currentRoofs = new List<KingdomSubsidenceRungRoof>();
					if (KingdomLodging.Enabled && work.PlotId != "" && !KingdomLodgingRules.IsCondemned(work.BeforeWear)
						&& KingdomLodgingRules.IsCondemned(work.AfterWear)
						&& (!owner.City.TryReadExact(out _, out _)
							|| !CaptureRungRoofs(system, owner.City, survey, work.PlotId, currentRoofs, residents))) return false;
					if (currentRoofs.Count != work.Roofs.Count) return false;
					for (int i = 0; i < currentRoofs.Count; i++)
						if (currentRoofs[i].ResidentId != work.Roofs[i].ResidentId
							|| currentRoofs[i].BodyObjectId != work.Roofs[i].BodyObjectId) return false;
					foreach (KingdomSubsidenceRungRoof roof in work.Roofs)
					{
						if (!RungRoofExact(frame, work, roof, out KingdomCityBook.SubsidenceRoofRow row)
							|| row.RoofStanding != roof.BeforeStanding || row.Reached != roof.BeforeReached
							|| row.Warned != roof.BeforeWarned) return false;
					}
				}
				if (!KingdomSubsidenceStepRules.TryFreezeRungPlan(owner.Step, plan,
					out KingdomSubsidenceStepBook next) || !SaveRung(frame, next)) return false;
				refusal = null;
				return true;
			}
			catch (Exception) { return false; }
		}

		private static bool FreeRungWear(GameObject work, out r_KingdomWear wear)
		{
			wear = work.GetPart<r_KingdomWear>();
			int copies = 0;
			if (work.PartsList != null)
				foreach (IPart part in work.PartsList) if (part is r_KingdomWear) copies++;
			return copies == (wear == null ? 0 : 1) && KingdomSubsidenceRungRuntime.ConstructionAvailable(work)
				&& (wear == null || wear.ParentObject == work && !wear.LifecycleQuarantined
					&& wear.IncidentPhase == (int)KingdomWearIncidentPhase.None
					&& wear.LeakPhase == (int)KingdomWearLeakPhase.None && wear.RepairEffortLeft == 0
					&& wear.Wear >= 0 && wear.Wear <= KingdomMaterialRules.MaxWearPercent);
		}

		private static bool CaptureRungRoofs(KingdomSystem system, KingdomCityBook city,
			KingdomSurvey survey, string plot, List<KingdomSubsidenceRungRoof> roofs,
			Dictionary<string, GameObject> residents)
		{
			foreach (GameObject body in survey.CitizenBodies)
			{
				if (!GameObject.Validate(body) || !KingdomCitizenship.BelongsTo(system, body)
					|| body.GetStringProperty(KingdomLodging.HomePlotIdProperty) != plot
					|| string.IsNullOrEmpty(body.GetStringProperty("KingdomName"))) continue;
				if (roofs.Count >= KingdomSubsidenceRungRules.MaxRoofs
					|| string.IsNullOrEmpty(body.IDIfAssigned)
					|| !city.TryCaptureSubsidenceRoof(KingdomResidents.IdOf(body),
						out KingdomCityBook.SubsidenceRoofRow row)) return false;
				if (residents.TryGetValue(body.IDIfAssigned, out GameObject prior) && !ReferenceEquals(prior, body)) return false;
				residents[body.IDIfAssigned] = body;
				roofs.Add(new KingdomSubsidenceRungRoof(row.ResidentId, body.IDIfAssigned,
					row.RoofStanding, row.Reached, row.Warned, KingdomSubsidenceEffectPhase.Prepared));
			}
			roofs.Sort((left, right) => left.ResidentId.CompareTo(right.ResidentId));
			return true;
		}
	}
}
