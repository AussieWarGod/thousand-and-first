using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomReach
	{
		/// <summary>
		/// The kingdom's one attended pass over this zone's great works: fills, keeps, or passes
		/// the seat each one is, and records what the headed ones shade the city with. Call from
		/// <c>KingdomSystem.HandleEvent(ZoneActivatedEvent)</c> after growth has resolved this
		/// pass's staffing and after <c>KingdomOffices.OnZoneActivated</c>, so the settlement's
		/// own office is settled before its buildings' are. Wrapped by the caller's own
		/// <c>Guard</c>, like every other module's pass.
		/// </summary>
		/// <param name="System">The kingdom. Unfounded, or a zone the realm does not claim, does
		/// nothing.</param>
		/// <param name="Z">The activated zone.</param>
		/// <param name="Survey">This pass's already-taken survey, for its <c>Settlers</c>.</param>
		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!KingdomOffices.Enabled || System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			List<KindAmount> shaded = new List<KindAmount>();
			List<KindAmount> realm = new List<KindAmount>();
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject item = Survey.Built[i];
				KingdomRules.BuildEntry entry;
				string key = KingdomUpgrade.DesignKeyOf(item);
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
				{
					continue;
				}
				ReachBand band = BandOf(key);
				if (!KingdomReachRules.RequiresSeat(band))
				{
					continue;
				}
				UpdateSeat(System, item, entry, Survey.Settlers);
				if (IsHeaded(item))
				{
					Gather(shaded, entry, item);
					if (EffectiveBandOf(item) == ReachBand.Realm)
					{
						Gather(realm, entry, item);
					}
				}
			}
			Record(Z, shaded, realm);
		}

		private static void UpdateSeat(KingdomSystem System, GameObject Work, KingdomRules.BuildEntry Entry, List<GameObject> Settlers)
		{
			string held = Work.GetStringProperty(SeatHolderProperty);
			// A holder the roster no longer carries is a holder who died, was exiled, or walked
			// out of the settlement: the seat is empty however far away their object may still be
			// standing. A holder the roster keeps but who is not in this zone this pass keeps the
			// seat, exactly as the settlement's own office does.
			Simulation.City.KingdomResidentRow heldRow;
			if (!string.IsNullOrEmpty(held)
				&& !Simulation.City.KingdomResidents.TryFindByName(System, held, out heldRow))
			{
				Vacate(System, Work, Entry, held);
				held = null;
			}
			string title = KingdomReachRules.SeatTitle(Entry.Category);
			int bestScore = -1;
			GameObject best = null;
			int heldScore = -1;
			for (int i = 0; (Settlers != null) && i < Settlers.Count; i++)
			{
				GameObject settler = Settlers[i];
				int score = FitnessOf(Entry.Category, settler);
				string name = settler.GetStringProperty("KingdomName");
				if (!string.IsNullOrEmpty(held) && name == held)
				{
					heldScore = score;
					continue;
				}
				if (score > bestScore || (score == bestScore && Tenure(System, name) < Tenure(System, NameOf(best))))
				{
					bestScore = score;
					best = settler;
				}
			}
			if (!string.IsNullOrEmpty(held))
			{
				// The seated notable's own score is re-read while they are here, so a challenger
				// is measured against the person actually sitting there.
				if (heldScore >= 0)
				{
					Work.SetIntProperty(SeatScoreProperty, heldScore);
				}
				if (best == null || !KingdomReachRules.ShouldUnseat(Work.GetIntProperty(SeatScoreProperty), bestScore))
				{
					return;
				}
				Seat(System, Work, Entry, best, bestScore, title, KingdomOfficeRules.OfficeTransition.Passed);
				return;
			}
			if (best == null)
			{
				Unheaded(Work, Entry, title);
				return;
			}
			Seat(System, Work, Entry, best, bestScore, title, KingdomOfficeRules.OfficeTransition.FirstHolder);
		}

		private static void Seat(KingdomSystem System, GameObject Work, KingdomRules.BuildEntry Entry, GameObject Holder, int Score, string Title, KingdomOfficeRules.OfficeTransition Transition)
		{
			string name = NameOf(Holder);
			if (string.IsNullOrEmpty(name))
			{
				return;
			}
			Work.SetStringProperty(SeatHolderProperty, name);
			Work.SetStringProperty(SeatTitleProperty, Title);
			Work.SetIntProperty(SeatScoreProperty, (Score < 0) ? 0 : Score);
			Work.SetIntProperty(SeatUnheadedAnnouncedProperty, 0);
			Holder.RequirePart<SocialRoles>().RequireRole(Title + " of " + Entry.Name);
			KingdomChronicle.Record(System, KingdomReachRules.SeatChronicle(Transition, Title, name, Entry.Name));
			MessageQueue.AddPlayerMessage(KingdomReachRules.SeatMessage(Transition, Title, name, Entry.Name));
			KingdomLog.Log("reach: seat " + Transition + " title=" + Title + " holder=" + name + " work=" + Entry.Key);
		}

		private static void Vacate(KingdomSystem System, GameObject Work, KingdomRules.BuildEntry Entry, string Held)
		{
			string title = SeatTitleOf(Work);
			if (title.Length == 0)
			{
				title = KingdomReachRules.SeatTitle(Entry.Category);
			}
			Work.SetStringProperty(SeatHolderProperty, null, RemoveIfNull: true);
			Work.SetIntProperty(SeatScoreProperty, 0);
			KingdomChronicle.Record(System, KingdomReachRules.SeatChronicle(KingdomOfficeRules.OfficeTransition.Vacant, title, Held, Entry.Name));
			MessageQueue.AddPlayerMessage(KingdomReachRules.SeatMessage(KingdomOfficeRules.OfficeTransition.Vacant, title, Held, Entry.Name));
			KingdomLog.Log("reach: seat vacated title=" + title + " was=" + Held + " work=" + Entry.Key);
		}

		private static void Unheaded(GameObject Work, KingdomRules.BuildEntry Entry, string Title)
		{
			if (Work.GetIntProperty(SeatUnheadedAnnouncedProperty) == 1)
			{
				return;
			}
			Work.SetIntProperty(SeatUnheadedAnnouncedProperty, 1);
			MessageQueue.AddPlayerMessage(KingdomReachRules.UnheadedLine(Entry.Name, Title));
			KingdomLog.Log("reach: unheaded work=" + Entry.Key + " title=" + Title);
		}

	}
}
