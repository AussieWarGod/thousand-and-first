using System;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal sealed class KingdomPlanMarkerProof
	{
		internal Zone Zone;
		internal Cell Cell;
		internal string MarkerId;
		internal string OwnerKey;
		internal string ZoneId;
		internal string DesignKey;
		internal int StakeX;
		internal int StakeY;
		internal string PlannedSkin;
		internal bool PlannedSkinExact;
		internal bool PlotReceiptFragments;
		internal bool PlotReceiptExact;
		internal string PlotPayload;
		internal KingdomPlotRules.PlotRect PlotRect;
		internal int MainX;
		internal int MainY;
		internal string FrozenBytes;
		internal KingdomPlanReceiptShape ReceiptShape;
		internal string ReceiptId;
	}

	public static partial class KingdomPlanMarker
	{
		public const int ProvenanceSchema = 1;
		public const string ProvenanceSchemaProperty = "r_TAF_PlanOwnerSchema_v1";
		public const string ProvenanceOwnerProperty = "r_TAF_PlanOwner_v1";
		public const string ProvenanceZoneProperty = "r_TAF_PlanZone_v1";
		public const string ProvenanceXProperty = "r_TAF_PlanStakeX_v1";
		public const string ProvenanceYProperty = "r_TAF_PlanStakeY_v1";
		public const string ProvenanceDesignProperty = "r_TAF_PlanDesign_v1";

		private static readonly string[] ProvenanceProperties = new string[]
		{
			ProvenanceSchemaProperty, ProvenanceOwnerProperty, ProvenanceZoneProperty,
			ProvenanceXProperty, ProvenanceYProperty, ProvenanceDesignProperty
		};

		private static readonly string[] PlotReceiptProperties = new string[]
		{
			KingdomPlots.PlanSchemaProperty, KingdomPlots.PlanPayloadProperty,
			KingdomPlots.PlanLabourProperty, KingdomPlots.PlanWaterProperty,
			KingdomPlots.PlanMaterialProperty, KingdomPlots.PlotX1Property,
			KingdomPlots.PlotY1Property, KingdomPlots.PlotX2Property,
			KingdomPlots.PlotY2Property
		};

		private static readonly string[] FrozenProperties = new string[]
		{
			ProvenanceSchemaProperty, ProvenanceOwnerProperty, ProvenanceZoneProperty,
			ProvenanceXProperty, ProvenanceYProperty, ProvenanceDesignProperty,
			KingdomDesign.PlannedSkinProperty, KingdomCeremony.SurveyorsPlanProperty,
			KingdomPlots.PlanSchemaProperty, KingdomPlots.PlanPayloadProperty,
			KingdomPlots.PlanLabourProperty, KingdomPlots.PlanWaterProperty,
			KingdomPlots.PlanMaterialProperty, KingdomPlots.PlotX1Property,
			KingdomPlots.PlotY1Property, KingdomPlots.PlotX2Property,
			KingdomPlots.PlotY2Property, KingdomPlots.BlockAnnouncedProperty
		};

		internal static bool HasProvenanceFragments(GameObject Marker)
		{
			if (Marker == null) return false;
			for (int i = 0; i < ProvenanceProperties.Length; i++)
				if (Marker.HasIntProperty(ProvenanceProperties[i])
					|| Marker.HasStringProperty(ProvenanceProperties[i])) return true;
			return false;
		}

		private static bool TryReadProvenance(GameObject Marker, out string Owner,
			out string ZoneId, out int X, out int Y, out string Design)
		{
			Owner = null; ZoneId = null; X = -1; Y = -1; Design = null;
			r_KingdomPlanMarker part = GameObject.Validate(Marker)
				? Marker.GetPart<r_KingdomPlanMarker>() : null;
			if (part == null || Marker.Blueprint != "r_KingdomPlanMarker"
				|| Marker.HasStringProperty(ProvenanceSchemaProperty)
				|| !Marker.HasIntProperty(ProvenanceSchemaProperty)
				|| Marker.GetIntProperty(ProvenanceSchemaProperty) != ProvenanceSchema
				|| !ExactString(Marker, ProvenanceOwnerProperty, out Owner)
				|| !ExactString(Marker, ProvenanceZoneProperty, out ZoneId)
				|| !ExactInt(Marker, ProvenanceXProperty, out X)
				|| !ExactInt(Marker, ProvenanceYProperty, out Y)
				|| !ExactString(Marker, ProvenanceDesignProperty, out Design)
				|| X < 0 || X > 1023 || Y < 0 || Y > 1023
				|| part.DesignKey != Design) return false;
			return !string.IsNullOrEmpty(Owner) && !string.IsNullOrEmpty(ZoneId)
				&& !string.IsNullOrEmpty(Design);
		}

		private static bool ExactString(GameObject Marker, string Property, out string Value)
		{
			Value = null;
			if (Marker == null || Marker.HasIntProperty(Property)
				|| !Marker.HasStringProperty(Property)) return false;
			Value = Marker.GetStringProperty(Property);
			return Value != null;
		}

		private static bool ExactInt(GameObject Marker, string Property, out int Value)
		{
			Value = 0;
			if (Marker == null || Marker.HasStringProperty(Property)
				|| !Marker.HasIntProperty(Property)) return false;
			Value = Marker.GetIntProperty(Property);
			return true;
		}

		private static bool TryStampProvenance(GameObject Marker, string Owner, string ZoneId,
			int X, int Y, string Design, out string Failure)
		{
			Failure = "The plan provenance could not be frozen exactly.";
			if (!GameObject.Validate(Marker) || HasProvenanceFragments(Marker)
				|| string.IsNullOrEmpty(Owner) || string.IsNullOrEmpty(ZoneId)
				|| string.IsNullOrEmpty(Design) || X < 0 || X > 1023 || Y < 0 || Y > 1023
				|| Marker.GetPart<r_KingdomPlanMarker>()?.DesignKey != Design) return false;
			try
			{
				Marker.SetStringProperty(ProvenanceOwnerProperty, Owner);
				Marker.SetStringProperty(ProvenanceZoneProperty, ZoneId);
				Marker.SetIntProperty(ProvenanceXProperty, X);
				Marker.SetIntProperty(ProvenanceYProperty, Y);
				Marker.SetStringProperty(ProvenanceDesignProperty, Design);
				Marker.SetIntProperty(ProvenanceSchemaProperty, ProvenanceSchema);
			}
			catch (Exception ex)
			{
				ClearProvenance(Marker);
				Failure += " " + ex.Message;
				return false;
			}
			if (!TryReadProvenance(Marker, out string readOwner, out string readZone,
				out int readX, out int readY, out string readDesign)
				|| readOwner != Owner || readZone != ZoneId || readX != X || readY != Y
				|| readDesign != Design)
			{
				ClearProvenance(Marker);
				return false;
			}
			Failure = null;
			return true;
		}

		private static void ClearProvenance(GameObject Marker)
		{
			if (Marker == null) return;
			for (int i = 0; i < ProvenanceProperties.Length; i++)
			{
				try { Marker.RemoveStringProperty(ProvenanceProperties[i]); } catch { }
				try { Marker.RemoveIntProperty(ProvenanceProperties[i]); } catch { }
			}
		}

		private static bool HasPlotReceiptFragments(GameObject Marker)
		{
			for (int i = 0; i < PlotReceiptProperties.Length; i++)
				if (Marker.HasIntProperty(PlotReceiptProperties[i])
					|| Marker.HasStringProperty(PlotReceiptProperties[i])) return true;
			return false;
		}

		private static bool ExactPlotReceiptTypes(GameObject Marker)
		{
			return ExactTypedInt(Marker, KingdomPlots.PlanSchemaProperty)
				&& ExactTypedString(Marker, KingdomPlots.PlanPayloadProperty)
				&& ExactTypedString(Marker, KingdomPlots.PlanLabourProperty)
				&& ExactTypedInt(Marker, KingdomPlots.PlanWaterProperty)
				&& ExactTypedString(Marker, KingdomPlots.PlanMaterialProperty)
				&& ExactTypedInt(Marker, KingdomPlots.PlotX1Property)
				&& ExactTypedInt(Marker, KingdomPlots.PlotY1Property)
				&& ExactTypedInt(Marker, KingdomPlots.PlotX2Property)
				&& ExactTypedInt(Marker, KingdomPlots.PlotY2Property);
		}

		private static bool ExactTypedString(GameObject Marker, string Property)
		{
			return Marker.HasStringProperty(Property) && !Marker.HasIntProperty(Property);
		}

		private static bool ExactTypedInt(GameObject Marker, string Property)
		{
			return Marker.HasIntProperty(Property) && !Marker.HasStringProperty(Property);
		}

		private static bool TryCaptureFrozenBytes(GameObject Marker, out string Frozen)
		{
			Frozen = null;
			if (!TryReadProvenance(Marker, out _, out _, out _, out _, out _)) return false;
			r_KingdomPlanMarker part = Marker.GetPart<r_KingdomPlanMarker>();
			StringBuilder text = new StringBuilder("TAF-PLAN-MARKER-1");
			Append(text, Marker.IDIfAssigned); Append(text, Marker.Blueprint);
			Append(text, Marker.DisplayName); Append(text, Marker.GetPart<Description>()?.Short);
			Append(text, part.DesignKey); text.Append('|').Append(part.PlacedTick)
				.Append('|').Append(part.PlacedOrder);
			for (int i = 0; i < FrozenProperties.Length; i++)
			{
				string property = FrozenProperties[i];
				text.Append('|').Append(Marker.HasStringProperty(property) ? 'S' : '-');
				Append(text, Marker.GetStringProperty(property));
				text.Append('|').Append(Marker.HasIntProperty(property) ? 'I' : '-')
					.Append('|').Append(Marker.GetIntProperty(property));
			}
			Frozen = text.ToString();
			return true;
		}

		private static void Append(StringBuilder Text, string Value)
		{
			Text.Append('|').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(Value ?? "")));
		}
	}
}
