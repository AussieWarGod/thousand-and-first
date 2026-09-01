using System.Globalization;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomDelveLink
	{
		/// <summary>
		/// Reads one complete delve only from both already-cached endpoints. Besides the physical
		/// link receipt, this proves the exact authored root and its immutable completion tick.
		/// No zone is generated, thawed, surveyed, or inferred from prose.
		/// </summary>
		public static bool TryReadLoadedCompletion(string HeadZoneId,
			out KingdomDelveLinkReceipt Receipt, out long CompletionTick,
			out string Failure)
		{
			Receipt = null; CompletionTick = -1L; Failure = null;
			if (!TryReadPhysicalReceipt(HeadZoneId, out KingdomDelveLinkReceipt receipt))
				return Fail("the durable delve-link receipt is absent", out Failure);
			if (The.ZoneManager?.CachedZones == null
				|| !The.ZoneManager.CachedZones.TryGetValue(receipt.HeadZoneId, out Zone head)
				|| !The.ZoneManager.CachedZones.TryGetValue(receipt.FootZoneId, out Zone foot)
				|| !ExactPhysicalLinkStands(receipt, head, foot))
				return Fail("both exact loaded delve landings do not stand", out Failure);
			if (FindExactEndpoint(head, receipt.RootId, out GameObject root)
					!= KingdomPhysicalLookupState.Exact
				|| !KingdomUpgrade.IsFunctionallyBuilt(root)
				|| !KingdomDelveRules.IsDelve(root.GetStringProperty(
					KingdomUpgrade.BuildKeyProperty))
				|| !long.TryParse(root.GetStringProperty(
					r_KingdomScaffold.CompletionTickProperty), NumberStyles.Integer,
					CultureInfo.InvariantCulture, out long tick) || tick < 0L)
				return Fail("the exact delve root has no valid completion tick", out Failure);
			Receipt = receipt;
			CompletionTick = tick;
			return true;
		}
	}
}
