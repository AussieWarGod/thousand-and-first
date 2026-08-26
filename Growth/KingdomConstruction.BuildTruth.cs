using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		/// <summary>Computes build effects once, before first publication/debit.</summary>
		public static bool FreezeBuildTruth(KingdomConstructionJob Job,
			KingdomSystem System, int BaseDefence, bool HasPlot)
		{
			bool tinkering = The.Player != null && The.Player.HasSkill("Tinkering");
			bool advanced = The.Player != null && The.Player.HasSkill("Tinkering_Tinker1");
			int defence = KingdomRules.BuiltDefence(BaseDefence, HasPlot,
				System == null ? null : System.FoundingTerrainBlueprint,
				System == null ? null : System.FoundingRegionName, tinkering, advanced);
			return KingdomConstructionRules.FreezeBuildTruth(Job, HasPlot, defence);
		}

		/// <summary>Stages only frozen receipt truth onto a new projection.</summary>
		public static bool ApplyBuildTruth(GameObject Object, KingdomConstructionJob Job)
		{
			if (!GameObject.Validate(Object)
				|| !KingdomConstructionRules.TryReadBuildTruth(Job, out _,
					out bool frontier, out int defence)) return false;
			if (frontier) Object.SetIntProperty(KingdomPlots.FrontierWorkProperty, 1);
			else Object.RemoveIntProperty(KingdomPlots.FrontierWorkProperty);
			if (defence > 0) Object.SetIntProperty("KingdomDefencePending", defence);
			else Object.RemoveIntProperty("KingdomDefencePending");
			return BuildTruthMatches(Object, Job);
		}

		/// <summary>Authenticates a projection against current receipt truth.</summary>
		public static bool BuildTruthMatches(GameObject Object, KingdomConstructionJob Job)
		{
			if (!GameObject.Validate(Object)
				|| !KingdomConstructionRules.TryReadBuildTruth(Job, out _,
					out bool frontier, out int defence)) return false;
			int frontierMark = Object.GetIntProperty(KingdomPlots.FrontierWorkProperty);
			return (frontierMark == 0 || frontierMark == 1)
				&& (frontierMark == 1) == frontier
				&& Object.GetIntProperty("KingdomDefencePending") == defence;
		}

		/// <summary>Legacy projected work may continue from its durable physical marks. Caller must
		/// independently prove exact object/receipt identity. Unprojected legacy work has no truth.</summary>
		public static bool LegacyProjectedBuildTruthMatches(GameObject Object,
			KingdomConstructionJob Job, bool HasPlot)
		{
			if (!GameObject.Validate(Object) || Job == null || Job.BuildTruthSchema != 0)
				return false;
			int frontier = Object.GetIntProperty(KingdomPlots.FrontierWorkProperty);
			int defence = Object.GetIntProperty("KingdomDefencePending");
			return (frontier == 0 || frontier == 1) && defence >= 0
				&& (frontier == 1) == KingdomRules.IsFrontierWork(defence, HasPlot);
		}

		/// <summary>Legacy improvement payloads did not always distinguish plotted from
		/// single-cell work. Their durable marks still prove every effect that can be applied.</summary>
		public static bool LegacyProjectedBuildTruthMatchesUnknownPlot(GameObject Object,
			KingdomConstructionJob Job)
		{
			if (!GameObject.Validate(Object) || Job == null || Job.BuildTruthSchema != 0)
				return false;
			int frontier = Object.GetIntProperty(KingdomPlots.FrontierWorkProperty);
			int defence = Object.GetIntProperty("KingdomDefencePending");
			return (frontier == 0 || frontier == 1) && defence >= 0
				&& (frontier == 0 || defence > 0);
		}

		/// <summary>Authenticates effects after scaffold/works handover.</summary>
		public static bool FinalBuildTruthMatches(GameObject Object, KingdomConstructionJob Job)
		{
			if (!GameObject.Validate(Object) || Job == null) return false;
			int frontierMark = Object.GetIntProperty(KingdomPlots.FrontierWorkProperty);
			int defenceMark = Object.GetIntProperty("KingdomDefence");
			if (frontierMark != 0 && frontierMark != 1 || defenceMark < 0) return false;
			if (KingdomConstructionRules.TryReadBuildTruth(Job, out _,
				out bool frontier, out int defence))
				return (frontierMark == 1) == frontier && defenceMark == defence;
			if (Job.BuildTruthSchema != 0) return false;
			if (Job.Route == KingdomConstructionRoute.CommissionScaffold
				|| Job.Route == KingdomConstructionRoute.PlanScaffold)
				return (frontierMark == 1) == KingdomRules.IsFrontierWork(defenceMark, false);
			if (Job.Route == KingdomConstructionRoute.PlotCommission
				|| Job.Route == KingdomConstructionRoute.PlotPlan
				|| Job.Route == KingdomConstructionRoute.SocketBuild
				|| Job.Route == KingdomConstructionRoute.SocketConvert)
				return (frontierMark == 1) == KingdomRules.IsFrontierWork(defenceMark, true);
			return Job.Route == KingdomConstructionRoute.Improvement
				&& (frontierMark == 0 || defenceMark > 0);
		}

		/// <summary>
		/// Copies a fully funded operation price onto its exact physical successor before AddObject.
		/// An in-place improvement adds the predecessor's already-frozen cumulative receipt. A
		/// predecessor from before this schema remains explicitly legacy; it is not assigned a price
		/// invented from the current catalogue.
		/// </summary>
		public static bool FreezePaidBuild(GameObject Successor, KingdomConstructionJob Job,
			GameObject ImprovementPredecessor = null)
		{
			if (!GameObject.Validate(Successor) || Job == null) return false;
			KingdomPaidBuildReceipt previous = null;
			if (GameObject.Validate(ImprovementPredecessor))
			{
				int schema = ImprovementPredecessor.GetIntProperty(PaidBuildSchemaProperty);
				if (schema == 0)
				{
					// A pre-receipt standing work stays legacy through its first improvement. The
					// strike path retains the old compatibility fallback instead of fabricating history.
					Successor.RemoveIntProperty(PaidBuildSchemaProperty);
					Successor.RemoveIntProperty(PaidBuildWaterProperty);
					Successor.RemoveStringProperty(PaidBuildMaterialProperty);
					Successor.RemoveStringProperty(PaidBuildWorkProperty);
					return Successor.GetIntProperty(PaidBuildSchemaProperty) == 0;
				}
				if (schema != PaidBuildSchema
					|| !TryReadPaidBuild(ImprovementPredecessor, out previous)) return false;
			}
			if (!KingdomConstructionRules.TryPaidBuildReceipt(Job, previous,
				out KingdomPaidBuildReceipt paid)) return false;
			string material = paid.Material.ToClaimString();
			string work = paid.WorkTicks.ToString(
				global::System.Globalization.CultureInfo.InvariantCulture);
			Successor.SetIntProperty(PaidBuildWaterProperty, paid.Water);
			Successor.SetStringProperty(PaidBuildMaterialProperty, material);
			Successor.SetStringProperty(PaidBuildWorkProperty, work);
			Successor.SetIntProperty(PaidBuildSchemaProperty, PaidBuildSchema);
			return PaidBuildMatches(Successor, Job, ImprovementPredecessor);
		}

		/// <summary>Re-proves the frozen bill after callback-bearing engine operations.</summary>
		public static bool PaidBuildMatches(GameObject Successor, KingdomConstructionJob Job,
			GameObject ImprovementPredecessor = null)
		{
			if (!GameObject.Validate(Successor) || Job == null) return false;
			KingdomPaidBuildReceipt previous = null;
			if (GameObject.Validate(ImprovementPredecessor))
			{
				int schema = ImprovementPredecessor.GetIntProperty(PaidBuildSchemaProperty);
				if (schema == 0)
					return Successor.GetIntProperty(PaidBuildSchemaProperty) == 0;
				if (schema != PaidBuildSchema
					|| !TryReadPaidBuild(ImprovementPredecessor, out previous)) return false;
			}
			if (!KingdomConstructionRules.TryPaidBuildReceipt(Job, previous,
				out KingdomPaidBuildReceipt expected)
				|| !TryReadPaidBuild(Successor, out KingdomPaidBuildReceipt actual)) return false;
			return actual.Water == expected.Water && actual.WorkTicks == expected.WorkTicks
				&& actual.Material.ToClaimString() == expected.Material.ToClaimString();
		}

		/// <summary>Reads only the building's frozen receipt; never consults a catalogue.</summary>
		public static bool TryReadPaidBuild(GameObject Building,
			out KingdomPaidBuildReceipt Receipt)
		{
			Receipt = null;
			if (!GameObject.Validate(Building)
				|| Building.GetIntProperty(PaidBuildSchemaProperty) != PaidBuildSchema
				|| !KingdomMaterialDebitCost.TryParseClaim(
					Building.GetStringProperty(PaidBuildMaterialProperty),
					out KingdomMaterialDebitCost material)
				|| !long.TryParse(Building.GetStringProperty(PaidBuildWorkProperty),
					global::System.Globalization.NumberStyles.None,
					global::System.Globalization.CultureInfo.InvariantCulture, out long work)
				|| work < 0L) return false;
			int water = Building.GetIntProperty(PaidBuildWaterProperty);
			if (water < 0) return false;
			Receipt = new KingdomPaidBuildReceipt(water, work, material);
			return true;
		}
	}
}
