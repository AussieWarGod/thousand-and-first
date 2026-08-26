using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static void WriteOption(BinaryWriter w, KingdomLifecycleOptionState s, long tick)
		{
			w.Write((byte)s); w.Write(tick);
		}

		private static void ReadOption(BinaryReader r, out KingdomLifecycleOptionState s, out long tick)
		{
			s = (KingdomLifecycleOptionState)r.ReadByte(); tick = r.ReadInt64();
		}

		private static void WriteSix(BinaryWriter w, int a, int b, int c, int d, int e, int f)
		{
			w.Write(a); w.Write(b); w.Write(c); w.Write(d); w.Write(e); w.Write(f);
		}

		private static void ReadSix(BinaryReader r, out int a, out int b, out int c,
			out int d, out int e, out int f)
		{
			a = r.ReadInt32(); b = r.ReadInt32(); c = r.ReadInt32();
			d = r.ReadInt32(); e = r.ReadInt32(); f = r.ReadInt32();
		}

		private static string S(BinaryReader r, bool id, bool text = false)
		{
			return ReadString(r, text ? KingdomLifecycleRules.MaxTextBytes
				: id ? KingdomLifecycleRules.MaxIdBytes : KingdomLifecycleRules.MaxNameBytes);
		}

		private static void S(BinaryWriter w, string value, bool id, bool text = false)
		{
			WriteString(w, value, text ? KingdomLifecycleRules.MaxTextBytes
				: id ? KingdomLifecycleRules.MaxIdBytes : KingdomLifecycleRules.MaxNameBytes);
		}

		private static void EnsureCount<T>(List<T> rows, int maximum, string description)
		{
			if (rows == null || rows.Count > maximum)
				throw new InvalidDataException("invalid " + description);
		}

		private static void EnsureOuterResourceKinds(
			List<KingdomLifecycleResourceRevision> rows,
			params KingdomLifecycleOperation[] operations)
		{
			if (rows != null) for (int i = 0; i < rows.Count; i++)
				if (rows[i] == null || (byte)rows[i].Kind >
					(byte)KingdomLifecycleResourceKind.Raid)
					throw new InvalidDataException("outer resource kind exceeds v5 contract");
			if (operations == null) return;
			for (int i = 0; i < operations.Length; i++)
			{
				KingdomLifecycleOperation operation = operations[i];
				if (operation == null || operation.ResourceLeases == null) continue;
				for (int j = 0; j < operation.ResourceLeases.Count; j++)
					if (operation.ResourceLeases[j] == null ||
						(byte)operation.ResourceLeases[j].Kind >
						(byte)KingdomLifecycleResourceKind.Raid)
						throw new InvalidDataException("outer lease kind exceeds v5 contract");
			}
		}

		private static void Reject(KingdomLifecycleBook target, string fault)
		{
			target.WireRejected = true; target.Quarantined = true; target.Fault = fault;
			throw new InvalidDataException(fault);
		}

		private static void Reject(KingdomCarryBook target, string fault)
		{
			target.WireRejected = true; target.Quarantined = true; target.Fault = fault;
			throw new InvalidDataException(fault);
		}

		private static void Poison(KingdomLifecycleBook target, string fault)
		{
			target.WireRejected = true;
			target.Quarantined = true;
			if (string.IsNullOrEmpty(target.Fault)) target.Fault = fault;
			target.PlainGuest = null;
			target.NotableGuest = null;
			target.Raid = null;
			target.Petition = null;
			target.Resources = new List<KingdomLifecycleResourceRevision>();
			target.RecentProofs = new List<KingdomLifecycleProof>();
			target.RaidLedger = new KingdomRaidLedger();
			target.Growth = PoisonGrowth("enclosing lifecycle wire was rejected");
		}

		private static void Poison(KingdomCarryBook target, string fault)
		{
			target.WireRejected = true;
			target.Quarantined = true;
			if (string.IsNullOrEmpty(target.Fault)) target.Fault = fault;
			target.Open = null;
			target.SettlementIds = new List<string>();
			target.Resources = new List<KingdomLifecycleResourceRevision>();
			target.RecentProofs = new List<KingdomLifecycleProof>();
			target.OpaqueWireVersion = 0;
			target.OpaquePayload = null;
		}

		private static void Copy(KingdomLifecycleBook a, KingdomLifecycleBook b)
		{
			b.FormatVersion = a.FormatVersion; b.LegacyIdentity = a.LegacyIdentity;
			b.LegacyMigrationKey = a.LegacyMigrationKey; b.Quarantined = a.Quarantined;
			b.Fault = a.Fault; b.SettlementId = a.SettlementId;
			b.IdentityBound = a.IdentityBound; b.IdentityProof = a.IdentityProof;
			b.PlainGuestNextSequence = a.PlainGuestNextSequence;
			b.PlainGuestRetiredThrough = a.PlainGuestRetiredThrough;
			b.NotableGuestNextSequence = a.NotableGuestNextSequence;
			b.NotableGuestRetiredThrough = a.NotableGuestRetiredThrough;
			b.RaidNextSequence = a.RaidNextSequence; b.RaidRetiredThrough = a.RaidRetiredThrough;
			b.PetitionNextSequence = a.PetitionNextSequence;
			b.PetitionRetiredThrough = a.PetitionRetiredThrough;
			b.LocusOption = a.LocusOption; b.LocusOptionTick = a.LocusOptionTick;
			b.NotableOption = a.NotableOption; b.NotableOptionTick = a.NotableOptionTick;
			b.RaidOption = a.RaidOption; b.RaidOptionTick = a.RaidOptionTick;
			b.PetitionOption = a.PetitionOption; b.PetitionOptionTick = a.PetitionOptionTick;
			b.PlainGuest = a.PlainGuest; b.NotableGuest = a.NotableGuest;
			b.Raid = a.Raid; b.Petition = a.Petition;
			b.Resources = a.Resources; b.RecentProofs = a.RecentProofs;
			b.RaidLedger = a.RaidLedger;
			b.Growth = a.Growth; b.WireRejected = false;
		}

		private static void Copy(KingdomCarryBook a, KingdomCarryBook b)
		{
			b.FormatVersion = a.FormatVersion; b.LegacyIdentity = a.LegacyIdentity;
			b.LegacyMigrationKey = a.LegacyMigrationKey; b.Quarantined = a.Quarantined;
			b.Fault = a.Fault; b.RealmId = a.RealmId; b.SettlementIds = a.SettlementIds;
			b.IdentityBound = a.IdentityBound; b.IdentityProof = a.IdentityProof;
			b.NextSequence = a.NextSequence;
			b.RetiredThrough = a.RetiredThrough; b.Open = a.Open;
			b.Resources = a.Resources; b.RecentProofs = a.RecentProofs;
			b.OpaqueWireVersion = a.OpaqueWireVersion; b.OpaquePayload = a.OpaquePayload;
			b.WireRejected = false;
		}
	}
}
