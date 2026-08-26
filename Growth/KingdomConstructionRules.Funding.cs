using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionRules
	{
		public static KingdomConstructionClaims NewClaims(int Water, KingdomMaterialDebitCost Material)
		{
			int water = Water > 0 ? Water : 0;
			string requested = (Material ?? new KingdomMaterialDebitCost()).ToClaimString();
			return new KingdomConstructionClaims
			{
				WaterRequested = water,
				WaterOutstanding = water,
				Exact = true,
				MaterialRequested = requested,
				MaterialSpent = EmptyCost,
				MaterialOutstanding = requested,
				MaterialLost = EmptyCost
			};
		}

		/// <summary>Freezes complete build effect before a job is first published.</summary>
		public static bool FreezeBuildTruth(KingdomConstructionJob Job, bool HasPlot,
			int FinalDefence)
		{
			if (Job == null || Job.BuildTruthSchema != 0 || Job.BuildHasPlot
				|| Job.BuildFrontier || Job.BuildDefence != 0 || FinalDefence < 0) return false;
			KingdomConstructionJob candidate = Job.Copy();
			candidate.BuildTruthSchema = BuildTruthSchema;
			candidate.BuildHasPlot = HasPlot;
			candidate.BuildFrontier = KingdomRules.IsFrontierWork(FinalDefence, HasPlot);
			candidate.BuildDefence = FinalDefence;
			if (!ValidBuildTruth(candidate)) return false;
			Job.BuildTruthSchema = candidate.BuildTruthSchema;
			Job.BuildHasPlot = candidate.BuildHasPlot;
			Job.BuildFrontier = candidate.BuildFrontier;
			Job.BuildDefence = candidate.BuildDefence;
			return true;
		}

		/// <summary>Reads persisted truth only. Schema zero never authorizes inference.</summary>
		public static bool TryReadBuildTruth(KingdomConstructionJob Job, out bool HasPlot,
			out bool Frontier, out int Defence)
		{
			HasPlot = false;
			Frontier = false;
			Defence = 0;
			if (!ValidBuildTruth(Job) || Job.BuildTruthSchema != BuildTruthSchema) return false;
			HasPlot = Job.BuildHasPlot;
			Frontier = Job.BuildFrontier;
			Defence = Job.BuildDefence;
			return true;
		}

		public static bool RequiresBuildTruth(KingdomConstructionRoute Route)
		{
			return Route == KingdomConstructionRoute.CommissionScaffold
				|| Route == KingdomConstructionRoute.PlanScaffold
				|| Route == KingdomConstructionRoute.PlotCommission
				|| Route == KingdomConstructionRoute.PlotPlan
				|| Route == KingdomConstructionRoute.SocketBuild
				|| Route == KingdomConstructionRoute.SocketConvert
				|| Route == KingdomConstructionRoute.Improvement;
		}

		private static bool ValidBuildTruth(KingdomConstructionJob Job)
		{
			if (Job == null) return false;
			if (Job.BuildTruthSchema == 0)
				return !Job.BuildHasPlot && !Job.BuildFrontier && Job.BuildDefence == 0;
			if (!RequiresBuildTruth(Job.Route)) return false;
			if (Job.Route == KingdomConstructionRoute.CommissionScaffold
				|| Job.Route == KingdomConstructionRoute.PlanScaffold)
			{
				if (Job.BuildHasPlot) return false;
			}
			else if ((Job.Route == KingdomConstructionRoute.PlotCommission
				|| Job.Route == KingdomConstructionRoute.PlotPlan
				|| Job.Route == KingdomConstructionRoute.SocketBuild
				|| Job.Route == KingdomConstructionRoute.SocketConvert) && !Job.BuildHasPlot)
				return false;
			return Job.BuildTruthSchema == BuildTruthSchema && Job.BuildDefence >= 0
				&& Job.BuildFrontier == KingdomRules.IsFrontierWork(
					Job.BuildDefence, Job.BuildHasPlot);
		}

		public static bool FullyFundedExact(KingdomConstructionJob Job)
		{
			KingdomMaterialDebitCost outstanding;
			return Job != null && Job.Claims != null && Job.Claims.Exact
				&& ValidateClaims(Job.Claims)
				&& Job.Claims.WaterOutstanding == 0
				&& KingdomMaterialDebitCost.TryParseClaim(Job.Claims.MaterialOutstanding,
					out outstanding) && outstanding.IsEmpty;
		}

		/// <summary>
		/// Freezes the exact funded claim of one building operation, optionally adding the cumulative
		/// receipt of the predecessor retained by an in-place improvement. Refuses overflow and every
		/// inexact or outstanding claim; a price is never inferred from the live catalogue here.
		/// </summary>
		public static bool TryPaidBuildReceipt(KingdomConstructionJob Job,
			KingdomPaidBuildReceipt Previous, out KingdomPaidBuildReceipt Receipt)
		{
			Receipt = null;
			if (!FullyFundedExact(Job) || Job.DueTick < Job.StartedTick
				|| Job.Claims == null || !KingdomMaterialDebitCost.TryParseClaim(
					Job.Claims.MaterialRequested, out KingdomMaterialDebitCost current)) return false;
			long water = Job.Claims.WaterRequested;
			long work = Job.DueTick - Job.StartedTick;
			KingdomMaterialDebitCost material = current;
			if (Previous != null)
			{
				if (Previous.Water < 0 || Previous.WorkTicks < 0 || Previous.Material == null)
					return false;
				water += Previous.Water;
				if (water > int.MaxValue) return false;
				if (long.MaxValue - work < Previous.WorkTicks) return false;
				work += Previous.WorkTicks;
				if (!TryAddPaidCost(Previous.Material, current, out material)) return false;
			}
			if (water < 0 || water > int.MaxValue || work < 0) return false;
			Receipt = new KingdomPaidBuildReceipt((int)water, work, material);
			return true;
		}

		public static KingdomConstructionClaims ApplyWaterCommit(KingdomConstructionClaims Claims,
			bool Committed, bool RestorationExact)
		{
			KingdomConstructionClaims next = Claims.Copy();
			if (Committed)
			{
				next.WaterSpent = next.WaterRequested;
				next.WaterOutstanding = 0;
				next.WaterLost = next.WaterRequested;
			}
			else if (!RestorationExact)
			{
				next.Exact = false;
			}
			return next;
		}

		/// <summary>Merges one measured debit whose request equals current water outstanding.</summary>
		public static bool TryApplyWaterAttempt(KingdomConstructionClaims Claims,
			int Requested, int Spent, int Outstanding, int Lost, bool Exact,
			out KingdomConstructionClaims Next)
		{
			Next = null;
			if (Claims == null || Requested != Claims.WaterOutstanding || Requested < 0
				|| Spent < 0 || Outstanding < 0 || Lost < Spent
				|| (long)Spent + Outstanding != Requested
				|| (long)Claims.WaterSpent + Spent > int.MaxValue
				|| (long)Claims.WaterLost + Lost > int.MaxValue)
			{
				return false;
			}
			KingdomConstructionClaims next = Claims.Copy();
			next.WaterSpent += Spent;
			next.WaterOutstanding = Outstanding;
			next.WaterLost += Lost;
			next.Exact &= Exact;
			if (!ValidateClaims(next))
			{
				return false;
			}
			Next = next;
			return true;
		}

		public static KingdomConstructionClaims ApplyWaterRollback(KingdomConstructionClaims Claims,
			bool RestoredExact)
		{
			KingdomConstructionClaims next = Claims.Copy();
			if (RestoredExact)
			{
				next.WaterSpent = 0;
				next.WaterOutstanding = next.WaterRequested;
				next.WaterLost = 0;
			}
			else
			{
				next.Exact = false;
			}
			return next;
		}

		/// <summary>Merges a receipt requested against the job's current outstanding claim.</summary>
		public static bool TryApplyMaterial(KingdomConstructionClaims Claims,
			KingdomMaterialDebitResult Result, out KingdomConstructionClaims Next)
		{
			Next = null;
			if (Claims == null || Result == null)
			{
				return false;
			}
			KingdomMaterialDebitCost outstanding;
			if (!KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialOutstanding, out outstanding)
				|| !SameCost(outstanding, Result.Requested))
			{
				return false;
			}
			KingdomMaterialDebitCost spent;
			KingdomMaterialDebitCost lost;
			if (!KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialSpent, out spent)
				|| !KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialLost, out lost))
			{
				return false;
			}
			KingdomConstructionClaims next = Claims.Copy();
			next.MaterialSpent = AddCost(spent, Result.Spent).ToClaimString();
			next.MaterialOutstanding = Result.Outstanding.ToClaimString();
			next.MaterialLost = AddCost(lost, Result.Lost).ToClaimString();
			Next = next;
			return ValidateClaims(next);
		}

	}
}
