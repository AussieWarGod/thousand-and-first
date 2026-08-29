using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomRemovalProjectionRuntime
	{
		private static bool TryPrepareExperienceNotes(KingdomSystem System, long Tick,
			List<NativeCivicNotePlan> Notes, out string Failure)
		{
			Failure = null;
			KingdomExperienceLedger ledger = System?.Experience;
			if (Notes == null || ledger == null
				|| !KingdomExperienceRules.TryValidate(ledger, out Failure)) return false;
			for (int i = 0; i < ledger.Offices.Count; i++)
				if (!TryAddExperienceNote(System, Tick, ledger.Offices[i], Notes, out Failure))
					return false;
			for (int i = 0; i < ledger.Remembrances.Count; i++)
				if (!TryAddExperienceNote(System, Tick, ledger.Remembrances[i], Notes, out Failure))
					return false;
			for (int i = 0; i < ledger.Voices.Count; i++)
				if (!TryAddExperienceNote(System, Tick, ledger.Voices[i], Notes, out Failure))
					return false;
			for (int i = 0; i < ledger.FirstFeasts.Count; i++)
				if (!TryAddExperienceNote(System, Tick, ledger.FirstFeasts[i], Notes, out Failure))
					return false;
			return true;
		}

		private static bool TryAddExperienceNote(KingdomSystem System, long Tick, object Row,
			List<NativeCivicNotePlan> Notes, out string Failure)
		{
			if (!TryPrepareNativeCivicNote(System, Tick, Row,
				out NativeCivicNotePlan note, out Failure)) return false;
			Notes.Add(note); return true;
		}

		private static bool TryReadableCivicText(KingdomSystem System, object Row,
			out string Text, out string Failure)
		{
			Text = null; Failure = null;
			if (Row is KingdomCivicOfficeReceipt office)
			{
				string person = Rich(office.Phase == KingdomCivicOfficePhase.Held
					? office.HolderName : office.PredecessorName, "an unnamed citizen");
				Text = person + (office.Phase == KingdomCivicOfficePhase.Held
					? " held its title-only civic office in "
					: " was remembered as a predecessor of the vacant civic office in ")
					+ Rich(office.SettlementName, "an unnamed settlement") + ".";
				return true;
			}
			if (Row is KingdomRemembranceReceipt remembrance)
			{
				Text = Rich(remembrance.SubjectName, "An unnamed citizen") + " had a "
					+ RemembranceClause(remembrance.Phase) + " in "
					+ Rich(remembrance.SettlementName, "an unnamed settlement")
					+ (string.IsNullOrEmpty(remembrance.MournerName) ? "."
						: ", witnessed by " + Rich(remembrance.MournerName, "an unnamed mourner") + ".");
				return true;
			}
			if (Row is KingdomCivicVoiceReceipt voice)
			{
				if (!TrySettlementName(System, voice.SettlementId,
					out string settlement, out Failure)) return false;
				Text = Rich(voice.FirstName, "One citizen") + " and "
					+ Rich(voice.SecondName, "another citizen") + " remembered the "
					+ VoiceClause(voice.Fixture) + " of " + Rich(settlement, "their settlement")
					+ ": " + Rich(voice.Facts, "the exact ruling") + ".";
				return true;
			}
			if (Row is KingdomFirstFeastReceipt feast)
			{
				Text = Rich(feast.ProposerName, "One citizen") + " proposed "
					+ Rich(feast.DishName, "a shared dish") + " in "
					+ Rich(feast.SettlementName, "an unnamed settlement") + " after "
					+ Rich(feast.DeedText, "a remembered deed") + "; the proposal was "
					+ FeastClause(feast.Phase) + ".";
				return true;
			}
			return Fail("civic retirement found an unknown experience row", out Failure);
		}

		private static string CivicText(KingdomSystem System, string Reading, string Digest)
		{
			return new StringBuilder("Before ")
				.Append(Rich(System?.KingdomDisplayName ?? System?.SeatName, "the realm"))
				.Append(" put away its charter, it preserved one exact civic memory.\n")
				.Append(Reading).Append("\nRecord seal: ")
				.Append(Digest.Substring(0, 12)).Append('.').ToString();
		}

		private static bool TrySettlementName(KingdomSystem System, string SettlementId,
			out string Name, out string Failure)
		{
			Name = null; Failure = null;
			if (System == null || string.IsNullOrEmpty(SettlementId)
				|| !System.TryFindSettlement(SettlementId, out bool seated,
					out KingdomSettlement settlement))
				return Fail("civic voice has no exact retirement settlement", out Failure);
			Name = seated ? System.SeatName : settlement?.SettlementName;
			return !string.IsNullOrEmpty(Name)
				|| Fail("civic voice retirement settlement has no name", out Failure);
		}

		private static string Rich(string Text, string Fallback)
		{
			return KingdomPresentation.Rich(string.IsNullOrEmpty(Text) ? Fallback : Text);
		}

		private static string RemembranceClause(KingdomRemembrancePhase Phase)
		{
			switch (Phase)
			{
			case KingdomRemembrancePhase.Projected: return "named memorial";
			case KingdomRemembrancePhase.Lost: return "lost but still recorded memorial";
			case KingdomRemembrancePhase.Declined: return "memorial deliberately declined";
			case KingdomRemembrancePhase.Eligible: return "memorial opportunity left unanswered";
			default: return "bounded remembrance record";
			}
		}

		private static string VoiceClause(KingdomCivicVoiceFixture Fixture)
		{
			switch (Fixture)
			{
			case KingdomCivicVoiceFixture.CreedDeclaration: return "creed declaration";
			case KingdomCivicVoiceFixture.VillageCovenant: return "village covenant";
			case KingdomCivicVoiceFixture.AssentingMoot: return "assenting moot";
			default: return "civic decision";
			}
		}

		private static string FeastClause(KingdomFirstFeastPhase Phase)
		{
			switch (Phase)
			{
			case KingdomFirstFeastPhase.Adopted: return "adopted as a private practice";
			case KingdomFirstFeastPhase.Adapted: return "adapted as a private practice";
			case KingdomFirstFeastPhase.Refused: return "refused";
			case KingdomFirstFeastPhase.Archived: return "archived";
			default: return "left unresolved";
			}
		}
	}
}
