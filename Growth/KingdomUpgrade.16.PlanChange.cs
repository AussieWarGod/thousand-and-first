using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		public static bool BeginPlanChange(KingdomSystem System, Zone Z, GameObject Work,
			KingdomRules.BuildEntry Successor, KingdomSocketTransition Transition,
			out string Failure)
		{
			if (!TryPreparePlanChange(System, Z, Work, Successor, Transition,
				out Assessment assessment, out PreparedImprovement prepared, out Failure))
				return false;
			return BeginPreparedPlanChange(System, Z, Work, assessment, prepared, out Failure);
		}

		/// <summary>Freezes and preflights one declared plan target for preview.</summary>
		public static bool TryPreparePlanChange(KingdomSystem System, Zone Z, GameObject Work,
			KingdomRules.BuildEntry Successor, KingdomSocketTransition Transition,
			out Assessment Assessment, out PreparedImprovement Prepared, out string Failure)
		{
			Assessment = default;
			Prepared = null;
			Failure = null;
			if (System == null || Z == null || !GameObject.Validate(Work) || Successor == null
				|| Transition == null || Transition.ToBuildKey != Successor.Key)
			{
				Failure = "The same-set plan change has no exact endpoints.";
				return false;
			}
			if (!ContentsWouldFit(Work, Successor.Blueprint)
				|| !FounderMarksWouldFit(Work, Successor.Blueprint, out Failure))
			{
				if (Failure == null)
					Failure = "The successor cannot receive everything the standing work holds.";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			if (survey.StoredWater < Transition.WaterDrams)
			{
				Failure = "The change wants {{C|" + Transition.WaterDrams
					+ " drams}}, and the stores cannot bear it.";
				return false;
			}
			if (!KingdomMaterials.CanPayTransition(Z, Transition.Materials, out Failure))
				return false;
			Assessment assessment = new Assessment
			{
				Valid = true, Verdict = KingdomUpgradeRules.UpgradeVerdict.Ready,
				Key = Transition.FromBuildKey, SuccessorKey = Transition.ToBuildKey,
				Successor = Successor, CostDrams = Transition.WaterDrams,
				BuildTicks = Transition.WorkTicks,
				CrewNeeded = Math.Max(1, Successor.Staff), Transition = Transition
			};
			if (!TryPrepareImprovement(System, Z, Work, assessment, out Prepared, out Failure)
				|| Prepared.Legacy || Prepared.Architecture == null)
			{
				if (Failure == null)
					Failure = "The declared plan change has no exact authored target to preview.";
				return false;
			}
			Assessment = assessment;
			return true;
		}

		/// <summary>Commits only the exact plan target already previewed.</summary>
		public static bool BeginPreparedPlanChange(KingdomSystem System, Zone Z, GameObject Work,
			Assessment Assessment, PreparedImprovement Prepared, out string Failure)
		{
			Failure = null;
			if (!Assessment.Valid || Assessment.Transition == null || Prepared == null
				|| Assessment.Successor == null)
			{
				Failure = "The previewed same-set plan change is incomplete.";
				return false;
			}
			if (!ContentsWouldFit(Work, Assessment.Successor.Blueprint)
				|| !FounderMarksWouldFit(Work, Assessment.Successor.Blueprint, out Failure))
			{
				if (Failure == null)
					Failure = "The successor can no longer receive everything the standing work holds.";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			if (survey.StoredWater < Assessment.CostDrams)
			{
				Failure = "The change wants {{C|" + Assessment.CostDrams
					+ " drams}}, and the stores cannot bear it.";
				return false;
			}
			if (!KingdomMaterials.CanPayTransition(Z, Assessment.Transition.Materials,
				out Failure)) return false;
			if (!BeginPrepared(System, Z, Work, Assessment, survey, Prepared))
			{
				Failure = "The declared plan change could not raise its exact previewed scaffold.";
				return false;
			}
			return true;
		}

		private static bool FounderMarksWouldFit(GameObject Work, string SuccessorBlueprint,
			out string Failure)
		{
			Failure = null;
			GameObjectBlueprint blueprint = string.IsNullOrEmpty(SuccessorBlueprint) ? null
				: GameObjectFactory.Factory.GetBlueprintIfExists(SuccessorBlueprint);
			if (blueprint == null)
			{
				Failure = "The successor blueprint is absent, so founder state cannot be handed over.";
				return false;
			}
			if (Work.GetIntProperty(KingdomAdopt.LarderProperty) == 1
				&& !blueprint.HasPart("Inventory"))
			{
				Failure = "The successor has no inventory for the founder's larder dedication.";
				return false;
			}
			if (Work.GetIntProperty(KingdomAdopt.StoresProperty) == 1
				&& !blueprint.HasPart("LiquidVolume"))
			{
				Failure = "The successor has no vessel for the founder's stores dedication.";
				return false;
			}
			return true;
		}

		private static bool TryReadImprovementArchitecture(GameObject Work,
			KingdomConstructionJob Job, out KingdomArchitectureIntent Intent,
			out bool Authored, out string Failure)
		{
			Intent = null;
			Authored = false;
			Failure = null;
			if (!GameObject.Validate(Work) || Job == null)
			{
				Failure = "Improvement architecture endpoints are absent.";
				return false;
			}
			bool v2 = Job.Payload != null
				&& Job.Payload.StartsWith("v2|", StringComparison.Ordinal);
			bool schemaMarker = Work.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Work.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty);
			if (!v2)
			{
				if (schemaMarker)
				{
					KingdomArchitectureIntent standing;
					if (!KingdomArchitectureRuntime.TryRead(Work, out standing, out Failure)) return false;
					if (KingdomArchitectureRules.IsCurrentSnapshotEncoding(
						standing.EncodedSnapshot))
					{
						Failure = "Current authored predecessor lacks its frozen successor payload.";
						return false;
					}
				}
				return string.IsNullOrEmpty(Job.Payload)
					|| Work.GetStringProperty(BuildKeyProperty) == Job.Payload;
			}
			KingdomPlotRules.PlotRect rect;
			string skin;
			bool legacy;
			if (!KingdomPlots.TryDecodePlotPayload(Job.Payload, out rect, out skin,
				out Intent, out legacy, out Failure) || legacy || Intent == null
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(Intent.EncodedSnapshot)
				|| Intent.BuildKey != Job.TargetKey || Job.X != Intent.MainWorldX
				|| Job.Y != Intent.MainWorldY || Work.CurrentZone == null
				|| !schemaMarker)
			{
				if (Failure == null) Failure = "Frozen authored improvement payload is malformed.";
				return false;
			}
			ArchitectureLayoutDelta ignored;
			if (!KingdomArchitectureStamper.TryValidateFrozenUpgrade(Work, Work.CurrentZone,
				Intent, out ignored, out Failure)) return false;
			Authored = true;
			return true;
		}

	}
}
