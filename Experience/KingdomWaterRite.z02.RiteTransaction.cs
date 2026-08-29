using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomWaterRite
	{
		// ==================================================================================
		// Holding the rite
		// ==================================================================================

		private static void Hold(KingdomSystem System, Zone Z, GameObject Resident, string RealmCreed, RiteOffer Offer)
		{
			string name = NameOf(Resident);
			string shownName = KingdomPresentation.Rich(name);
			bool closing = KingdomWaterRiteRules.AskedTooOften(Resident.GetIntProperty(RefusalsProperty));
			bool takesTheRoad = KingdomConversionRules.Resents(Offer.Facts.Hostility);
			string prompt = KingdomWaterRiteRules.OfferPrompt(
				shownName,
				KingdomCreed.CreedName(Resident.GetStringProperty(KingdomCreed.CreedProperty)),
				KingdomCreed.CreedName(RealmCreed),
				KingdomPresentation.Rich(System.SeatName),
				Offer.Drams);
			if (closing)
			{
				prompt += KingdomWaterRiteRules.PressedWarning(shownName, takesTheRoad);
			}
			if (Popup.ShowYesNo(prompt) != DialogResult.Yes)
			{
				return;
			}
			WaterRiteAnswer answer = closing
				? default(WaterRiteAnswer)
				: KingdomWaterRiteRules.Answer(Offer.Facts);
			// One survey binds one exact receipt to the dedicated vessels that are standing here
			// now. A stale row in the picker may say the water existed; only this reservation is
			// permission to hold the rite.
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			KingdomWaterDebit debit;
			if (!survey.TryReserveExactWater(Offer.Drams, out debit))
			{
				Popup.Show("The rite requires exactly {{C|" + Offer.Drams
					+ " drams}} from the dedicated stores, and they cannot provide it.");
				return;
			}
			// Last safe point before any answer, stamp or cadence changes. Commit itself is
			// all-or-nothing; a failed commit restores every receipt-bound vessel.
			if (!debit.Commit())
			{
				Popup.Show("The dedicated stores could not yield exactly {{C|" + Offer.Drams
					+ " drams}}. No rite was held.");
				return;
			}
			if (closing)
			{
				Close(System, Resident, RealmCreed, name, shownName, takesTheRoad);
				System.LastSoulRiteTick = (The.Game != null) ? The.Game.TimeTicks : 0L;
				return;
			}
			if (KingdomWaterRiteRules.Converted(answer))
			{
				if (!Accept(System, Z, Resident, RealmCreed, shownName))
				{
					bool returned = debit.Rollback();
					if (!returned)
					{
						MetricsManager.LogError("ThousandAndFirst water rite: conversion failed and the exact "
							+ Offer.Drams + "-dram debit could not be restored: " + (debit.Failure ?? "unknown failure"));
					}
					Popup.Show(returned
						? "The rite did not take hold. Exactly {{C|" + Offer.Drams + " drams}} were returned to the same stores."
						: "The rite did not take hold, and the stores could not be restored exactly. See the game log.");
					return;
				}
				LogRite(name, answer, Offer);
				System.LastSoulRiteTick = (The.Game != null) ? The.Game.TimeTicks : 0L;
				return;
			}
			Refuse(System, Resident, RealmCreed, Offer, answer, shownName);
			LogRite(name, answer, Offer);
			System.LastSoulRiteTick = (The.Game != null) ? The.Game.TimeTicks : 0L;
		}

		private static void LogRite(string Name, WaterRiteAnswer Answer, RiteOffer Offer)
		{
			KingdomLog.Log("water rite: " + Name + " answer=" + Answer
				+ " distance=" + KingdomWaterRiteRules.Distance(Offer.Facts)
				+ " reach=" + KingdomWaterRiteRules.Reach(Offer.Facts.SharedDays)
				+ " poured=" + Offer.Drams);
		}

		private static bool Accept(KingdomSystem System, Zone Z, GameObject Resident, string RealmCreed, string Name)
		{
			// One path for every conversion in the mod: the tally moves, both registers are
			// written in this channel's own words, and the ledger is noted -- all of it there, none
			// of it here, so no two channels can ever tell a conversion differently.
			if (!KingdomConversion.Convert(System, Z, Resident, RealmCreed,
				ConversionChannel.Diplomacy, "share water rite"))
			{
				return false;
			}
			ClearStamp(Resident);
			Resident.SetIntProperty(RefusalsProperty, 0, RemoveIfZero: true);
			Resident.SetStringProperty(AskedTooOftenCreedProperty, null, RemoveIfNull: true);
			Popup.Show(KingdomWaterRiteRules.AcceptNotice(Name, KingdomCreed.CreedName(RealmCreed)));
			return true;
		}

		private static void Refuse(KingdomSystem System, GameObject Resident, string RealmCreed, RiteOffer Offer, WaterRiteAnswer Answer, string Name)
		{
			WriteStamp(Resident, KingdomWaterRiteRules.StampFor(Offer.Facts, Answer));
			KingdomGovernanceScope.Commit("share water rite");
			Resident.SetIntProperty(RefusalsProperty, KingdomWaterRiteRules.RefusalsAfter(Resident.GetIntProperty(RefusalsProperty)));
			Chronicle(System,
				Offer.Facts.Hostility,
				KingdomWaterRiteRules.RefusalTelling(Answer, Name, KingdomPresentation.Rich(System.SeatName)),
				KingdomWaterRiteRules.RefusalRumour(Name, KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(KingdomChronicle.FounderName())));
			Popup.Show(KingdomWaterRiteRules.RefusalNotice(
				Answer,
				Name,
				KingdomCreed.CreedName(Resident.GetStringProperty(KingdomCreed.CreedProperty)),
				KingdomCreed.CreedName(RealmCreed),
				KingdomCreed.CreedName(Offer.ShrineCreed)));
		}

		// The asking that went one too far. The mark shuts the rite for as long as the realm holds
		// this creed, and RepeatedAsking reports it to KingdomConversion, whose own machinery
		// decides whether this person minds enough to take the road -- and, if they do, names them,
		// graces them and chronicles them exactly as every other resented creed is.
		private static void Close(KingdomSystem System, GameObject Resident, string RealmCreed,
			string Name, string ShownName, bool TakesTheRoad)
		{
			Resident.SetStringProperty(AskedTooOftenCreedProperty, RealmCreed);
			KingdomGovernanceScope.Commit("share water rite");
			Chronicle(System,
				KingdomConversionRules.ContestedHostility,
				KingdomWaterRiteRules.ClosedTelling(ShownName, KingdomPresentation.Rich(System.SeatName)),
				KingdomWaterRiteRules.ClosedRumour(ShownName, KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(KingdomChronicle.FounderName())));
			System.Ledger.Note("{{r|" + KingdomWaterRiteRules.ClosedNote(ShownName,
				KingdomCreed.CreedName(RealmCreed)) + "}}");
			Popup.Show(KingdomWaterRiteRules.ClosedNotice(ShownName,
				KingdomPresentation.Rich(System.SeatName), TakesTheRoad));
			KingdomLog.Log("water rite: " + Name + " asked too often about " + (RealmCreed ?? "-") + " road=" + TakesTheRoad);
		}

		// The two registers disagree where the day is contested, and agree where it is not, by
		// exactly the rule KingdomConversion applies to a conversion. A settler who holds nothing
		// in particular saying no is not a thing the roads argue about.
		private static void Chronicle(KingdomSystem System, int Hostility, string Telling, string Rumour)
		{
			if (KingdomConversionRules.Contested(Hostility))
			{
				KingdomChronicle.RecordDisputed(System, Telling, Rumour);
				return;
			}
			KingdomChronicle.Record(System, Telling);
		}

	}
}
