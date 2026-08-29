using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private const string PortfolioCargoRootUnencodable = "unencodable";

		/// <summary>The exact rooted cargo, migrating a legacy key only once the candidate under it
		/// has fully proved itself. Validating first is the whole point: the old delimiter-joined
		/// key could be shared by two distinct pair/epoch/operation tuples, so seizing whatever sits
		/// there would install another operation's object under this one's canonical name. A
		/// candidate that does not reprove is left exactly where it is.</summary>
		private static bool TryRootedPurposeCargo(KingdomPurposeOperationReceipt Operation,
			out GameObject Cargo)
		{
			if (TryRootedPurposeCargoExact(Operation, out Cargo)) return true;
			if (The.Game == null) return false;
			string legacy = PortfolioLegacyCargoRootKey(Operation.PairId, Operation.PairEpoch,
				Operation.OperationId);
			string key = PurposeCargoRootKey(Operation);
			if (key == legacy
				|| !The.Game.ObjectGameState.TryGetValue(legacy, out object value)
				|| The.Game.ObjectGameState.ContainsKey(key)
				|| !ExactRootedPurposeCargo(Operation, value, out Cargo)) return false;
			The.Game.ObjectGameState.Remove(legacy);
			The.Game.ObjectGameState[key] = Cargo;
			return true;
		}

		/// <summary>The rooted cargo under this operation's canonical key alone. Read-only: status
		/// and every other reporting surface use this, so rendering can never mutate the root
		/// table.</summary>
		private static bool TryRootedPurposeCargoExact(KingdomPurposeOperationReceipt Operation,
			out GameObject Cargo)
		{
			Cargo = null;
			return The.Game != null
				&& The.Game.ObjectGameState.TryGetValue(PurposeCargoRootKey(Operation),
					out object value)
				&& ExactRootedPurposeCargo(Operation, value, out Cargo);
		}

		private static bool ExactRootedPurposeCargo(KingdomPurposeOperationReceipt Operation,
			object Value, out GameObject Cargo)
		{
			Cargo = Value as GameObject;
			if (Cargo != null && Cargo.IDIfAssigned == Operation.OutputCargoId
				&& ExactPortfolioCargoIdentity(Cargo, Operation.OutputCargoReceipt)) return true;
			Cargo = null;
			return false;
		}

		/// <summary>Removes both the canonical and the legacy root for one consumed cargo, so a
		/// receipt written before the canonical key existed cannot leave an entry behind. Each key
		/// is checked before it is deleted: the legacy delimiter form shares a namespace with other
		/// pair/epoch/operation tuples, and a blind removal there would drop another operation's
		/// live root. Idempotent, so a cut between this and its publish costs nothing on retry.</summary>
		internal static void RemovePurposeCargoRoots(KingdomPurposeCargoReceipt Cargo)
		{
			if (The.Game == null || Cargo == null) return;
			string encoded = KingdomPurposePortfolioRules.EncodeCargo(Cargo);
			RemovePurposeCargoRoot(PurposeCargoRootKey(Cargo), Cargo, encoded);
			RemovePurposeCargoRoot(PortfolioLegacyCargoRootKey(Cargo.PairId, Cargo.PairEpoch,
				Cargo.OperationId), Cargo, encoded);
		}

		/// <summary>Removes one root entry only when the value under it is the object this receipt
		/// names &mdash; alive and reproving its whole receipt, or the dead remains of that same
		/// identity, which leaves nothing but a stale key. A foreign object under a colliding key
		/// survives untouched.</summary>
		private static void RemovePurposeCargoRoot(string Key, KingdomPurposeCargoReceipt Cargo,
			string Encoded)
		{
			if (!The.Game.ObjectGameState.TryGetValue(Key, out object value)) return;
			GameObject rooted = value as GameObject;
			if (!KingdomPurposePortfolioRules.RootEntryIsRetirable(rooted != null,
				rooted != null && rooted.IDIfAssigned == Cargo.ObjectId,
				GameObject.Validate(rooted),
				rooted != null && ExactPortfolioCargoIdentity(rooted, Encoded))) return;
			The.Game.ObjectGameState.Remove(Key);
		}

		// Reproduced exactly as the old form was written, with the epoch pinned to invariant digits
		// so the key one machine saved is the key another reads back.
		private static string PortfolioLegacyCargoRootKey(string PairId, long PairEpoch,
			string OperationId)
		{
			return PortfolioCargoRootPrefix + PairId + ":"
				+ PairEpoch.ToString(CultureInfo.InvariantCulture) + ":" + OperationId;
		}

		// Canonical because Id() admits ':': a plain delimiter join lets two distinct
		// pair/epoch/operation tuples name one root. Both overloads go through the one encoder so
		// the operation-side and cargo-side keys can never drift apart. A receipt that reaches here
		// has already passed ValidOperation/ValidCargo, so the encoding cannot fail; the sentinel
		// keeps the key total rather than handing a caller a null dictionary key, and roots nothing
		// because no cargo is ever published under it.
		private static string PurposeCargoRootKey(KingdomPurposeOperationReceipt Operation)
		{
			return PortfolioCargoRootPrefix
				+ (KingdomPurposePortfolioRules.TryCargoRootBody(Operation.PairId,
					Operation.PairEpoch, Operation.OperationId, out string body)
					? body : PortfolioCargoRootUnencodable);
		}

		private static string PurposeCargoRootKey(KingdomPurposeCargoReceipt Cargo)
		{
			return PortfolioCargoRootPrefix
				+ (KingdomPurposePortfolioRules.TryCargoRootBody(Cargo.PairId, Cargo.PairEpoch,
					Cargo.OperationId, out string body) ? body : PortfolioCargoRootUnencodable);
		}
	}
}
