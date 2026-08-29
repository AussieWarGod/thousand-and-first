using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomLab
	{

		// ==================================================================================
		// Rungs 2 and 3 — the slate
		// ==================================================================================

		/// <summary>
		/// Level one of the slate: the founder's own body, place by place, and what is on each.
		/// <para>
		/// Straight from the golem mound's own option list &mdash; the marks, the sentinel rows, the
		/// escape &mdash; because that screen is the canon for exactly this act and re-inventing it
		/// would cost the player their familiarity for nothing.
		/// </para>
		/// </summary>
		internal static void OpenSlate(GameObject Building, GameObject Actor)
		{
			if (Actor == null || Building == null) return;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (system == null || !system.Founded)
			{
				return;
			}
			if (KingdomLabCivicRuntime.HandleSlate(system, Building.CurrentZone, Building)) return;
			r_KingdomLabRemovalJob removal = ActiveRemovalJob(Actor);
			if (removal != null)
			{
				if (!string.Equals(removal.PatientId, Actor.IDIfAssigned, StringComparison.Ordinal))
				{
					removal.State = KingdomLabRemovalPhase.Quarantined;
					removal.Fault = "This removal receipt belongs to another patient. It offers no action here.";
					Popup.Show(removal.Fault);
					return;
				}
				ManageRemoval(Actor, system, removal);
				return;
			}
			r_KingdomLabJob existing = Building?.GetPart<r_KingdomLabJob>();
			if (existing != null)
			{
				if (!string.Equals(existing.PatientId, Actor.IDIfAssigned, StringComparison.Ordinal))
				{
					existing.State = KingdomLabJobPhase.ApplicationRecovery;
					existing.Fault = "This hall's commission belongs to another patient. No payment, cancellation, application, or cleanup is offered.";
					Popup.Show(existing.Fault);
					return;
				}
				ManageJob(Building, Actor, system, existing);
				return;
			}
			if (HandleActivePatientRegistry(Actor, system)) return;
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("Kingdom work is paused. Existing laboratory recovery remains available, but no new procedure can be commissioned.");
				return;
			}
			// Turning the feature off refuses only a new commission. Existing application,
			// removal, registry and terminal outbox cleanup above must always keep running.
			if (!KingdomProcedures.Enabled) return;
			string city = system.SeatName;
			int rung = RungAt(Building);
			List<GameObject> kept = KeptParts(Actor);
			// Read once per slate rather than once per row: the roster is the same for every place
			// on the founder's body, and it is one city's rolls, not a per-procedure question.
			List<string> roster = KingdomZoning.Roster(system);
			r_KingdomLabRecord record = KingdomProcedures.Record(Actor);
			record.Normalize();
			List<string> names = new List<string>();
			List<LabSlot> anatomy = KingdomProcedures.Census(Actor, names);
			while (true)
			{
				List<string> options = new List<string>();
				List<int> slotIndex = new List<int>();
				List<string> directRemoval = new List<string>();
				List<XRL.World.Anatomy.BodyPart> live = Actor.Body?.GetParts();
				for (int receipt = 0; receipt < record.Keys.Count; receipt++)
				{
					int bodyId = record.BodyPartIds[receipt];
					XRL.World.Anatomy.BodyPart detached = KingdomProcedures.ExactBodyPart(Actor, bodyId);
					if (bodyId <= 0 || detached == null || ContainsBodyReference(live, detached)) continue;
					string label = (receipt < record.DisplayNames.Count
						&& !string.IsNullOrEmpty(record.DisplayNames[receipt]))
						? record.DisplayNames[receipt] : record.Keys[receipt];
					options.Add("{{M|detached graft receipt}} #" + bodyId + "  " + label);
					slotIndex.Add(-1);
					directRemoval.Add(record.Keys[receipt]);
				}
				for (int i = 0; i < anatomy.Count; i++)
				{
					List<LabProcedure> offers = Candidates(anatomy, i, rung, kept, record, roster);
					XRL.World.Anatomy.BodyPart exactPart = SelectedPart(Actor, i);
					string grafted = record.GraftedAt(exactPart?.ID ?? 0, anatomy[i].Type);
					if (offers.Count == 0 && grafted == null)
					{
						// A place the hall could never do anything with is not a row. A slate that
						// listed all 157 body-part types would be a slate nobody reads.
						continue;
					}
					LabProcedure held;
					options.Add(KingdomLabRules.SlotRow(names[i],
						(grafted != null && KingdomProcedures.TryGet(grafted, out held)) ? held.Named : null,
						offers.Count > 0));
					slotIndex.Add(i);
					directRemoval.Add(null);
				}
				string gap = KingdomLabRules.LadderGapLine(rung >= KingdomProcedureRules.RungSlab,
					rung >= KingdomProcedureRules.RungVat, rung >= KingdomProcedureRules.RungHall,
					rung >= KingdomProcedureRules.RungTheatre);
				if (options.Count == 0)
				{
					Popup.Show(gap ?? ("The hall has nothing it could do to you today, and it says so rather than "
						+ "showing you an empty list. Bring something to the vats, or raise the hall higher."));
					return;
				}
				int picked = Popup.PickOption(
					Title: KingdomLabRules.SlateTitle(KingdomPresentation.Rich(city)),
					Intro: KingdomLabRules.SlateIntro(
						KingdomPresentation.Rich(SavantAt(system)), null, TotalKept(kept))
						+ ((gap == null) ? "" : ("\n" + gap))
						+ (string.IsNullOrEmpty(KingdomLabCivicRuntime.Status(Building)) ? ""
							: ("\n\n" + KingdomLabCivicRuntime.Status(Building))),
					Options: options, AllowEscape: true, RespectOptionNewlines: true);
				if (picked < 0)
				{
					return;
				}
				int at = slotIndex[picked];
				if (!string.IsNullOrEmpty(directRemoval[picked]))
				{
					OfferRemoval(Actor, system, directRemoval[picked], city);
					names = new List<string>();
					anatomy = KingdomProcedures.Census(Actor, names);
					kept = KeptParts(Actor);
					if (KingdomGovernanceScope.HasCommitted) return;
					continue;
				}
				XRL.World.Anatomy.BodyPart exactStandingPart = SelectedPart(Actor, at);
				string standing = record.GraftedAt(exactStandingPart?.ID ?? 0, anatomy[at].Type);
				if (standing != null)
				{
					OfferRemoval(Actor, system, standing, city);
				}
				else
				{
					OfferProcedure(Building, Actor, system, anatomy, at, names[at], rung, kept, record, roster, city);
				}
				// The body may have changed under us, so it is read again rather than patched.
				names = new List<string>();
				anatomy = KingdomProcedures.Census(Actor, names);
					kept = KeptParts(Actor);
					if (KingdomGovernanceScope.HasCommitted)
					{
						return;
					}
				}
		}

		private static r_KingdomLabRemovalJob ActiveRemovalJob(GameObject Actor)
		{
			IList<IPart> parts = Actor?.PartsList;
			for (int i = 0; parts != null && i < parts.Count; i++)
			{
				r_KingdomLabRemovalJob job = parts[i] as r_KingdomLabRemovalJob;
				if (job == null) continue;
				job.Normalize();
				if (job.State != KingdomLabRemovalPhase.Complete
					&& job.State != KingdomLabRemovalPhase.Cancelled) return job;
				if (!string.Equals(job.PatientId, Actor.IDIfAssigned, StringComparison.Ordinal)
					|| job.SchemaQuarantined) return job;
				KingdomLabOwnedTarget ignored;
				KingdomLabOwnedTargetState observed = KingdomProcedures.ClassifyOwned(Actor,
					RemovalSnapshot(job), out ignored);
				if (observed == KingdomLabOwnedTargetState.Absent) continue;
				if (job.State == KingdomLabRemovalPhase.Complete
					&& observed == KingdomLabOwnedTargetState.Present)
				{
					job.State = KingdomLabRemovalPhase.RemovalRecovery;
					job.Fault = "The exact removed effect is present again. Its paid receipt was reopened and will charge no more water.";
				}
				else
				{
					job.State = KingdomLabRemovalPhase.Quarantined;
					job.Fault = observed == KingdomLabOwnedTargetState.Present
						? "An effect returned after a clean cancelled receipt. No procedure debt or removal authority was inferred."
						: "Terminal physical state is uncertain. The archived receipt was reopened only to quarantine it.";
				}
				return job;
			}
			return null;
		}

		private static int RemovalReceiptCount(GameObject Actor)
		{
			int count = 0;
			IList<IPart> parts = Actor?.PartsList;
			for (int i = 0; parts != null && i < parts.Count; i++)
			{
				if (parts[i] is r_KingdomLabRemovalJob) count++;
			}
			return count;
		}

		private static int CountParts<T>(GameObject Object) where T : IPart
		{
			int count = 0;
			IList<IPart> parts = Object?.PartsList;
			for (int i = 0; parts != null && i < parts.Count; i++)
			{
				if (parts[i] is T) count++;
			}
			return count;
		}

		/// <summary>
		/// Level two: what the hall could do at one place, each with its effects and its whole price
		/// stated before anything is committed.
		/// </summary>
		private static void OfferProcedure(GameObject Building, GameObject Actor, KingdomSystem System,
			List<LabSlot> Anatomy, int At,
			string SlotName, int Rung, List<GameObject> Kept, r_KingdomLabRecord Record, List<string> Roster, string City)
		{
			List<LabProcedure> offers = Candidates(Anatomy, At, Rung, Kept, Record, Roster);
			if (offers.Count == 0)
			{
				Popup.ShowFail(KingdomLabRules.NothingMeetsRequirement(SlotName));
				return;
			}
			List<string> rows = new List<string>();
			for (int i = 0; i < offers.Count; i++)
			{
				rows.Add(KingdomLabRules.CandidateRow(offers[i], CountFor(Kept, offers[i])));
			}
			int picked = Popup.PickOption(Title: "Choose a procedure for " + SlotName,
				Options: rows, AllowEscape: true, RespectOptionNewlines: true);
			if (picked < 0)
			{
				return;
			}
			LabProcedure procedure = offers[picked];
			int consent = Popup.PickOption(
				Title: procedure.Named,
				Intro: KingdomLabRules.PriceLine(procedure) + "\n" + KingdomLabRules.ReversibilityLine(),
				Options: KingdomLabRules.ConsentOptions, AllowEscape: true);
			if (consent == 2)
			{
				Record.Exclude(procedure.Key);
				MessageQueue.AddPlayerMessage("{{K|The hall will not offer that again.}}");
				return;
			}
			if (consent != 0)
			{
				return;
			}
			Commission(Building, Actor, System, procedure, Anatomy, At, Kept, City);
		}
	}
}
