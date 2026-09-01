using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomFaith
	{
		// ==================================================================================
		// The attended pass: shrine conversion, and both channels' 7b lapse lines.
		// ==================================================================================

		/// <summary>
		/// The kingdom's one attended pass over this zone's faith and knowledge buildings. Call
		/// from <c>KingdomSystem.HandleEvent(ZoneActivatedEvent)</c>, after growth has resolved
		/// this pass's staffing (<see cref="StaffedProperty"/> must already be current) and after
		/// creed has resolved this pass's dissent (residents' own creeds are stable facts by
		/// then). Wrapped by the caller's own <c>Guard</c>, like every other module's pass.
		/// </summary>
		/// <param name="System">The kingdom. Unfounded or a zone the realm does not claim does
		/// nothing.</param>
		/// <param name="Z">The activated zone.</param>
		/// <param name="Survey">This pass's already-taken survey and immutable physical-benefit
		/// snapshot.</param>
		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || !System.Founded || Z == null || Survey == null
				|| The.Game == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long now = The.Game.TimeTicks;
			KingdomElapsedOptionDecision option = ObserveOption(System, Z, now);
			if (!option.Valid) return;
			if (option.Action == KingdomElapsedOptionAction.AnchorDisabled)
			{
				if (option.Transition == KingdomElapsedOptionTransition.Disabled
					|| option.Transition == KingdomElapsedOptionTransition.InitializedDisabled)
					CancelUncommittedFaith(Survey);
				CommitOption(System, Z, option.Record);
				return;
			}
			if (option.Action == KingdomElapsedOptionAction.AnchorEnabled)
			{
				if (option.Transition == KingdomElapsedOptionTransition.Enabled)
					ResumeCanceledFaith(Survey, now);
				else
					AnchorPreservedFaith(Survey, now);
				CommitOption(System, Z, option.Record);
				return;
			}
			if (option.Action != KingdomElapsedOptionAction.Run) return;
			HashSet<GameObject> claimed = new HashSet<GameObject>();
			if (!KingdomCapabilityRuntime.TryIndex(Z, Survey, "faith pass",
				out KingdomBenefitIndex benefits))
			{
				ForgetUnreached(System, Z, Survey, claimed); return;
			}
			IReadOnlyList<KingdomBenefitReading> readings = benefits.Readings;
			for (int i = 0; i < readings.Count; i++)
			{
				KingdomBenefitReading reading = readings[i];
				if (!KingdomReach.TryRoot(Z, reading, out GameObject work)
					|| !KingdomData.TryGetBuilding(reading.Designation.BuildingKey,
						out KingdomRules.BuildEntry entry)) continue;
				if (KingdomBenefitCapabilities.Has(reading,
					KingdomBenefitCapabilities.Shrine))
					RunShrine(System, Z, Survey, work, entry, claimed);
				if (KingdomBenefitCapabilities.Accepts(reading,
					KingdomBenefitCapabilities.Education))
					RunEducationLapse(work, entry, KingdomBenefitCapabilities.Has(reading,
						KingdomBenefitCapabilities.Education));
			}
			ForgetUnreached(System, Z, Survey, claimed);
		}

		// Rule 2 of the brink, for the settlers no shrine spoke to at all this pass: the building
		// that had them at the end of its road was struck, deconsecrated, unstaffed, or simply no
		// longer reaches where they stand, so the pressure is gone and the brink goes with it.
		// Without this sweep a shrine brink would outlive its shrine, which is the exact failure
		// IConversionPressure's re-derive-every-pass contract exists to forbid.
		private static void ForgetUnreached(KingdomSystem System, Zone Z, KingdomSurvey Survey, HashSet<GameObject> Claimed)
		{
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (Claimed.Contains(settler))
				{
					continue;
				}
				LiftShrineBrink(System, Z, settler);
				if (settler.GetIntProperty(ShrinePullProperty) != 0)
				{
					settler.SetIntProperty(ShrinePullProperty, 0);
				}
				if (settler.GetLongProperty(ShrinePullTickProperty) != 0L)
					settler.SetLongProperty(ShrinePullTickProperty, 0L);
				if (settler.GetIntProperty(ShrineDisabledActiveProperty) != 0)
					settler.SetIntProperty(ShrineDisabledActiveProperty, 0);
				if (settler.GetLongProperty(ShrineWindowAnchorProperty) != 0L)
					settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
			}
		}

		// Lifts a standing shrine brink and unsays it. A creed brink reached through any other
		// channel is not this file's to touch -- KingdomConversion spends and arrests those.
		private static bool LiftShrineBrink(KingdomSystem System, Zone Z, GameObject Settler)
		{
			BrinkRecord brink = KingdomBrink.Of(Settler, BrinkKind.Creed);
			if (!brink.Stands || brink.Channel != (int)ConversionChannel.Shrine)
			{
				if (Settler != null && Settler.GetLongProperty(ShrineWindowAnchorProperty) != 0L)
					Settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
				return false;
			}
			bool wasWarned = brink.Warned;
			KingdomBrink.Lift(Settler, BrinkKind.Creed);
			Settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
			if (wasWarned)
			{
				// Only what was actually said is unsaid.
				KingdomBrink.Unsay(System, BrinkKind.Creed, NameOf(Settler), KingdomWord.StandsIn(Z), System.SeatName);
			}
			return true;
		}

		private static void RunShrine(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Shrine, KingdomRules.BuildEntry Entry, HashSet<GameObject> Claimed)
		{
			string shrineCreed = Shrine.GetStringProperty(ShrineCreedProperty);
			if (string.IsNullOrEmpty(shrineCreed)
				|| !KingdomData.CreedUsesTheology(shrineCreed))
			{
				// Not applicable: an unconsecrated shrine has no pass to run and says nothing
				// (STANDARDS 7b's other kind of early return).
				return;
			}
			bool staffed = Shrine.GetIntProperty(StaffedProperty) == 1;
			if (!staffed)
			{
				if (Shrine.GetIntProperty(ShrineLapsedAnnouncedProperty) != 1)
				{
					Shrine.SetIntProperty(ShrineLapsedAnnouncedProperty, 1);
					MessageQueue.AddPlayerMessage(KingdomFaithRules.ShrineLapsedLine(Entry.Name, KingdomCreed.CreedName(shrineCreed)));
				}
				return;
			}
			Shrine.SetIntProperty(ShrineLapsedAnnouncedProperty, 0);
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (Claimed.Contains(settler))
				{
					continue;
				}
				// Addendum 6: a shrine draws whoever is IN ITS REACH, which for an S or M plot is
				// its own quarter -- the cluster of built ground it stands in, measured, not the
				// whole zone. Before the claim, so a settler this shrine cannot reach is still
				// there for the shrine in their own quarter.
				if (!KingdomReach.Reaches(System, Z, Shrine, settler))
				{
					continue;
				}
				Claimed.Add(settler);
				string residentCreed = settler.GetStringProperty(KingdomCreed.CreedProperty);
				int hostility = KingdomCreed.HostilityBetween(residentCreed, shrineCreed);
				KingdomFaithRules.ShrineStance stance = KingdomFaithRules.ClassifyStance(residentCreed, shrineCreed, hostility);
				switch (stance)
				{
				case KingdomFaithRules.ShrineStance.Neutral:
					AdvancePull(System, Z, settler, shrineCreed, Entry.Name);
					break;
				case KingdomFaithRules.ShrineStance.Opposed:
					ForgetPull(System, Z, settler);
					HandOffOpposedPressure(System, Z, settler, shrineCreed);
					break;
				default:
					ForgetPull(System, Z, settler);
					break;
				}
			}
		}

		// Clears a settler's pull and any shrine brink standing over them, because the shrine has
		// stopped arguing at them -- they took a creed, or they came to oppose it.
		private static void ForgetPull(KingdomSystem System, Zone Z, GameObject Settler)
		{
			LiftShrineBrink(System, Z, Settler);
			if (Settler.GetIntProperty(ShrinePullProperty) != 0)
			{
				Settler.SetIntProperty(ShrinePullProperty, 0);
			}
			if (Settler.GetLongProperty(ShrinePullTickProperty) != 0L)
				Settler.SetLongProperty(ShrinePullTickProperty, 0L);
			if (Settler.GetIntProperty(ShrineDisabledActiveProperty) != 0)
				Settler.SetIntProperty(ShrineDisabledActiveProperty, 0);
			if (Settler.GetLongProperty(ShrineWindowAnchorProperty) != 0L)
				Settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
		}

	}
}
