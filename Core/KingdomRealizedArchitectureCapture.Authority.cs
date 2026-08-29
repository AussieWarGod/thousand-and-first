using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRealizedArchitectureCapture
	{
		/// <summary>Highest authored layer ordinal. Ground 0, Structure 1, Object 2.</summary>
		private const int MaxAuthoredLayer = 2;

		/// <summary>
		/// Markers exclusive to the architecture stamper, used to notice that an object is claiming
		/// component authority at all. Plot markers are deliberately absent: ordinary plot ground
		/// carries them lawfully and is not a component.
		/// </summary>
		private static readonly string[] IntMarkers =
		{
			KingdomArchitectureStamper.ComponentSchemaProperty,
			KingdomArchitectureStamper.ComponentLayerProperty,
			KingdomArchitectureStamper.ComponentExistingProperty,
			KingdomArchitectureStamper.ComponentCarriedProperty
		};

		/// <summary>Every component marker the stamper writes as text.</summary>
		private static readonly string[] TextMarkers =
		{
			KingdomArchitectureStamper.ComponentSlotProperty,
			KingdomArchitectureStamper.ComponentAnchorProperty,
			KingdomArchitectureStamper.ComponentHashProperty,
			KingdomArchitectureStamper.ComponentTokenProperty
		};

		/// <summary>
		/// Every authority key whose type table is audited on an exact component: the stamper's own
		/// markers plus the plot custody pair. None of these may be read through a default getter,
		/// because a key living under the wrong table would answer as a lawful default.
		/// </summary>
		private static readonly string[] AuditedIntKeys =
		{
			KingdomArchitectureStamper.ComponentSchemaProperty,
			KingdomArchitectureStamper.ComponentLayerProperty,
			KingdomArchitectureStamper.ComponentExistingProperty,
			KingdomArchitectureStamper.ComponentCarriedProperty,
			KingdomPlots.PlotPartProperty
		};

		private static readonly string[] AuditedTextKeys =
		{
			KingdomArchitectureStamper.ComponentSlotProperty,
			KingdomArchitectureStamper.ComponentAnchorProperty,
			KingdomArchitectureStamper.ComponentHashProperty,
			KingdomArchitectureStamper.ComponentTokenProperty,
			KingdomPlots.PlotIdProperty
		};

		/// <summary>
		/// The complete exact marking for one authored placement, judged the way the stamper's own
		/// verification judges it, plus a recomputed component token.
		/// <para>
		/// Every marker is proved present under its ONE lawful type table. A marker present under
		/// two tables is unreadable and is never resolved in either direction; a marker absent where
		/// the stamper always writes one is partial authority, not a default.
		/// </para>
		/// </summary>
		private static bool TryExactAuthority(GameObject Item, KingdomArchitectureIntent Intent,
			string Lot, ArchitecturePlacement Placement, out string Failure)
		{
			Failure = null;
			string slot = Bounded(Placement.Slot);
			for (int i = 0; i < AuditedIntKeys.Length; i++)
				if (Item.HasStringProperty(AuditedIntKeys[i]))
					return Fail("authored slot " + slot + " carries " + AuditedIntKeys[i]
						+ " under the string table", out Failure);
			for (int i = 0; i < AuditedTextKeys.Length; i++)
				if (Item.HasIntProperty(AuditedTextKeys[i]))
					return Fail("authored slot " + slot + " carries " + AuditedTextKeys[i]
						+ " under the int table", out Failure);
			if (!string.Equals(Item.Blueprint, Placement.Blueprint, StringComparison.Ordinal))
				return Fail("authored slot " + slot + " is not its authored blueprint", out Failure);
			if (!Item.HasIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
				|| Item.GetIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
					!= KingdomArchitectureStamper.ComponentSchema
				|| !Item.HasIntProperty(KingdomArchitectureStamper.ComponentLayerProperty)
				|| !Item.HasIntProperty(KingdomArchitectureStamper.ComponentExistingProperty)
				|| !Item.HasIntProperty(KingdomPlots.PlotPartProperty)
				|| !Item.HasStringProperty(KingdomPlots.PlotIdProperty)
				|| !Item.HasStringProperty(KingdomArchitectureStamper.ComponentSlotProperty)
				|| !Item.HasStringProperty(KingdomArchitectureStamper.ComponentHashProperty)
				|| !Item.HasStringProperty(KingdomArchitectureStamper.ComponentTokenProperty))
				return Fail("authored slot " + slot + " carries a partial component marking",
					out Failure);
			if (!TryProveCarried(Item, slot, out Failure)) return false;
			int layer = Item.GetIntProperty(KingdomArchitectureStamper.ComponentLayerProperty);
			if (layer < 0 || layer > MaxAuthoredLayer || layer != (int)Placement.Layer)
				return Fail("authored slot " + slot + " declares a layer its receipt does not",
					out Failure);
			if (!TryProveAnchor(Item, Placement, slot, out Failure)) return false;
			if (!string.Equals(Item.GetStringProperty(KingdomPlots.PlotIdProperty), Lot,
					StringComparison.Ordinal)
				|| !string.Equals(Item.GetStringProperty(
						KingdomArchitectureStamper.ComponentSlotProperty), Placement.Slot,
					StringComparison.Ordinal)
				|| !string.Equals(Item.GetStringProperty(
						KingdomArchitectureStamper.ComponentHashProperty), Intent.SnapshotHash,
					StringComparison.Ordinal))
				return Fail("authored slot " + slot + " carries foreign component authority",
					out Failure);
			return TryProveToken(Item, Intent, Lot, Placement, slot, out Failure);
		}

		/// <summary>
		/// The carried marker is proved and then left out of the digest.
		/// <para>
		/// A component retained across a same-lot tier upgrade keeps this marker for the rest of its
		/// life, so refusing it would make every upgraded building permanently uncapturable. It says
		/// how the piece got here, not what stands here, and two lawful builds with identical final
		/// placements must compare alike whichever way they were reached.
		/// </para>
		/// </summary>
		private static bool TryProveCarried(GameObject Item, string Slot, out string Failure)
		{
			Failure = null;
			KingdomRealizedCarriedShape shape = KingdomRealizedAuthorityShape.Carried(
				Item.HasIntProperty(KingdomArchitectureStamper.ComponentCarriedProperty),
				Item.GetIntProperty(KingdomArchitectureStamper.ComponentCarriedProperty),
				Item.HasStringProperty(KingdomArchitectureStamper.ComponentCarriedProperty));
			if (shape == KingdomRealizedCarriedShape.Invalid)
				return Fail("authored slot " + Slot + " carries an upgrade-retention marker in a "
					+ "shape the stamper never writes", out Failure);
			return true;
		}

		/// <summary>
		/// Anchor ABSENCE and an explicitly stored empty anchor are different states. The stamper
		/// removes the key when a placement declares no stateful anchor, so a stored empty one was
		/// written by something else, and a default getter would compare the two as equal.
		/// </summary>
		private static bool TryProveAnchor(GameObject Item, ArchitecturePlacement Placement,
			string Slot, out string Failure)
		{
			Failure = null;
			bool present = Item.HasStringProperty(
				KingdomArchitectureStamper.ComponentAnchorProperty);
			if (Placement.StatefulAnchor == null)
				return !present || Fail("authored slot " + Slot + " stores an anchor key its "
					+ "receipt declares absent", out Failure);
			if (!present)
				return Fail("authored slot " + Slot + " is missing the anchor key its receipt "
					+ "declares", out Failure);
			if (!string.Equals(Item.GetStringProperty(
					KingdomArchitectureStamper.ComponentAnchorProperty), Placement.StatefulAnchor,
				StringComparison.Ordinal))
				return Fail("authored slot " + Slot + " carries an anchor its receipt does not",
					out Failure);
			return true;
		}

		private static bool TryProveToken(GameObject Item, KingdomArchitectureIntent Intent,
			string Lot, ArchitecturePlacement Placement, string Slot, out string Failure)
		{
			Failure = null;
			if (Item.GetIntProperty(KingdomArchitectureStamper.ComponentExistingProperty)
					!= (Placement.ExistingAuthority ? 1 : 0)
				|| Item.GetIntProperty(KingdomPlots.PlotPartProperty)
					!= (Placement.ExistingAuthority ? 0 : 1))
				return Fail("authored slot " + Slot + " disagrees with its receipt about being a "
					+ "bound pre-existing relic", out Failure);
			string expected = ComponentToken(Lot, Intent.SnapshotHash, Placement);
			if (expected == null)
				return Fail("authored slot " + Slot + " has no recomputable component token",
					out Failure);
			if (!string.Equals(Item.GetStringProperty(
					KingdomArchitectureStamper.ComponentTokenProperty), expected,
				StringComparison.Ordinal))
				return Fail("authored slot " + Slot + " carries a component token this owner's "
					+ "receipt does not recompute", out Failure);
			return true;
		}

		/// <summary>
		/// Recomputes the stamper's component token from the owner's own frozen receipt.
		/// <para>
		/// The preimage mirrors <c>KingdomArchitectureStamper.ComponentToken</c>, which is private to
		/// the stamper. A source contract test pins the two preimages together, so a change to the
		/// production formula fails loudly instead of silently accepting every stored token.
		/// </para>
		/// </summary>
		private static string ComponentToken(string Lot, string Hash, ArchitecturePlacement Placement)
		{
			if (Lot == null || Hash == null || Placement == null || Placement.Slot == null
				|| Placement.Blueprint == null) return null;
			string preimage = Lot + "|" + Hash + "|" + Placement.Slot + "|"
				+ ((int)Placement.Layer).ToString(CultureInfo.InvariantCulture) + "|"
				+ Placement.X.ToString(CultureInfo.InvariantCulture) + "|"
				+ Placement.Y.ToString(CultureInfo.InvariantCulture) + "|"
				+ Placement.Blueprint + "|" + (Placement.StatefulAnchor ?? "") + "|"
				+ (Placement.ExistingAuthority ? "1" : "0");
			byte[] digest;
			using (SHA256 sha = SHA256.Create())
				digest = sha.ComputeHash(Encoding.UTF8.GetBytes(preimage));
			StringBuilder result = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++)
				result.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
			return result.ToString();
		}
	}
}
