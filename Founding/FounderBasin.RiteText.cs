using XRL.UI;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_FounderBasin
	{
		private static KingdomFoundingResult Refused()
		{
			return KingdomFoundingResult.From(KingdomFoundingOutcome.Refused,
				KingdomFoundingWaterDisposition.Untouched,
				KingdomFoundingProjection.None);
		}

		private static void ShowFailure(KingdomFoundingResult Result)
		{
			string detail = string.IsNullOrEmpty(Result.Failure)
				? "The rite did not commit."
				: Result.Failure;
			switch (Result.Water)
			{
			case KingdomFoundingWaterDisposition.RestoredExactly:
				Popup.Show(detail + "\n\nThe exact water was restored to this basin. Nothing was founded, sealed, or charged.");
				break;
			case KingdomFoundingWaterDisposition.HeldForRecovery:
				Popup.Show(detail + "\n\nThe pour has already published part of its promise. This basin holds its receipt; use it again on this same ground to finish. No time is charged until it does.");
				break;
			case KingdomFoundingWaterDisposition.RestorationFailed:
				Popup.Show(detail + "\n\nThe basin no longer matches its exact receipt. The rite is left pending and will not draw or charge again.");
				break;
			default:
				Popup.Show(detail + " Nothing has been poured or charged.");
				break;
			}
		}

		private static string CompletionText(KingdomSystem System, KingdomFoundingKind Kind,
			string Name, string Vocation, string VillageDisplayName)
		{
			switch (Kind)
			{
			case KingdomFoundingKind.VillageCharter:
				return "The interrupted covenant is sealed. {{C|" +
					KingdomPresentation.Rich(VillageDisplayName ?? Name ?? "the village") +
					"}} stands with {{C|" +
					KingdomPresentation.Rich(System.KingdomDisplayName) +
					"}}.\n\nLive and drink.";
			case KingdomFoundingKind.SecondCity:
				return "The interrupted pour takes. {{C|" +
					KingdomPresentation.Rich(Name ?? System.SeatName) +
					"}} stands as " + KingdomSettlement.VocationClause(Vocation) +
					", the realm's second city.\n\nLive and drink.";
			default:
				return "The interrupted first pour takes. {{C|" +
					KingdomPresentation.Rich(Name ?? System.KingdomDisplayName) +
					"}} stands, claimed and sealed.\n\nLive and drink.";
			}
		}

		/// <summary>
		/// Asks what the city is for. Every site offers the same readings, including the neutral
		/// one: terrain narrows what a place is good at, never whether it may exist.
		/// </summary>
		/// <param name="Name">The city's name, for the menu title.</param>
		/// <returns>A vocation from <see cref="KingdomSettlement.Vocations"/>, or null if the
		/// founder walked away from the question.</returns>
		private static string AskVocation(string Name)
		{
			string[] vocations = KingdomSettlement.Vocations;
			string[] options = new string[vocations.Length];
			for (int i = 0; i < vocations.Length; i++)
			{
				options[i] = "{{C|" + vocations[i] + "}} — " +
					KingdomSettlement.VocationBlurb(vocations[i]);
			}
			int picked = Popup.PickOption(Title: "What is " +
				KingdomPresentation.Rich(Name) + " for?",
				Intro: "A city is founded for something. Say it now, and the people who come will know what they came for.",
				Options: options, AllowEscape: true);
			if (picked < 0 || picked >= vocations.Length)
			{
				return null;
			}
			return vocations[picked];
		}
	}
}
