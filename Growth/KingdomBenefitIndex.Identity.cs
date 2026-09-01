using System;
using System.Globalization;
using System.Text;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private static string ProviderType(IKingdomBenefitProvider Provider)
		{
			string name = Provider?.GetType()?.FullName;
			return Component(string.IsNullOrEmpty(name) ? "unknown-provider" : name, 512);
		}

		private static string ObjectAnchor(GameObject Item)
		{
			string assigned = ExactAssignedId(Item);
			if (assigned != null) return "id|" + Frame(assigned);
			Cell cell = Item?.CurrentCell ?? Item?.InInventory?.CurrentCell;
			GameObject holder = Item?.InInventory;
			StringBuilder result = new StringBuilder("anonymous|");
			result.Append(Frame(Component(Item?.Blueprint ?? "object", 512)));
			result.Append((cell?.X ?? -1).ToString(CultureInfo.InvariantCulture)).Append(',')
				.Append((cell?.Y ?? -1).ToString(CultureInfo.InvariantCulture)).Append('|');
			if (holder == null) result.Append("ground|");
			else
			{
				result.Append("held|");
				string holderId = ExactAssignedId(holder);
				result.Append(Frame(holderId ?? Component(holder.Blueprint ?? "holder", 512)));
			}
			return result.ToString();
		}

		private static string IdentityPrefix(GameObject Item)
		{
			string assigned = ExactAssignedId(Item);
			if (assigned != null) return assigned;
			Cell cell = Item?.CurrentCell ?? Item?.InInventory?.CurrentCell;
			return "<anonymous:" + Component(Item?.Blueprint ?? "object", 256) + "@"
				+ (cell?.X ?? -1).ToString(CultureInfo.InvariantCulture) + ","
				+ (cell?.Y ?? -1).ToString(CultureInfo.InvariantCulture) + ">";
		}

		private static string CandidateStableKey(string ObjectKey, string TypeName,
			KingdomBenefitProviderDeclaration Declaration)
		{
			return "provider|" + Frame(ObjectKey) + Frame(TypeName)
				+ KingdomBenefitAllocationRules.DeclarationKey(Declaration);
		}

		private static string ExactAssignedId(GameObject Item)
		{
			string id = Item?.IDIfAssigned;
			return !string.IsNullOrEmpty(id) && id.Length <= 512 && id.IndexOf('#') < 0
				? id : null;
		}

		private static string Component(string Value, int Maximum)
		{
			if (string.IsNullOrEmpty(Value)) return "none";
			StringBuilder result = new StringBuilder(Math.Min(Value.Length, Maximum));
			for (int i = 0; i < Value.Length && result.Length < Maximum; i++)
				result.Append(char.IsControl(Value[i]) || Value[i] == '#' ? '_' : Value[i]);
			return result.ToString();
		}

		private static string Frame(string Value)
		{
			string value = Value ?? "";
			return value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value + "|";
		}

		private void RecordLoose(string IdentityBase, string StableAnchor,
			KingdomBenefitFault Fault, string Detail, string ProviderKey = null)
		{
			KingdomBenefitInspection row = new KingdomBenefitInspection {
				ProviderKey = ProviderKey, Fault = Fault,
				Detail = KingdomBenefitAllocationRules.BoundDetail(Detail) };
			TrackInspection(row, IdentityBase, StableAnchor);
		}
	}
}
