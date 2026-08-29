using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static void HandlePortfolioState(KingdomSystem System, GameObject Work,
			KingdomPurposePairReceipt Pair)
		{
			string status = PortfolioStatus(Pair);
			if (Pair.Phase == KingdomPurposePairPhase.Frozen)
			{
				OfferStartOrDissolve(System, Work, Pair, status, "Perform the one bootstrap operation");
				return;
			}
			if (Pair.Phase == KingdomPurposePairPhase.SecondPending)
			{
				if (SecondWorkAnswersCommitment(Work, Pair))
					OfferStartOnly(System, Work, Pair, status,
						"Perform the one reciprocal return operation");
				else Popup.Show(status + "\n\nThe exact second purpose must be commissioned from the delivered bootstrap cargo, then operated at its standing root.");
				return;
			}
			if (Pair.Phase == KingdomPurposePairPhase.Active)
			{
				string expected = Pair.NextKind == Pair.FirstKind
					? Pair.FirstWorkId : Pair.SecondWorkId;
				if (Work.IDIfAssigned == expected)
					OfferStartOrDissolve(System, Work, Pair, status,
						"Perform the next reciprocal operation");
				else OfferDissolve(Pair, status + "\n\nThe next token belongs at the partner work.");
				return;
			}
			if (Pair.Phase == KingdomPurposePairPhase.CargoAwaitingActivation
				|| Pair.Phase == KingdomPurposePairPhase.CargoAwaitingConsumption)
			{
				OfferCredit(System, Work, Pair, status);
				return;
			}
			if (Pair.Phase == KingdomPurposePairPhase.BootstrapOutstanding
				|| Pair.Phase == KingdomPurposePairPhase.ReturnOutstanding
				|| Pair.Phase == KingdomPurposePairPhase.OperationOutstanding)
			{
				OfferRetry(System, Work, Pair, status);
				return;
			}
			if (Pair.Phase == KingdomPurposePairPhase.Quarantined)
				Popup.Show(status + "\n\n{{r|Quarantined: " + Pair.Fault
					+ "}}\nNo mutation is permitted from this surface.");
			else if (Pair.Phase == KingdomPurposePairPhase.Orphaned)
				OfferTopologyReconcile(Pair, status);
			else Popup.Show(status);
		}

		private static void OfferTopologyReconcile(KingdomPurposePairReceipt Pair, string Status)
		{
			int choice = Popup.PickOption(Title: "Purpose topology", Intro: Status
				+ "\n\nThe pair is orphaned by settlement topology. Committed custody remains frozen.",
				Options: new string[] { "Reprove this epoch's settlement topology", "Wait" },
				AllowEscape: true);
			if (choice != 0) return;
			if (!TryReconcilePortfolioTopology(ref Pair, out string failure))
				Popup.Show(failure ?? "The purpose topology cannot be reconciled.");
			else Popup.Show(PortfolioStatus(Pair));
		}

		private static void OfferStartOrDissolve(KingdomSystem System, GameObject Work,
			KingdomPurposePairReceipt Pair, string Status, string StartLabel)
		{
			int choice = Popup.PickOption(Title: "Purpose pair", Intro: Status,
				Options: new string[] { StartLabel, "Dissolve the quiescent pair", "Keep the pair" },
				AllowEscape: true);
			if (choice == 0) ConfirmAndStart(System, Work, Pair);
			else if (choice == 1) DissolvePair(Pair);
		}

		private static void OfferStartOnly(KingdomSystem System, GameObject Work,
			KingdomPurposePairReceipt Pair, string Status, string Label)
		{
			int choice = Popup.PickOption(Title: "Purpose pair", Intro: Status,
				Options: new string[] { Label, "Wait" }, AllowEscape: true);
			if (choice == 0) ConfirmAndStart(System, Work, Pair);
		}

		private static void ConfirmAndStart(KingdomSystem System, GameObject Work,
			KingdomPurposePairReceipt Pair)
		{
			KingdomPurposeKind source = Pair.Phase == KingdomPurposePairPhase.Frozen
				? Pair.FirstKind : Pair.Phase == KingdomPurposePairPhase.SecondPending
					? Pair.SecondKind : Pair.NextKind;
			KingdomPurposeKind destination = source == Pair.FirstKind
				? Pair.SecondKind : Pair.FirstKind;
			if (!KingdomPurposePortfolioRules.TryRecipe(source, destination, out var recipe)) return;
			string operationId = "purpose-op-" + Pair.PairId + "-"
				+ Pair.NextOperationOrdinal;
			if (!TrySelectBodyAuthority(System, Work, The.Player, Pair, source, operationId,
				out string procedureKey, out string procedureReceipt, out string bodyQuote,
				out string selectionFailure))
			{
				if (!string.IsNullOrEmpty(selectionFailure)) Popup.Show(selectionFailure);
				return;
			}
			string prompt = OperationPrompt(recipe, destination, bodyQuote);
			if (Popup.ShowYesNo(prompt) != DialogResult.Yes) return;
			if (!TryStartPortfolioOperation(Work, Pair, procedureKey, procedureReceipt,
				out KingdomPurposePairReceipt started, out string failure))
			{
				Popup.Show(failure ?? "The exact purpose operation could not be frozen.");
				return;
			}
			KingdomGovernanceScope.Commit("start purpose operation");
			DriveAndReport(System, started);
		}

		private static void OfferRetry(KingdomSystem System, GameObject Work,
			KingdomPurposePairReceipt Pair, string Status)
		{
			if (Pair.Operation == null || Pair.Operation.SourceWorkId != Work.IDIfAssigned)
			{
				Popup.Show(Status + "\n\nRetry belongs at the exact source work named by the receipt.");
				return;
			}
			int choice = Popup.PickOption(Title: "Purpose operation", Intro: Status,
				Options: new string[] { "Retry the exact operation", "Wait" }, AllowEscape: true);
			if (choice == 0) DriveAndReport(System, Pair);
		}

		private static void DriveAndReport(KingdomSystem System, KingdomPurposePairReceipt Pair)
		{
			bool advanced = DrivePortfolioOperation(System, Pair,
				out KingdomPurposePairReceipt published, out string failure);
			if (published != null && Pair != null && published.Revision != Pair.Revision)
				KingdomGovernanceScope.Commit("advance purpose operation");
			if (!advanced) Popup.Show(failure ?? "The exact purpose operation waits for repair.");
			else Popup.Show(PortfolioStatus(published));
		}

		private static void OfferCredit(KingdomSystem System, GameObject Work,
			KingdomPurposePairReceipt Pair, string Status)
		{
			// No pause gate here by ruling: the cargo has already physically arrived, so crediting
			// it completes committed work rather than starting new work, and a paused realm must
			// leave committed recovery resumable. Brand-new work on this surface is still refused,
			// downstream and once: the activating branch reaches TryPortfolioOperationPreflight
			// before it publishes. Neither this path nor AcceptPortfolioCredit reads a clock, so
			// no disabled span can be billed as work on resume.
			int choice = Popup.PickOption(Title: "Purpose cargo", Intro: Status,
				Options: new string[] { Pair.Phase == KingdomPurposePairPhase.CargoAwaitingActivation
					? "Consume the return and perform the activating operation"
					: "Acknowledge the exact delivered cargo", "Wait" },
				AllowEscape: true);
			if (choice != 0) return;
			string procedureKey = null;
			string procedureReceipt = null;
			if (Pair.Phase == KingdomPurposePairPhase.CargoAwaitingActivation)
			{
				string operationId = "purpose-op-" + Pair.PairId + "-"
					+ Pair.NextOperationOrdinal;
				if (!TrySelectBodyAuthority(System, Work, The.Player, Pair, Pair.FirstKind,
					operationId, out procedureKey, out procedureReceipt, out string bodyQuote,
					out string selectionFailure))
				{
					if (!string.IsNullOrEmpty(selectionFailure)) Popup.Show(selectionFailure);
					return;
				}
				if (!KingdomPurposePortfolioRules.TryRecipe(Pair.FirstKind, Pair.SecondKind,
					out var recipe) || Popup.ShowYesNo(OperationPrompt(recipe, Pair.SecondKind,
					bodyQuote)) != DialogResult.Yes) return;
			}
			if (!AcceptPortfolioCredit(Work, Pair, procedureKey, procedureReceipt,
				out KingdomPurposePairReceipt published, out string failure))
				Popup.Show(failure ?? "This work cannot acknowledge the exact cargo.");
			else
			{
				KingdomGovernanceScope.Commit(Pair.Phase
					== KingdomPurposePairPhase.CargoAwaitingActivation
					? "start purpose activation" : "acknowledge purpose cargo");
				if (Pair.Phase == KingdomPurposePairPhase.CargoAwaitingActivation)
					DriveAndReport(System, published);
				else Popup.Show(PortfolioStatus(published));
			}
		}

		private static string OperationPrompt(KingdomPurposePortfolioRecipe Recipe,
			KingdomPurposeKind Destination, string BodyQuote)
		{
			return "Perform one exact {{C|" + Recipe.CargoName + "}} operation?\n\n"
				+ "Local debit: {{C|" + Recipe.WaterDrams + " drams, "
				+ ParseClaim(Recipe.MaterialClaim).Materials.Describe()
				+ (Recipe.FoodServings > 0 ? ", " + Recipe.FoodServings + " food" : "")
				+ "}}." + CarriageLine("Provision carried", Recipe, Destination)
				+ PurposeEffectDisclosure(Recipe.Source)
				+ "\n" + (string.IsNullOrEmpty(BodyQuote) ? "" : "Additional existing body service:\n"
					+ BodyQuote + "\n")
				+ "Destination: {{C|" + KingdomPurposePortfolioRules.PurposeName(Destination)
				+ "}}." + DeclaredEffect(Recipe.Source)
				+ "\n\nThe receipt proves the local debit and bounded-effect charge together before the first physical callback. A retry advances the same operation; it never substitutes or charges twice. Outgoing cargo is frozen only at its own checkpoint.";
		}

		private static void OfferDissolve(KingdomPurposePairReceipt Pair, string Status)
		{
			int choice = Popup.PickOption(Title: "Purpose pair", Intro: Status,
				Options: new string[] { "Keep the pair", "Dissolve the quiescent pair" },
				AllowEscape: true);
			if (choice == 1) DissolvePair(Pair);
		}

		private static void DissolvePair(KingdomPurposePairReceipt Pair)
		{
			if (Popup.ShowYesNo("Dissolve this pair without refund? Both shells remain standing but dormant. A later pair receives a new epoch; old cargo remains a physical but inert token and never becomes civic stock.")
				!= DialogResult.Yes) return;
			if (Pair == null || Pair.Revision == int.MaxValue)
			{
				Popup.Show("The purpose pair exhausted its exact revision range; no cargo disposition changed.");
				return;
			}
			if (!TryReconcilePortfolioTopology(ref Pair, out string topologyFailure))
			{
				Popup.Show(topologyFailure ?? "The purpose topology cannot be reconciled.");
				return;
			}
			// Disposition runs before the CAS, never after. A crash here leaves the receipt exactly
			// as it was and the delivered cargo still findable by the zone scan every reader uses
			// (FindExactKnown), so dissolving again re-converges and releasing again finds nothing.
			// Releasing after a successful publish would strand the entry beyond recovery, because
			// the dormant receipt no longer names the cargo it was rooted under.
			if (KingdomPurposePortfolioRules.TryDecodeCargo(Pair.CreditCargoReceipt,
				out KingdomPurposeCargoReceipt credit)) RemovePurposeCargoRoots(credit);
			KingdomPurposePairReceipt dormant = Pair.Copy();
			dormant.Phase = KingdomPurposePairPhase.Dormant;
			dormant.Operation = null;
			dormant.NextKind = KingdomPurposeKind.None;
			dormant.CreditCargoId = null;
			dormant.CreditCargoReceipt = null;
			dormant.ResumePhase = KingdomPurposePairPhase.Invalid;
			dormant.Revision++;
			if (!TryPublishPortfolioPair(Pair, dormant, out string failure)) Popup.Show(failure);
			else KingdomGovernanceScope.Commit("dissolve purpose pair");
		}
	}
}
