using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomSocket
	{
		/// <summary>
		/// Reads what converting <paramref name="Building"/> into <paramref name="NewKey"/> would
		/// take: whether it is even allowed, which of Addendum 2's two verbs it is, and the one
		/// combined figure <see cref="KingdomSocketRules.DescribeConversion"/> composes from it.
		/// Spends nothing. Safe to call for a confirmation popup, and called again by
		/// <see cref="ExecuteConvert"/> itself before anything actually moves, because nothing
		/// here trusts that the world held still between the two calls.
		/// </summary>
		public static bool AssessConvert(KingdomSystem System, Zone Z, GameObject Building, string NewKey, out KingdomSocketRules.ChangeKind Kind, out KingdomSocketRules.ConversionQuote Quote, out string Failure)
		{
			Kind = default(KingdomSocketRules.ChangeKind);
			Quote = default(KingdomSocketRules.ConversionQuote);
			if (!Validate(System, Z, Building, NewKey, out ConvertContext context, out Failure))
			{
				return false;
			}
			Kind = context.Kind;
			Quote = Kind == KingdomSocketRules.ChangeKind.SameSet
				? KingdomSocketRules.AssessPlanChange(context.Transition)
				: KingdomSocketRules.AssessConversion(
					KingdomMaterials.CostFor(context.OldEntry.Key), context.OldEntry.CostDrams,
					KingdomMaterials.CostFor(context.NewEntry.Key), context.NewEntry.CostDrams);
			return true;
		}

		/// <summary>
		/// Resolves and preflights the exact production target before consent. No debit, strike,
		/// marker, or receipt is created here.
		/// </summary>
		private static bool TryPrepareConvert(KingdomSystem System, Zone Z, GameObject Building,
			string NewKey, string NewSkinKey, out PreparedConvert Prepared, out string Failure)
		{
			Prepared = null;
			if (!Validate(System, Z, Building, NewKey, out ConvertContext context, out Failure))
				return false;
			KingdomSocketRules.ConversionQuote quote = context.Kind
				== KingdomSocketRules.ChangeKind.SameSet
				? KingdomSocketRules.AssessPlanChange(context.Transition)
				: KingdomSocketRules.AssessConversion(
					KingdomMaterials.CostFor(context.OldEntry.Key), context.OldEntry.CostDrams,
					KingdomMaterials.CostFor(context.NewEntry.Key), context.NewEntry.CostDrams);
			PreparedConvert prepared = new PreparedConvert
			{
				BuildingId = Building.ID, SkinKey = NewSkinKey, Context = context, Quote = quote
			};
			if (context.Kind == KingdomSocketRules.ChangeKind.SameSet)
			{
				if (!KingdomUpgrade.TryPreparePlanChange(System, Z, Building, context.NewEntry,
					context.Transition, out prepared.Improvement,
					out prepared.PreparedImprovement, out Failure)) return false;
				prepared.Architecture = prepared.PreparedImprovement.Architecture;
				prepared.Payload = prepared.PreparedImprovement.Payload;
				prepared.Delta = prepared.PreparedImprovement.Delta;
			}
			else
			{
				KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
					KingdomMaterials.CostFor(context.NewEntry.Key),
					KingdomMaterials.BitCostFor(context.NewEntry.Key),
					KingdomMaterials.ExoticCostFor(context.NewEntry.Key));
				bool architectureMarker = Building.HasIntProperty(
					KingdomArchitectureRuntime.SchemaProperty)
					|| Building.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty);
				if (architectureMarker)
				{
					KingdomArchitectureIntent standing;
					ArchitectureLayoutSnapshot standingSnapshot;
					string standingLot;
					if (!KingdomArchitectureStamper.TryReadOwner(Building, out standing,
						out standingSnapshot, out standingLot, out Failure)) return false;
					if (KingdomArchitectureRules.IsCurrentSnapshotEncoding(
						standing.EncodedSnapshot))
					{
						prepared.RequiresRestakePreflight = true;
						if (!KingdomArchitectureRuntime.TryPrepare(System, Z, context.TargetRect,
							context.NewEntry.Key, context.NewEntry.Category,
							out prepared.Architecture, out Failure)
							|| !KingdomArchitectureStamper.TryPreflightRestake(System, Z, Building,
								prepared.Architecture, claim, out Failure)
							|| !KingdomPlots.TryEncodePlotPayload(context.TargetRect, NewSkinKey,
								prepared.Architecture, out prepared.Payload, out Failure)) return false;
					}
					else if (!KingdomPlots.TryPreparePlotPayload(System, Z, context.TargetRect,
						context.NewEntry.Key, context.NewEntry.Category, NewSkinKey,
						out prepared.Architecture, out prepared.Payload, out Failure)) return false;
				}
				else if (!KingdomPlots.TryPreparePlotPayload(System, Z, context.TargetRect,
					context.NewEntry.Key, context.NewEntry.Category, NewSkinKey,
					out prepared.Architecture, out prepared.Payload, out Failure)) return false;
			}
			Prepared = prepared;
			return true;
		}

		/// <summary>
		/// Orders the ceremony: pays the new design's full water and material cost right now,
		/// exactly as an ordinary commission would (<see cref="KingdomMaterials.CanPay"/> /
		/// <c>Pay</c>, <c>KingdomGrowth.ConsumeStoredWater</c>, unmodified), then hands the strike
		/// itself to <see cref="KingdomMaterials.OrderStrike"/>, also unmodified. Nothing is built
		/// yet &mdash; the crew has to take the old work down first, on its own ordinary schedule
		/// &mdash; but the whole price is spent and disclosed before any of that begins, which is
		/// what "before anything moves" means here. <see cref="OnCleared"/> is what actually
		/// raises the new design, once the strike finishes.
		/// </summary>
		public static bool ExecuteConvert(KingdomSystem System, Zone Z, GameObject Building, string NewKey, string NewSkinKey, out string Failure)
		{
			if (!TryPrepareConvert(System, Z, Building, NewKey, NewSkinKey,
				out PreparedConvert prepared, out Failure)) return false;
			return ExecutePreparedConvert(System, Z, Building, prepared, out Failure);
		}

		private static bool ExecutePreparedConvert(KingdomSystem System, Zone Z,
			GameObject Building, PreparedConvert Prepared, out string Failure)
		{
			Failure = null;
			if (Prepared == null || !GameObject.Validate(Building)
				|| Building.ID != Prepared.BuildingId
				|| !Validate(System, Z, Building, Prepared.Context.NewEntry.Key,
					out ConvertContext live, out Failure)
				|| live.Kind != Prepared.Context.Kind
				|| live.OldEntry.Key != Prepared.Context.OldEntry.Key
				|| live.NewEntry.Key != Prepared.Context.NewEntry.Key)
			{
				if (Failure == null) Failure = "The previewed conversion is no longer current.";
				return false;
			}
			ConvertContext context = Prepared.Context;
			if (context.Kind == KingdomSocketRules.ChangeKind.SameSet)
			{
				string currentName = KingdomDesign.ReferenceFor(Building, Building.ShortDisplayName);
				if (!KingdomUpgrade.BeginPreparedPlanChange(System, Z, Building,
					Prepared.Improvement, Prepared.PreparedImprovement, out Failure)) return false;
				KingdomGovernanceScope.Commit("change plot plan");
				KingdomChronicle.Record(System, "the founder ordered the " + currentName + " of "
					+ KingdomPresentation.Rich(System.KingdomDisplayName) + " changed in place into "
					+ XRL.Language.Grammar.A(context.NewEntry.Name));
				System.Ledger.Note("{{G|The " + currentName + " keeps its exact lot while its declared plan change is worked.}}");
				MessageQueue.AddPlayerMessage("{{G|The " + currentName + " is ordered changed in place into "
					+ XRL.Language.Grammar.A(context.NewEntry.Name) + ".}}");
				KingdomLog.Log("socket: declared plan change ordered " + context.OldEntry.Key
					+ " -> " + context.NewEntry.Key + " at " + System.SeatName);
				return true;
			}
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				KingdomMaterials.CostFor(context.NewEntry.Key),
				KingdomMaterials.BitCostFor(context.NewEntry.Key),
				KingdomMaterials.ExoticCostFor(context.NewEntry.Key));
			KingdomArchitectureIntent architecture = Prepared.Architecture;
			string payload = Prepared.Payload;
			if (architecture == null || string.IsNullOrEmpty(payload)
				|| (Prepared.RequiresRestakePreflight
					&& !KingdomArchitectureStamper.TryPreflightRestake(System, Z, Building,
						architecture, claim, out Failure))) return false;
			Cell mainCell = Z.GetCell(architecture.MainWorldX, architecture.MainWorldY);
			if (mainCell == null || KingdomConstruction.HasActiveAt(System, Z, mainCell))
			{
				Failure = "The authored successor's main ground already has paid construction in hand.";
				return false;
			}
			if (KingdomGrowth.CountStoredWater(Z) < context.NewEntry.CostDrams)
			{
				Failure = "The work would cost {{C|" + context.NewEntry.CostDrams + " drams}} from the stores, and the stores cannot bear it.";
				return false;
			}
			if (!KingdomMaterials.CanPay(Z, context.NewEntry.Key, out string materialFailure))
			{
				Failure = materialFailure;
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			KingdomWaterDebit water = survey.ReserveExactWater(context.NewEntry.CostDrams);
			KingdomMaterialDebit materials = KingdomMaterials.ReservePayment(Z, context.NewEntry.Key);
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.SocketConvert, mainCell, Building,
				context.NewEntry.Key, payload,
				context.NewEntry.CostDrams, claim);
			if (!KingdomConstruction.FreezeBuildTruth(job, System,
				context.NewEntry.Defence, true))
			{
				water.Rollback();
				materials.Cancel();
				Failure = "The converted plot's exact build effects could not be frozen.";
				return false;
			}
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funding == KingdomConstructionStartResult.Refused)
			{
				Failure = fundingFailure ?? "The stores could not cover the work after all.";
				return false;
			}
			KingdomConstruction.Bind(Building, job);
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				KingdomGovernanceScope.Commit("convert plot");
				System.Ledger.Note("{{r|The conversion receipt remains outstanding. The old work stands until its exact claim is settled.}}");
				return true;
			}
			if (!ProjectConvertOrder(System, Z, Building, context.NewEntry.Key, Prepared.SkinKey,
				job, out job, out string strikeFailure))
			{
				KingdomGovernanceScope.Commit("convert plot");
				System.Ledger.Note("{{r|The paid conversion could not yet be given to the striking crew. Its receipt remains queued.}}");
				KingdomLog.Log("construction: conversion order waits: " + strikeFailure);
				return true;
			}
			KingdomGovernanceScope.Commit("convert plot");
			KingdomSocketRules.ChangeKind kind = context.Kind;
			string verb = KingdomSocketRules.VerbFor(kind);
			string name = Building.ShortDisplayName;
			KingdomChronicle.Record(System, "the founder ordered the " + name + " of " + KingdomPresentation.Rich(System.KingdomDisplayName) + " " + verb + " into " + XRL.Language.Grammar.A(context.NewEntry.Name));
			System.Ledger.Note("{{G|The " + name + " is to become " + XRL.Language.Grammar.A(context.NewEntry.Name) + ". The crew is set to strike it.}}");
			MessageQueue.AddPlayerMessage("{{G|The " + name + " is ordered " + verb + " into " + XRL.Language.Grammar.A(context.NewEntry.Name) + ".}}");
			KingdomLog.Log("socket: convert ordered " + context.OldEntry.Key + " -> " + context.NewEntry.Key + " at " + System.SeatName);
			return true;
		}
	}
}
