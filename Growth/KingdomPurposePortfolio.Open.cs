using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		internal static void OpenPortfolio(GameObject Work, GameObject Actor)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (!KingdomUpgrade.IsFunctionallyBuilt(Work) || Actor == null || system == null
				|| !KingdomPurposePortfolioRules.TryBuildKind(
					KingdomUpgrade.DesignKeyOf(Work), out KingdomPurposeKind kind))
			{
				Popup.Show("This object is not one exact standing purposeful work.");
				return;
			}
			if (!TryReadPortfolioPair(out KingdomPurposePairReceipt pair, out string failure))
			{
				Popup.Show(failure);
				return;
			}
			if (pair == null || pair.Phase == KingdomPurposePairPhase.Dormant)
			{
				OfferPair(system, Work, kind, pair);
				return;
			}
			bool pendingSecond = pair.Phase == KingdomPurposePairPhase.SecondPending
				&& string.IsNullOrEmpty(pair.SecondWorkId)
				&& SecondWorkAnswersCommitment(Work, pair);
			string workId = Work.IDIfAssigned;
			if (string.IsNullOrEmpty(workId)
				|| (pair.FirstWorkId != workId && pair.SecondWorkId != workId && !pendingSecond))
			{
				Popup.Show("This work is outside the current purpose-pair epoch. The register binds {{C|"
					+ KingdomPurposePortfolioRules.PurposeName(pair.FirstKind) + "}} and {{C|"
					+ KingdomPurposePortfolioRules.PurposeName(pair.SecondKind) + "}} exactly.");
				return;
			}
			HandlePortfolioState(system, Work, pair);
		}

		private static string PortfolioStatus(KingdomPurposePairReceipt Pair)
		{
			KingdomPurposeKind acting = Pair.Operation != null ? Pair.Operation.SourceKind
				: Pair.NextKind != KingdomPurposeKind.None ? Pair.NextKind : Pair.FirstKind;
			return "Pair epoch {{C|" + Pair.Epoch + "}}: {{C|"
				+ KingdomPurposePortfolioRules.PurposeName(Pair.FirstKind) + "}} ↔ {{C|"
				+ KingdomPurposePortfolioRules.PurposeName(Pair.SecondKind) + "}}.\n"
				+ "State: {{C|" + Pair.Phase + "}}; bootstrap "
				+ (Pair.BootstrapUsed ? "spent" : "available") + "; return "
				+ (Pair.ReturnUsed ? "spent" : "available") + "."
				+ (Pair.NextKind == KingdomPurposeKind.None ? "" : "\nNext token: {{C|"
					+ KingdomPurposePortfolioRules.PurposeName(Pair.NextKind) + "}}.")
				+ (Pair.Operation == null ? "" : "\nOperation: {{C|"
					+ Pair.Operation.OperationId + "}} at {{C|" + Pair.Operation.Phase + "}}.")
				+ ProvisionState(Pair) + PurposeEffectState(Pair) + DeclaredEffect(acting);
		}

		/// <summary>Committed provision for the operation in flight, and — once delivered — what
		/// can actually be proved about its arrival. Arrival is never inferred from a phase:
		/// landed-ness rides the physical servings in the destination larders, so a delivery is
		/// called landed only when <see cref="TryPurposeProvisionLanded"/> reproves this
		/// operation's whole canonical landing receipt on its exact rooted cargo. A delivery
		/// written before that record existed, or one whose rooted cargo is gone, reports
		/// unverified rather than claiming food that may never have arrived. The accessor reads
		/// the canonical key alone and migrates nothing, so drawing this popup changes no
		/// state.</summary>
		private static string ProvisionState(KingdomPurposePairReceipt Pair)
		{
			if (Pair.Operation == null || !KingdomPurposePortfolioRules.TryRecipe(
				Pair.Operation.SourceKind, Pair.Operation.DestinationKind,
				out KingdomPurposePortfolioRecipe recipe)) return "";
			string carriage = CarriageLine("Provision committed", recipe,
				Pair.Operation.DestinationKind);
			if (carriage.Length == 0
				|| Pair.Operation.Phase != KingdomPurposeOperationPhase.Delivered) return carriage;
			bool proved = TryPurposeProvisionLanded(Pair.Operation, out _, out bool applicable);
			if (!applicable) return carriage;
			return proved ? CarriageLine("Provision landed", recipe,
					Pair.Operation.DestinationKind)
				: carriage + "\nWhether that provision reached the destination larders is proved by those stores, not by this receipt.";
		}

		/// <summary>The one carriage disclosure, rendered only by a row that declares a carry.
		/// Every consent and status surface renders this exact arithmetic from the catalogue row,
		/// so no carried serving is lost unseen. A row that carries nothing transports nothing:
		/// its food stays an ordinary local debit and claims no carriage.</summary>
		private static string CarriageLine(string Lead, KingdomPurposePortfolioRecipe Recipe,
			KingdomPurposeKind Destination)
		{
			if (Recipe == null || Recipe.CarriedFood <= 0) return "";
			return "\n" + Lead + ": {{C|" + Recipe.CarriedFood + " of "
				+ Recipe.FoodServings + " food}} to {{C|"
				+ KingdomPurposePortfolioRules.PurposeName(Destination) + "}}; {{C|"
				+ (Recipe.FoodServings - Recipe.CarriedFood) + "}} lost in carriage.";
		}

		/// <summary>The declared operation of one purposeful work. The three portfolio-only
		/// declarations are filtered out of the dispatch catalogue, so these consent and status
		/// surfaces are the only place their published effect is legible.</summary>
		private static string DeclaredEffect(KingdomPurposeKind Kind)
		{
			if (!TryGetDefinition(KingdomPurposePortfolioRules.BuildKey(Kind),
				out KingdomPurposeDefinition definition)
				|| string.IsNullOrEmpty(definition.Effect)) return "";
			return "\nDeclared operation: " + KingdomPurposePortfolioRules.PurposeName(Kind)
				+ " " + definition.Effect + ".";
		}

		private static string PurposeDigest(params string[] Fields)
		{
			using (SHA256 sha = SHA256.Create())
			{
				string value = string.Join("\n", Fields ?? new string[0]);
				byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
				StringBuilder text = new StringBuilder(64);
				for (int i = 0; i < bytes.Length; i++) text.Append(bytes[i].ToString("x2"));
				return text.ToString();
			}
		}
	}
}
