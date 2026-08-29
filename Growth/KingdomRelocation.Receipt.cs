using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static bool TryRead(Zone Zone, out KingdomRelocationReceipt Receipt,
			out string Encoded, out string Failure)
		{
			Receipt = null; Encoded = null; Failure = null;
			if (Zone == null)
			{
				Failure = "relocation zone is absent";
				return false;
			}
			Encoded = Zone.GetZoneProperty(ReceiptProperty, null);
			if (string.IsNullOrEmpty(Encoded))
			{
				Failure = "no heart ring call is active here";
				return false;
			}
			if (!KingdomRelocationCodec.TryDecode(Encoded, out Receipt, out Failure))
			{
				Zone.SetZoneProperty(FaultProperty, Bounded(Failure));
				KingdomLog.Log("relocation: malformed receipt in " + Zone.ZoneID + " ("
					+ Failure + ")");
				return false;
			}
			if (Receipt.ZoneId != Zone.ZoneID)
			{
				Failure = "relocation receipt names another zone";
				return false;
			}
			return true;
		}

		private static bool TryPublish(Zone Zone, string Expected,
			KingdomRelocationReceipt Receipt, out string Encoded, out string Failure)
		{
			Encoded = null; Failure = null;
			if (Zone == null || Receipt == null
				|| (Zone.GetZoneProperty(ReceiptProperty, null) ?? "") != (Expected ?? ""))
			{
				Failure = "relocation receipt changed before compare-and-swap";
				return false;
			}
			if (Receipt.Generation == int.MaxValue)
			{
				Failure = "relocation receipt generation is exhausted";
				return false;
			}
			Receipt.Generation++;
			if (!KingdomRelocationCodec.TryEncode(Receipt, out Encoded, out Failure)) return false;
			Zone.SetZoneProperty(ReceiptProperty, Encoded);
			if (Zone.GetZoneProperty(ReceiptProperty, null) != Encoded)
			{
				Failure = "relocation receipt did not persist";
				return false;
			}
			Zone.RemoveZoneProperty(FaultProperty);
			return true;
		}

		private static bool TryOpen(Zone Zone, KingdomRelocationReceipt Receipt,
			out string Encoded, out string Failure)
		{
			Encoded = null; Failure = null;
			if (Zone == null || !string.IsNullOrEmpty(Zone.GetZoneProperty(ReceiptProperty, null)))
			{
				Failure = "another heart ring call already owns this ground";
				return false;
			}
			// TryPublish owns every mutation generation. Prepared authority starts at zero.
			Receipt.Generation = 0;
			return TryPublish(Zone, null, Receipt, out Encoded, out Failure);
		}

		private static bool TryRetire(Zone Zone, string Expected,
			KingdomRelocationReceipt Receipt, out string Failure)
		{
			Failure = null;
			string completed;
			if (Zone == null || Receipt == null || Receipt.Phase != KingdomRelocationPhase.Complete
				|| Receipt.CurrentMove != Receipt.Moves.Count
				|| (Zone.GetZoneProperty(ReceiptProperty, null) ?? "") != (Expected ?? "")
				|| !KingdomRelocationCodec.TryEncode(Receipt, out completed, out Failure)) return false;
			Zone.SetZoneProperty(LastReceiptProperty, completed);
			if (Zone.GetZoneProperty(LastReceiptProperty, null) != completed) return false;
			Zone.RemoveZoneProperty(ReceiptProperty);
			return string.IsNullOrEmpty(Zone.GetZoneProperty(ReceiptProperty, null));
		}

		private static bool Quarantine(Zone Zone, string Expected,
			KingdomRelocationReceipt Receipt, string Failure)
		{
			if (Receipt == null || Receipt.Phase == KingdomRelocationPhase.Complete) return false;
			Receipt.Phase = KingdomRelocationPhase.Quarantined;
			Receipt.Failure = Bounded(Failure);
			string ignored;
			string publishFailure;
			bool published = TryPublish(Zone, Expected, Receipt, out ignored, out publishFailure);
			Zone?.SetZoneProperty(FaultProperty, Receipt.Failure);
			KingdomLog.Log("relocation quarantined: " + Receipt.Failure
				+ (published ? "" : " (publication also failed: " + publishFailure + ")"));
			return published;
		}
	}
}
