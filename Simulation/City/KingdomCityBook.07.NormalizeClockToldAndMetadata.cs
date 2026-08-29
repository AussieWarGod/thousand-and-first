
namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		private void NormalizeClockColumns()
		{
			ClockKinds = Repair(ClockKinds);
			ClockNextDueTicks = Repair(ClockNextDueTicks);
			ClockOrdinals = Repair(ClockOrdinals);
			int clocks = Shortest(new int[3] { ClockKinds.Count, ClockNextDueTicks.Count, ClockOrdinals.Count });
			if (clocks > KingdomCityState.MaxClocks)
			{
				clocks = KingdomCityState.MaxClocks;
			}
			Trim(ClockKinds, clocks);
			Trim(ClockNextDueTicks, clocks);
			Trim(ClockOrdinals, clocks);
		}

		private void NormalizeToldColumns()
		{
			ToldKinds = Repair(ToldKinds);
			ToldTicks = Repair(ToldTicks);
			ToldSubjectsA = Repair(ToldSubjectsA);
			ToldSubjectsB = Repair(ToldSubjectsB);
			ToldPlaceZoneIds = Repair(ToldPlaceZoneIds);
			ToldOutcomes = Repair(ToldOutcomes);
			int told = Shortest(new int[6]
			{
				ToldKinds.Count, ToldTicks.Count, ToldSubjectsA.Count, ToldSubjectsB.Count,
				ToldPlaceZoneIds.Count, ToldOutcomes.Count
			});
			if (told > KingdomCityState.MaxToldEntries)
			{
				// The ring forgets its OLDEST lines, never its newest: a book that came back with
				// more than the ring holds keeps the end of the story.
				DropOldest(ToldKinds, told - KingdomCityState.MaxToldEntries);
				DropOldest(ToldTicks, told - KingdomCityState.MaxToldEntries);
				DropOldest(ToldSubjectsA, told - KingdomCityState.MaxToldEntries);
				DropOldest(ToldSubjectsB, told - KingdomCityState.MaxToldEntries);
				DropOldest(ToldPlaceZoneIds, told - KingdomCityState.MaxToldEntries);
				DropOldest(ToldOutcomes, told - KingdomCityState.MaxToldEntries);
				told = KingdomCityState.MaxToldEntries;
			}
			Trim(ToldKinds, told);
			Trim(ToldTicks, told);
			Trim(ToldSubjectsA, told);
			Trim(ToldSubjectsB, told);
			Trim(ToldPlaceZoneIds, told);
			Trim(ToldOutcomes, told);
		}

		private void NormalizeCityMetadata()
		{
			if (AssentingMoot == null)
			{
				AssentingMoot = new ThousandAndFirst.KingdomAssentingMootReceipt();
			}
			AssentingMoot.Normalize();
			string mootFailure;
			if (AssentingMoot.Phase != ThousandAndFirst.KingdomAssentingMootPhase.None
				&& !ThousandAndFirst.KingdomAssentingMootRules.Validate(
					AssentingMoot, out mootFailure))
			{
				AssentingMoot = ThousandAndFirst.KingdomAssentingMootRules.Quarantined(
					AssentingMoot, mootFailure) ??
					new ThousandAndFirst.KingdomAssentingMootReceipt();
			}
			if (NamedCook == null)
			{
				NamedCook = new ThousandAndFirst.KingdomNamedCookReceipt();
			}
			NamedCook.Normalize();
			string cookFailure;
			if (NamedCook.Phase != ThousandAndFirst.KingdomNamedCookPhase.None
				&& !ThousandAndFirst.KingdomNamedCookRules.Validate(NamedCook, out cookFailure))
			{
				NamedCook = ThousandAndFirst.KingdomNamedCookRules.Quarantined(
					NamedCook, cookFailure) ?? new ThousandAndFirst.KingdomNamedCookReceipt();
			}
			if (SettlementId == null)
			{
				SettlementId = "";
			}
			if (PilgrimLoudness < 0)
			{
				PilgrimLoudness = 0;
			}
			else if (PilgrimLoudness >= ThousandAndFirst.KingdomLocusRules.PilgrimStoryThreshold)
			{
				PilgrimLoudness = ThousandAndFirst.KingdomLocusRules.PilgrimStoryThreshold - 1;
			}
			if (PilgrimSequence < 0)
			{
				PilgrimSequence = 0;
			}
			if (PilgrimCauseTick < 0L)
			{
				PilgrimCauseTick = 0L;
			}
			PilgrimCause = PilgrimCause ?? "";
			PilgrimObjectId = PilgrimObjectId ?? "";
			PilgrimName = PilgrimName ?? "";
			if (PilgrimName.Length > ThousandAndFirst.KingdomLocusRules.MaxPilgrimNameChars)
			{
				PilgrimName = "";
			}
			PilgrimPlaceName = PilgrimPlaceName ?? "";
			if (PilgrimPlaceName.Length > ThousandAndFirst.KingdomLocusRules.MaxPilgrimPlaceChars)
			{
				PilgrimPlaceName = "";
			}
			PilgrimGreeted = PilgrimGreeted == 1 ? 1 : 0;
			if (!ThousandAndFirst.KingdomLocusRules.KnownPilgrimState(PilgrimState)
				|| PilgrimCause.Length > ThousandAndFirst.KingdomLocusRules.MaxPilgrimCauseChars
				|| (PilgrimState != (int)ThousandAndFirst.KingdomLocusRules.PilgrimState.None
					&& (PilgrimSequence <= 0 || PilgrimCauseTick <= 0L
						|| string.IsNullOrWhiteSpace(PilgrimCause)
						|| string.IsNullOrWhiteSpace(PilgrimPlaceName))))
			{
				PilgrimState = (int)ThousandAndFirst.KingdomLocusRules.PilgrimState.None;
				PilgrimCauseTick = 0L;
				PilgrimCause = "";
				PilgrimObjectId = "";
				PilgrimName = "";
				PilgrimPlaceName = "";
				PilgrimGreeted = 0;
			}
			else if (PilgrimState == (int)ThousandAndFirst.KingdomLocusRules.PilgrimState.None)
			{
				PilgrimCauseTick = 0L;
				PilgrimCause = "";
				PilgrimObjectId = "";
				PilgrimName = "";
				PilgrimPlaceName = "";
				PilgrimGreeted = 0;
			}
			else if (PilgrimState == (int)ThousandAndFirst.KingdomLocusRules.PilgrimState.Waiting)
			{
				PilgrimObjectId = "";
				PilgrimGreeted = 0;
			}
			// A stamp below zero is a corrupt reading and not a model in debt: the book fails
			// closed to "nothing reckoned yet" rather than refusing to load a whole city.
			if (ProcessedThroughTick < 0L)
			{
				ProcessedThroughTick = 0L;
			}
			for (int i = 0; i < ZoneIds.Count; i++)
			{
				if (ZoneIds[i] == null)
				{
					ZoneIds[i] = "";
				}
				if (ZoneLastReadTicks[i] < 0L)
				{
					ZoneLastReadTicks[i] = 0L;
				}
			}
		}
	}
}
