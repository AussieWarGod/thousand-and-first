using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		internal const string HandoverManifestPrefix = "r_TAF_ImprovementManifest:";
		private const string HandoverManifestEscrowPrefix =
			"r_TAF_ImprovementManifestEscrow:";

		internal static bool TryPublishInventoryManifest(GameObject Source, GameObject Target,
			Cell Where, r_KingdomImprovement Receipt)
		{
			if (!ExactHandoverObjects(Source, Target, Receipt) || Where == null
				|| Source.CurrentCell != Where || Target.CurrentCell != Where)
				return FailHandover(Receipt, "Inventory manifest endpoints are not exact.");
			GameObject owner = Target;
			string schemaKey = ManifestKey("Schema");
			if (owner.HasStringProperty(schemaKey))
				return FailHandover(Receipt, "Inventory manifest schema has the wrong type.");
			if (owner.HasIntProperty(schemaKey))
			{
				if (owner.GetIntProperty(schemaKey) != 1)
					return FailHandover(Receipt, "Inventory manifest schema is not supported.");
				string failure;
				return VerifyHandoverContentCustody(Source, Target, Where, Receipt, false,
					out failure) || FailHandover(Receipt, failure);
			}
			IList<GameObject> inventory = Source.Inventory?.Objects;
			int count = inventory == null ? 0 : inventory.Count;
			if (!KingdomUpgradeContentRules.ManifestCardinalityValid(count))
				return FailHandover(Receipt,
					"Improvement inventory exceeds the 4096-item custody limit.");
			int destinationKind = Target.Inventory == null ? 2 : 1;
			string destinationId = destinationKind == 1 ? Target.IDIfAssigned : CellKey(Where);
			if (!BoundedIdentity(destinationId) || The.Game == null)
				return FailHandover(Receipt, "Inventory manifest destination cannot be rooted.");
			List<GameObject> items = inventory == null
				? new List<GameObject>() : new List<GameObject>(inventory);
			List<string> roots = new List<string>(count);
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			StringBuilder canonical = BeginManifestDigest(Source, Target, Receipt, count,
				destinationKind, destinationId);
			for (int i = 0; i < count; i++)
			{
				GameObject item = items[i];
				GameObject global;
				if (!ExactItemOwner(item, Source, null) || !BoundedIdentity(item.IDIfAssigned)
					|| !ids.Add(item.IDIfAssigned) || string.IsNullOrEmpty(item.Blueprint)
					|| item.Blueprint.Length > 256 || item.Count <= 0
					|| KingdomConstruction.FindGlobalLiveId(item.IDIfAssigned, out global)
						!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(global, item))
					return FailHandover(Receipt,
						"Inventory manifest contains an unbounded, duplicate, or foreign item.");
				string root = ManifestEscrowKey(Receipt.HandoverConstructionReceipt,
					Source.IDIfAssigned, Target.IDIfAssigned, i, item.IDIfAssigned);
				if (!BoundedManifestEscrowKey(root))
					return FailHandover(Receipt, "Inventory manifest root cannot be derived.");
				roots.Add(root);
				AppendManifestTerm(canonical, item.IDIfAssigned);
				AppendManifestTerm(canonical, item.Blueprint);
				AppendManifestTerm(canonical, item.Count.ToString(CultureInfo.InvariantCulture));
				AppendManifestTerm(canonical, root);
			}
			string digest = FinishManifestDigest(canonical);
			if (!ExactManifestHeaderOrAbsent(owner, Source, Target, Receipt, count,
				destinationKind, destinationId, digest))
				return FailHandover(Receipt, "Inventory manifest header carries a third value.");
			for (int i = 0; i < count; i++)
				if (!ExactManifestEntryOrAbsent(owner, i, items[i], roots[i]))
					return FailHandover(Receipt,
						"Inventory manifest entry carries a third or malformed value.");
			for (int i = 0; i < count; i++)
			{
				object collision;
				if (The.Game.ObjectGameState.TryGetValue(roots[i], out collision)
					&& !ReferenceEquals(collision, items[i]))
					return FailHandover(Receipt, "Inventory manifest root collides with foreign custody.");
			}
			try
			{
				for (int i = 0; i < count; i++)
				{
					The.Game.SetObjectGameState(roots[i], items[i]);
					owner.SetStringProperty(ManifestEntryKey(i, "Id"), items[i].IDIfAssigned);
					owner.SetStringProperty(ManifestEntryKey(i, "Blueprint"), items[i].Blueprint);
					owner.SetIntProperty(ManifestEntryKey(i, "Count"), items[i].Count);
					owner.SetStringProperty(ManifestEntryKey(i, "Root"), roots[i]);
				}
				owner.SetStringProperty(ManifestKey("SourceId"), Source.IDIfAssigned);
				owner.SetStringProperty(ManifestKey("TargetId"), Target.IDIfAssigned);
				owner.SetStringProperty(ManifestKey("ConstructionReceipt"),
					Receipt.HandoverConstructionReceipt);
				owner.SetIntProperty(ManifestKey("Count"), count);
				owner.SetIntProperty(ManifestKey("DestinationKind"), destinationKind);
				owner.SetStringProperty(ManifestKey("DestinationId"), destinationId);
				owner.SetStringProperty(ManifestKey("Digest"), digest);
				owner.SetIntProperty(schemaKey, 1);
			}
			catch (Exception exception)
			{
				Receipt.HandoverFailure = "Inventory manifest publication remains retryable: "
					+ exception.Message;
				return false;
			}
			string verifyFailure;
			return VerifyHandoverContentCustody(Source, Target, Where, Receipt, false,
				out verifyFailure) || FailHandover(Receipt, verifyFailure);
		}

		private static bool ExactManifestHeaderOrAbsent(GameObject Owner, GameObject Source,
			GameObject Target, r_KingdomImprovement Receipt, int Count, int DestinationKind,
			string DestinationId, string Digest)
		{
			return ExactManifestTextOrAbsent(Owner, "SourceId", Source.IDIfAssigned)
				&& ExactManifestTextOrAbsent(Owner, "TargetId", Target.IDIfAssigned)
				&& ExactManifestTextOrAbsent(Owner, "ConstructionReceipt",
					Receipt.HandoverConstructionReceipt)
				&& ExactManifestIntOrAbsent(Owner, "Count", Count)
				&& ExactManifestIntOrAbsent(Owner, "DestinationKind", DestinationKind)
				&& ExactManifestTextOrAbsent(Owner, "DestinationId", DestinationId)
				&& ExactManifestTextOrAbsent(Owner, "Digest", Digest);
		}

		private static bool ExactManifestEntryOrAbsent(GameObject Owner, int Index,
			GameObject Item, string Root)
		{
			return ExactTextOrAbsent(Owner, ManifestEntryKey(Index, "Id"), Item.IDIfAssigned)
				&& ExactTextOrAbsent(Owner, ManifestEntryKey(Index, "Blueprint"), Item.Blueprint)
				&& ExactIntOrAbsent(Owner, ManifestEntryKey(Index, "Count"), Item.Count)
				&& ExactTextOrAbsent(Owner, ManifestEntryKey(Index, "Root"), Root);
		}

		private static StringBuilder BeginManifestDigest(GameObject Source, GameObject Target,
			r_KingdomImprovement Receipt, int Count, int DestinationKind, string DestinationId)
		{
			StringBuilder value = new StringBuilder();
			AppendManifestTerm(value, Source.IDIfAssigned);
			AppendManifestTerm(value, Target.IDIfAssigned);
			AppendManifestTerm(value, Receipt.HandoverConstructionReceipt);
			AppendManifestTerm(value, Count.ToString(CultureInfo.InvariantCulture));
			AppendManifestTerm(value, DestinationKind.ToString(CultureInfo.InvariantCulture));
			AppendManifestTerm(value, DestinationId);
			return value;
		}

		private static void AppendManifestTerm(StringBuilder Value, string Term)
		{
			string text = Term ?? string.Empty;
			Value.Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text);
		}

		private static string FinishManifestDigest(StringBuilder Canonical)
		{
			using (SHA256 hash = SHA256.Create())
				return Convert.ToBase64String(hash.ComputeHash(
					Encoding.UTF8.GetBytes(Canonical.ToString())));
		}

		private static string ManifestEscrowKey(string Receipt, string SourceId,
			string TargetId, int Index, string ItemId)
		{
			if (!BoundedIdentity(Receipt) || !BoundedIdentity(SourceId)
				|| !BoundedIdentity(TargetId) || !BoundedIdentity(ItemId) || Index < 0)
				return null;
			string material = Receipt + "\n" + SourceId + "\n" + TargetId + "\n"
				+ Index.ToString(CultureInfo.InvariantCulture) + "\n" + ItemId;
			byte[] digest;
			using (SHA256 hash = SHA256.Create())
				digest = hash.ComputeHash(Encoding.UTF8.GetBytes(material));
			StringBuilder key = new StringBuilder(HandoverManifestEscrowPrefix, 104);
			for (int i = 0; i < digest.Length; i++)
				key.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
			return key.ToString();
		}

		private static bool BoundedManifestEscrowKey(string Key)
		{
			return !string.IsNullOrEmpty(Key) && Key.Length <= 128
				&& Key.StartsWith(HandoverManifestEscrowPrefix, StringComparison.Ordinal);
		}

		private static string ManifestKey(string Name)
		{
			return HandoverManifestPrefix + Name;
		}

		private static string ManifestEntryKey(int Index, string Name)
		{
			return HandoverManifestPrefix + "Entry:" + Index.ToString(
				CultureInfo.InvariantCulture) + ":" + Name;
		}

		private static bool ExactManifestTextOrAbsent(GameObject Owner, string Name,
			string Expected)
		{
			return ExactTextOrAbsent(Owner, ManifestKey(Name), Expected);
		}

		private static bool ExactManifestIntOrAbsent(GameObject Owner, string Name, int Expected)
		{
			return ExactIntOrAbsent(Owner, ManifestKey(Name), Expected);
		}

		private static bool ExactTextOrAbsent(GameObject Owner, string Key, string Expected)
		{
			return Owner != null && !Owner.HasIntProperty(Key)
				&& (!Owner.HasStringProperty(Key) || Owner.GetStringProperty(Key) == Expected);
		}

		private static bool ExactIntOrAbsent(GameObject Owner, string Key, int Expected)
		{
			return Owner != null && !Owner.HasStringProperty(Key)
				&& (!Owner.HasIntProperty(Key) || Owner.GetIntProperty(Key) == Expected);
		}
	}
}
