using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{

		public static void ReadLifecycle(BinaryReader Reader, KingdomLifecycleBook Target)
		{
			ReadLifecycle(Reader, Target, null);
		}

		public static void ReadLifecycle(BinaryReader Reader, KingdomLifecycleBook Target,
			KingdomGrowthMigrationInput Migration)
		{
			if (Reader == null || Target == null) throw new ArgumentNullException();
			try
			{
				if (Reader.ReadInt32() != LifecycleMagic) Reject(Target, "invalid lifecycle framing");
				int version = Reader.ReadInt32();
				Target.FormatVersion = version;
				if (version != KingdomLifecycleRules.CurrentFormatVersion &&
					version != KingdomLifecycleRules.RaidLedgerLifecycleFormatVersion &&
					version != KingdomLifecycleRules.PreviousLifecycleFormatVersion &&
					version != KingdomLifecycleRules.LegacyLifecycleFormatVersion)
					Reject(Target, "unsupported lifecycle version");
				KingdomLifecycleBook value = new KingdomLifecycleBook();
				value.FormatVersion = version;
				value.LegacyIdentity = ReadExactBoolean(Reader);
				value.LegacyMigrationKey = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.Quarantined = ReadExactBoolean(Reader);
				value.Fault = ReadString(Reader, KingdomLifecycleRules.MaxTextBytes);
				value.SettlementId = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.IdentityBound = ReadExactBoolean(Reader);
				value.IdentityProof = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.PlainGuestNextSequence = Reader.ReadInt64();
				value.PlainGuestRetiredThrough = Reader.ReadInt64();
				value.NotableGuestNextSequence = Reader.ReadInt64();
				value.NotableGuestRetiredThrough = Reader.ReadInt64();
				value.RaidNextSequence = Reader.ReadInt64();
				value.RaidRetiredThrough = Reader.ReadInt64();
				value.PetitionNextSequence = Reader.ReadInt64();
				value.PetitionRetiredThrough = Reader.ReadInt64();
				ReadOption(Reader, out value.LocusOption, out value.LocusOptionTick);
				ReadOption(Reader, out value.NotableOption, out value.NotableOptionTick);
				ReadOption(Reader, out value.RaidOption, out value.RaidOptionTick);
				ReadOption(Reader, out value.PetitionOption, out value.PetitionOptionTick);
				bool legacyWire = version == KingdomLifecycleRules.LegacyLifecycleFormatVersion;
				value.PlainGuest = ReadOperation(Reader, version);
				value.NotableGuest = ReadOperation(Reader, version);
				value.Raid = ReadOperation(Reader, version);
				value.Petition = ReadOperation(Reader, version);
				int resources = ReadCount(Reader, KingdomLifecycleRules.MaxResourceRows);
				value.Resources = new List<KingdomLifecycleResourceRevision>(resources);
				for (int i = 0; i < resources; i++)
					value.Resources.Add(ReadResource(Reader, legacyWire));
				int proofs = ReadCount(Reader, KingdomLifecycleRules.MaxRecentProofs);
				value.RecentProofs = new List<KingdomLifecycleProof>(proofs);
				for (int i = 0; i < proofs; i++) value.RecentProofs.Add(ReadProof(Reader));
				value.RaidLedger = version >= KingdomLifecycleRules.RaidLedgerLifecycleFormatVersion
					? ReadRaidLedger(Reader) : new KingdomRaidLedger();
				if (version == KingdomLifecycleRules.LegacyLifecycleFormatVersion)
				{
					if (!KingdomLifecycleRules.TryStageGrowthMigrationFromV5(value,
						out KingdomGrowthBook staged))
						throw new InvalidDataException("legacy lifecycle v5 graph is malformed");
					value.FormatVersion = KingdomLifecycleRules.CurrentFormatVersion;
					value.Growth = staged;
					if (Migration != null && staged.MigrationPending)
					{
						KingdomGrowthMigrationResult migrated =
							KingdomLifecycleRules.ApplyGrowthMigration(value, Migration);
							if (!migrated.Valid ||
								!KingdomLifecycleRules.TryPublishGrowthMigration(value, migrated))
								throw new InvalidDataException(migrated.Failure);
						}
						KingdomLifecycleRules.QuarantineLegacyRaidAuthority(value);
					}
					else
					{
						value.Growth = ReadGrowthSection(Reader);
						if (version == KingdomLifecycleRules.PreviousLifecycleFormatVersion)
						{
							if (!KingdomLifecycleRules.StageRaidMigrationFromV6(value))
								throw new InvalidDataException(
									"lifecycle v6 raid migration was rejected");
						}
						else if (version == KingdomLifecycleRules.RaidLedgerLifecycleFormatVersion)
							value.FormatVersion = KingdomLifecycleRules.CurrentFormatVersion;
					}
				KingdomLifecycleRules.Normalize(value);
				Copy(value, Target);
			}
			catch (Exception)
			{
				Poison(Target, "malformed lifecycle wire was rejected");
				throw;
			}
		}
	}
}
