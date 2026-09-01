using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
			if (System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			// Remove the earlier observation before any fallible physical callback. A save or
			// exception in this pass therefore yields zero, never yesterday's now-disproved view.
			if (!KingdomReachObservationRuntime.TryRevokeZone(Z.ZoneID, out string revokeFailure))
			{
				KingdomLog.Log("reach: observation revocation refused (" + revokeFailure + ")");
				return;
			}
			if (!KingdomOffices.Enabled) return;
			List<KindAmount> shaded = new List<KindAmount>();
			List<KindAmount> realm = new List<KindAmount>();
			List<string> authorityRows = new List<string>();
			if (!TryActiveBenefits(Z, Survey, "great-work offices", out var benefits))
			{
				KingdomHostedArcology.RefreshActiveProjection(System, Z, null,
					Survey.StoredWater > 0, out string ignored);
				return;
			}
			if (!KingdomHostedArcology.RefreshActiveProjection(System, Z, benefits,
				Survey.StoredWater > 0, out string overlayFailure))
				KingdomLog.Log("hosted reach refresh refused ("
					+ (overlayFailure ?? "invalid physical observation") + ")");
			IReadOnlyList<KingdomBenefitReading> readings = benefits.Readings;
			if (readings == null || readings.Count > KingdomReachObservationRules.MaxAuthorityRows)
			{
				KingdomLog.Log("reach: designation source exceeds the receipt bound"); return;
			}
			for (int i = 0; i < readings.Count; i++)
			{
				KingdomBenefitReading reading = readings[i];
				if (reading?.Designation == null)
				{
					KingdomLog.Log("reach: designation authority is absent"); return;
				}
				bool found = TryRoot(Z, reading, out GameObject item);
				bool ours = reading.Designation.ProviderId == "taf.architecture"
					|| reading.Designation.ProviderId == "taf.adoption";
				bool live = found && (!ours || KingdomUpgrade.IsFunctionallyBuilt(item));
				KingdomRules.BuildEntry entry = null;
				string key = reading.Designation.BuildingKey;
				bool catalogued = live && !string.IsNullOrEmpty(key)
					&& KingdomData.TryGetBuilding(key, out entry);
				int bandValue = catalogued ? (int)BandOf(item, reading) : -1;
				if (catalogued && KingdomReachRules.RequiresSeat((ReachBand)bandValue))
					UpdateSeat(System, item, entry, Survey.Settlers);
				if (!TryObservationSourceRow(Z, reading, item, found, live, bandValue,
					out string authorityRow))
				{
					KingdomLog.Log("reach: designation authority could not be frozen");
					return;
				}
				authorityRows.Add(authorityRow);
				if (!catalogued || !KingdomReachRules.RequiresSeat((ReachBand)bandValue)
					|| !IsHeaded(item)) continue;
				GatherLive(shaded, reading);
				if (EffectiveBandOf(item, reading) == ReachBand.Realm) GatherLive(realm, reading);
			}
			if (!KingdomReachObservationRuntime.TryWrite(System, Z, shaded, realm,
				authorityRows, The.Game.TimeTicks, out string writeFailure))
				KingdomLog.Log("reach: observation write refused (" + writeFailure + ")");
		}

		private static bool TryObservationSourceRow(Zone Z, KingdomBenefitReading Reading,
			GameObject Root, bool RootFound, bool Live, int Band, out string Row)
		{
			Row = null; KingdomBenefitDesignation d = Reading?.Designation;
			if (Z == null || d == null || d.ZoneId != Z.ZoneID || Reading.Carries == null
				|| Reading.Provides == null || d.Caps == null || d.AcceptedTags == null
				|| d.Cells == null || (RootFound && Root == null)) return false;
			string rootObjectId = RootFound ? Root.IDIfAssigned : null;
			string seatHolder = RootFound ? Root.GetStringProperty(SeatHolderProperty) : null;
			if ((rootObjectId?.Length ?? 0) > KingdomZoneObservationRules.MaxIdentityChars
				|| (seatHolder?.Length ?? 0) > KingdomZoneObservationRules.MaxIdentityChars)
				return false;
			StringBuilder text = new StringBuilder();
			SourceFrame(text, "taf.reach.designation-source/v1");
			SourceFrame(text, d.ProviderId); SourceFrame(text, d.ProviderVersion);
			SourceFrame(text, d.Identity); SourceFrame(text, d.Revision);
			SourceFrame(text, d.ZoneId); SourceFrame(text, d.RootId);
			SourceFrame(text, d.BuildingKey); SourceFrame(text, d.LotId);
			text.Append(RootFound ? '1' : '0').Append('|').Append(Live ? '1' : '0')
				.Append('|').Append(Band.ToString(CultureInfo.InvariantCulture)).Append('|');
			SourceFrame(text, rootObjectId); SourceFrame(text, seatHolder);
			text.Append("caps|").Append(d.Caps.Count.ToString(CultureInfo.InvariantCulture))
				.Append('|');
			for (int i = 0; i < d.Caps.Count; i++) AppendAmount(text, d.Caps[i]);
			text.Append("tags|").Append(d.AcceptedTags.Count.ToString(CultureInfo.InvariantCulture))
				.Append('|');
			for (int i = 0; i < d.AcceptedTags.Count; i++) SourceFrame(text, d.AcceptedTags[i]);
			text.Append("cells|").Append(d.Cells.Count.ToString(CultureInfo.InvariantCulture))
				.Append('|');
			for (int i = 0; i < d.Cells.Count; i++)
			{
				KingdomBenefitCell cell = d.Cells[i];
				text.Append(cell.X.ToString(CultureInfo.InvariantCulture)).Append(',')
					.Append(cell.Y.ToString(CultureInfo.InvariantCulture)).Append(',')
					.Append(((int)cell.Use).ToString(CultureInfo.InvariantCulture)).Append(',')
					.Append(((int)cell.Cover).ToString(CultureInfo.InvariantCulture)).Append('|');
				SourceFrame(text, cell.NetworkKey);
			}
			text.Append("carries|").Append(Reading.Carries.Count.ToString(
				CultureInfo.InvariantCulture)).Append('|');
			for (int i = 0; i < Reading.Carries.Count; i++) AppendAmount(text, Reading.Carries[i]);
			text.Append("provides|").Append(Reading.Provides.Count.ToString(
				CultureInfo.InvariantCulture)).Append('|');
			for (int i = 0; i < Reading.Provides.Count; i++) SourceFrame(text, Reading.Provides[i]);
			if (text.Length > KingdomReachObservationRules.MaxAuthorityRowChars) return false;
			Row = text.ToString(); return true;
		}

		private static void AppendAmount(StringBuilder Text, KindAmount Amount)
		{
			SourceFrame(Text, Amount.Kind);
			Text.Append(Amount.Amount.ToString(CultureInfo.InvariantCulture)).Append('|');
		}

		private static void SourceFrame(StringBuilder Text, string Value)
		{
			string value = Value ?? "";
			Text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
				.Append(value).Append('|');
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
