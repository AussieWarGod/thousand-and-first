using System;
using System.Collections.Generic;
using System.Globalization;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		public const string AssignmentIdentityProperty = "r_TAF_BenefitDesignation";
		public const string AssignmentRevisionProperty = "r_TAF_BenefitDesignationRevision";

		private void Evaluate(ProviderCandidate Candidate, Zone Z,
			KingdomSurvey Survey, KingdomDesignationIndex Designations)
		{
			GameObject Item = Candidate.Item;
			IKingdomBenefitProvider Provider = Candidate.Provider;
			KingdomBenefitProviderDeclaration Declaration = Candidate.Declaration;
			KingdomBenefitInspection inspection = new KingdomBenefitInspection {
				ProviderKey = Declaration.Key };
			inspection.Offered.AddRange(Declaration.Carries);
			inspection.Tags.AddRange(Declaration.Provides);
			TrackInspection(inspection, Candidate.IdentityBase, Candidate.StableKey);
			if (!TryProviderCell(Item, Declaration.Scope, out Cell cell,
				out GameObject holder, out bool inContainer))
			{
				Fault(inspection, KingdomBenefitFault.ForeignCustody,
					"provider is not one whole physical object on this ground"); return;
			}
			List<KingdomDesignationMatch> matches = Designations.MatchingExact(cell.X, cell.Y,
				Declaration.Scope, inContainer, Declaration.NetworkKey);
			KingdomDesignationMatch match;
			if (!TryAssign(Item, matches, Designations, out match, out KingdomBenefitFault fault,
				out string detail))
			{
				Fault(inspection, fault, detail); return;
			}
			if (!KingdomBenefitEmbodimentRules.ProviderBelongs(true, 1)
				|| !ByIdentity.TryGetValue(match.Designation.Identity, out Aggregate aggregate)
				|| !GameObject.Validate(aggregate.Root))
			{
				Fault(inspection, KingdomBenefitFault.MissingDesignation,
					"designation has no exact live root"); return;
			}
			inspection.DesignationIdentity = match.Designation.Identity;
			aggregate.Reading.Providers.Add(inspection);
			Candidate.Matched = true; Candidate.Match = match;
			Candidate.AssignedAggregate = aggregate;
			Candidate.CustodyValid = Declaration.Scope != KingdomBenefitScope.Container
				|| ReferenceEquals(Item, aggregate.Root)
				|| inContainer && ReferenceEquals(holder, aggregate.Root);
			if (!Candidate.CustodyValid)
			{
				Fault(inspection, KingdomBenefitFault.ForeignCustody,
					"container-scoped provider is not held by the designated container"); return;
			}
			Candidate.AccessObserved = true;
			Candidate.AccessValid = Accessible(aggregate, match, Z);
			if (!Candidate.AccessValid)
			{
				Fault(inspection, KingdomBenefitFault.WrongScope,
					"provider has no live passable route from designated ground"); return;
			}
			Candidate.ShellObserved = true;
			Candidate.ShellValid = ShellValid(aggregate, match, Z);
			if (NeedsCover(Declaration.Scope) && !Candidate.ShellValid)
			{
				Fault(inspection, KingdomBenefitFault.WrongScope,
					"provider's covered scope has no current physical shell"); return;
			}
			int operation = OperationPercent(Item, aggregate.Root, Provider,
				Declaration.Operation, Survey, match.Designation.BuildingKey,
				out detail, out bool unsupported);
			Candidate.OperationObserved = true; Candidate.OperationPercent = operation;
			Candidate.ItemBroken = Item.IsBroken();
			Candidate.RootBroken = aggregate.Root.IsBroken();
			Candidate.ItemConditionPercent = PhysicalConditionPercent(Item);
			Candidate.RootConditionPercent = PhysicalConditionPercent(aggregate.Root);
			if (Declaration.Operation == KingdomBenefitOperation.Custom && Provider != null
				&& !ReproveAfterCustomOperation(Candidate, match, aggregate, Z,
					Designations, out detail))
			{
				Fault(inspection, KingdomBenefitFault.Inoperable,
					detail ?? "custom provider changed its physical evidence"); return;
			}
			inspection.OperationPercent = operation;
			if (operation <= 0)
			{
				Fault(inspection, unsupported ? KingdomBenefitFault.UnsupportedOperation
					: KingdomBenefitFault.Inoperable,
					detail ?? "provider is not functioning"); return;
			}

			KingdomBenefitAllocationClaim claim = new KingdomBenefitAllocationClaim {
				StableKey = Candidate.StableKey + "|operation|"
					+ operation.ToString("D3", CultureInfo.InvariantCulture),
				DesignationIdentity = match.Designation.Identity };
			for (int i = 0; i < Declaration.Carries.Count; i++)
			{
				KindAmount offered = Declaration.Carries[i];
				int amount = KingdomCatalogueRules.Carried(offered.Amount, operation);
				if (amount > 0) claim.ActiveAmounts.Add(new KindAmount(offered.Kind, amount));
			}
			for (int i = 0; i < Declaration.Provides.Count; i++)
			{
				claim.ActiveTags.Add(Declaration.Provides[i]);
			}
			aggregate.Pending.Add(new ProviderEvaluation { Inspection = inspection, Claim = claim });
		}

		private bool ShellValid(Aggregate Aggregate, KingdomDesignationMatch Match, Zone Z)
		{
			if (Match.Cover == KingdomBenefitCover.Open) return true;
			if (Aggregate.Reading.Designation.ProviderId != "taf.architecture") return true;
			if (!Aggregate.ShellRead)
			{
				Aggregate.ShellRead = true;
				Aggregate.ShellValid = KingdomArchitectureStamper.TryVerifyBenefitShell(
					Aggregate.Root, Z, out _);
			}
			return Aggregate.ShellValid;
		}

		private static bool NeedsCover(KingdomBenefitScope Scope)
		{
			return Scope == KingdomBenefitScope.Covered || Scope == KingdomBenefitScope.Interior
				|| Scope == KingdomBenefitScope.Habitable;
		}

		private static void Fault(KingdomBenefitInspection Row, KingdomBenefitFault Fault,
			string Detail)
		{
			Row.Fault = Fault; Row.Detail = Detail;
		}
	}
}
