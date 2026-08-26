using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		public static void WriteCarry(BinaryWriter Writer, KingdomCarryBook Book)
		{
			if (Writer == null || Book == null || Book.WireRejected
				|| Book.FormatVersion != KingdomLifecycleRules.CurrentCarryFormatVersion)
				throw new InvalidDataException("carry authority is not writable");
			if (Book.OpaquePayload != null)
			{
				if (!Book.Quarantined || Book.OpaqueWireVersion <=
					KingdomLifecycleRules.CurrentCarryFormatVersion
					|| Book.OpaquePayload.Length > KingdomLifecycleRules.MaxCarrySectionBytes)
					throw new InvalidDataException("opaque carry evidence is malformed");
				Writer.Write(CarryMagic); Writer.Write(Book.OpaqueWireVersion);
				Writer.Write(Book.OpaquePayload.Length);
				Writer.Write(Book.OpaquePayload, 0, Book.OpaquePayload.Length);
				return;
			}
			using (GrowthCappedWriteStream stream =
				new GrowthCappedWriteStream(KingdomLifecycleRules.MaxCarrySectionBytes))
			using (BinaryWriter body = new BinaryWriter(stream, StrictUtf8, true))
			{
				WriteCarryBody(body, Book, true); body.Flush();
				byte[] payload = stream.ToArray();
				Writer.Write(CarryMagic);
				Writer.Write(KingdomLifecycleRules.CurrentCarryFormatVersion);
				Writer.Write(payload.Length);
				Writer.Write(payload, 0, payload.Length);
			}
		}

		/// <summary>Exact historical carry-v5 writer used only by migration and byte fixtures.</summary>
		internal static void WriteCarryV5Fixture(BinaryWriter Writer, KingdomCarryBook Book)
		{
			if (Writer == null || Book == null || Book.WireRejected || Book.OpaquePayload != null
				|| Book.FormatVersion != KingdomLifecycleRules.CurrentCarryFormatVersion
				|| (Book.Open != null && Book.Open.AuthorityKind !=
					KingdomCarryAuthorityKind.LegacyMaterialProjection))
				throw new InvalidDataException("carry authority is not representable as v5");
			Writer.Write(CarryMagic);
			Writer.Write(KingdomLifecycleRules.LegacyCarryFormatVersion);
			WriteCarryBody(Writer, Book, false);
		}

		private static void WriteCarryBody(BinaryWriter Writer, KingdomCarryBook Book,
			bool IncludeV6)
		{
			EnsureCount(Book.SettlementIds, KingdomLifecycleRules.MaxSettlementIds,
				"settlement ids");
			EnsureCount(Book.Resources, KingdomLifecycleRules.MaxResourceRows, "resource rows");
			EnsureCount(Book.RecentProofs, KingdomLifecycleRules.MaxRecentProofs, "proof rows");
			EnsureOuterResourceKinds(Book.Resources);
			if (Book.Open != null && Book.Open.ScheduleLease != null &&
				(byte)Book.Open.ScheduleLease.Kind > (byte)KingdomLifecycleResourceKind.Raid)
				throw new InvalidDataException("carry lease kind exceeds v5 contract");
			Writer.Write(Book.LegacyIdentity);
			WriteString(Writer, Book.LegacyMigrationKey, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.Quarantined);
			WriteString(Writer, Book.Fault, KingdomLifecycleRules.MaxTextBytes);
			WriteString(Writer, Book.RealmId, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.SettlementIds.Count);
			for (int i = 0; i < Book.SettlementIds.Count; i++)
				WriteString(Writer, Book.SettlementIds[i], KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.IdentityBound);
			WriteString(Writer, Book.IdentityProof, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.NextSequence);
			Writer.Write(Book.RetiredThrough);
			WriteCarryOperation(Writer, Book.Open, IncludeV6);
			Writer.Write(Book.Resources.Count);
			for (int i = 0; i < Book.Resources.Count; i++) WriteResource(Writer, Book.Resources[i]);
			Writer.Write(Book.RecentProofs.Count);
			for (int i = 0; i < Book.RecentProofs.Count; i++) WriteProof(Writer, Book.RecentProofs[i]);
		}

		public static void ReadCarry(BinaryReader Reader, KingdomCarryBook Target)
		{
			if (Reader == null || Target == null) throw new ArgumentNullException();
			try
			{
				if (Reader.ReadInt32() != CarryMagic) Reject(Target, "invalid carry framing");
				int version = Reader.ReadInt32();
				Target.FormatVersion = version;
				if (version == KingdomLifecycleRules.LegacyCarryFormatVersion)
				{
					KingdomCarryBook legacy = ReadCarryBody(Reader, false);
					legacy.FormatVersion = KingdomLifecycleRules.CurrentCarryFormatVersion;
					KingdomLifecycleRules.Normalize(legacy);
					Copy(legacy, Target);
					return;
				}
				if (version < KingdomLifecycleRules.CurrentCarryFormatVersion)
					Reject(Target, "unsupported carry version");
				int length = ReadCount(Reader, KingdomLifecycleRules.MaxCarrySectionBytes);
				byte[] payload = Reader.ReadBytes(length);
				if (payload.Length != length) throw new EndOfStreamException("carry payload is truncated");
				if (version > KingdomLifecycleRules.CurrentCarryFormatVersion)
				{
					Copy(OpaqueCarry(payload, version,
						"future carry payload preserved as opaque evidence"), Target);
					return;
				}
				KingdomCarryBook value;
				using (MemoryStream stream = new MemoryStream(payload, false))
				using (BinaryReader body = new BinaryReader(stream, StrictUtf8, true))
				{
					value = ReadCarryBody(body, true);
					if (stream.Position != stream.Length)
						throw new InvalidDataException("carry payload has trailing bytes");
				}
				value.FormatVersion = KingdomLifecycleRules.CurrentCarryFormatVersion;
				KingdomLifecycleRules.Normalize(value);
				Copy(value, Target);
			}
			catch (Exception)
			{
				Poison(Target, "malformed carry wire was rejected");
				throw;
			}
		}

		private static KingdomCarryBook ReadCarryBody(BinaryReader Reader, bool IncludeV6)
		{
			KingdomCarryBook value = new KingdomCarryBook();
				value.LegacyIdentity = ReadExactBoolean(Reader);
				value.LegacyMigrationKey = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.Quarantined = ReadExactBoolean(Reader);
				value.Fault = ReadString(Reader, KingdomLifecycleRules.MaxTextBytes);
				value.RealmId = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				int settlements = ReadCount(Reader, KingdomLifecycleRules.MaxSettlementIds);
				value.SettlementIds = new List<string>(settlements);
				for (int i = 0; i < settlements; i++)
					value.SettlementIds.Add(ReadString(Reader, KingdomLifecycleRules.MaxIdBytes));
				value.IdentityBound = ReadExactBoolean(Reader);
				value.IdentityProof = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.NextSequence = Reader.ReadInt64();
				value.RetiredThrough = Reader.ReadInt64();
				value.Open = ReadCarryOperation(Reader, IncludeV6);
				int resources = ReadCount(Reader, KingdomLifecycleRules.MaxResourceRows);
				value.Resources = new List<KingdomLifecycleResourceRevision>(resources);
				for (int i = 0; i < resources; i++) value.Resources.Add(ReadResource(Reader, true));
				int proofs = ReadCount(Reader, KingdomLifecycleRules.MaxRecentProofs);
				value.RecentProofs = new List<KingdomLifecycleProof>(proofs);
				for (int i = 0; i < proofs; i++) value.RecentProofs.Add(ReadProof(Reader));
			return value;
		}

		private static KingdomCarryBook OpaqueCarry(byte[] Payload, int WireVersion, string Fault)
		{
			return new KingdomCarryBook
			{
				FormatVersion = KingdomLifecycleRules.CurrentCarryFormatVersion,
				Quarantined = true,
				Fault = Fault,
				OpaqueWireVersion = WireVersion,
				OpaquePayload = Payload == null ? null : (byte[])Payload.Clone()
			};
		}

	}
}
