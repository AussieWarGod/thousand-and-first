using System;
using XRL;
using XRL.World;
using XRL.World.Parts;
using XRL.World.ZoneParts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMoot
	{
		internal static bool ExemptionStillActive(r_KingdomAssentingMootMember Marker,
			GameObject Body)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null || Marker == null || Body == null || (Marker.Roles & 2) == 0
				|| !TryBook(system, Marker.SettlementId, out KingdomCityBook book,
					out bool owned) || !owned) return false;
			book.Normalize();
			KingdomAssentingMootReceipt receipt = book.AssentingMoot;
			string invalid;
			if (!KingdomAssentingMootRules.Validate(receipt, out invalid)
				|| (receipt.Phase != KingdomAssentingMootPhase.Applied
					&& receipt.Phase != KingdomAssentingMootPhase.Prepared)
				|| !MarkerAuthorityMatches(Marker, receipt, Body)) return false;
			int at = receipt.ExemptResidentIds.BinarySearch(Marker.ResidentId);
			if (at < 0 || !string.Equals(receipt.ExemptBodyObjectIds[at],
				Body.IDIfAssigned, StringComparison.Ordinal)) return false;
			GameObject building;
			KingdomAssentingMootContext context;
			if (!TryExactBuilding(receipt, out building)
				|| !TryContext(system, building, out context, out string _)
				|| !BuildingReady(context, receipt, out string _)
				|| !TryMemberBody(context, receipt, KingdomAssentingMootRole.Exemption,
					at, false, out GameObject exact) || !ReferenceEquals(exact, Body)) return false;
			KingdomAssentingWardAuthority ward =
				context.Zone.GetPart<KingdomAssentingWardAuthority>();
			AmbientStabilization native = OwnedNative(context.Zone, ward);
			if (ward == null || !ward.Matches(receipt) || native == null
				|| native.Strength != ward.Strength) return false;
			return receipt.Phase == KingdomAssentingMootPhase.Applied
				? ward.Strength == receipt.Strength
				: ward.Strength > 0 && ward.Strength <=
					KingdomAssentingMootRules.StrengthFor(receipt.AssentResidentIds.Count,
						receipt.ExemptResidentIds.Count);
		}

		internal static string DescriptionLine(GameObject Building)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (!TryContext(system, Building, out KingdomAssentingMootContext context,
				out string _)) return "\n{{K|Ward authority: no exact city receipt answers.}}";
			KingdomAssentingMootReceipt receipt = context.Book.AssentingMoot;
			if (receipt == null || receipt.Phase == KingdomAssentingMootPhase.None)
				return "\n{{K|Ward authority: no voices have been enrolled.}}";
			if (receipt.Phase == KingdomAssentingMootPhase.Quarantined)
				return "\n{{R|Ward authority quarantined: "
					+ KingdomPresentation.Rich(receipt.Fault) + "}}";
			if (receipt.Phase == KingdomAssentingMootPhase.Applied)
				return "\n{{Y|Ward authority: " + receipt.Strength + " strength from "
					+ receipt.AssentResidentIds.Count + " named assents and "
					+ receipt.ExemptResidentIds.Count + " exemptions.}}";
			return "\n{{K|Ward authority suspended: "
				+ KingdomPresentation.Rich(receipt.SuspendedReason) + "}}";
		}
	}
}
