using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>Engine-free authority, replay, FSM, and conservation laws.</summary>
	public static partial class KingdomLifecycleRules
	{
		public const int LegacyLifecycleFormatVersion = 5;
		public const int PreviousLifecycleFormatVersion = 6;
		public const int RaidLedgerLifecycleFormatVersion = 7;
		public const int DefenceReservationLifecycleFormatVersion = 8;
		public const int LodgeTerminalLifecycleFormatVersion = 9;
		public const int LodgeMarketSourceLifecycleFormatVersion = 10;
		public const int CurrentFormatVersion = LodgeMarketSourceLifecycleFormatVersion;
		public const int MaxRaidGrievances = 64;
		public const int MaxRaidIncidents = 64;
		public const int LegacyCarryFormatVersion = 5;
		public const int CurrentCarryFormatVersion = 6;
		public const int CurrentCarryManifestVersion = 1;
		public const int MaxCarryJobIds = 16;
		public const int MaxCarryTripIds = 16;
		public const int MaxCarrySectionBytes = 512 * 1024;
		public const int LegacyGrowthFormatVersion = 1;
		public const int PreviousGrowthFormatVersion = 2;
		public const int SemanticGrowthFormatVersion = 3;
		public const int FirstGuestGrowthFormatVersion = 4;
		public const int TerminalReceiptGrowthFormatVersion = 5;
		public const int FirstGuestPhysicalGrowthFormatVersion = 6;
		public const int CadenceGrowthFormatVersion = 7;
		public const int CurrentGrowthFormatVersion = CadenceGrowthFormatVersion;
		public const int MaxGrowthFields = 8;
		public const int MaxGrowthSources = 64;
		public const int MaxGrowthOutputs = 96;
		public const int MaxGrowthOutboxEvents = 12;
		public const int MaxGrowthObjectCallbacks = 4;
		public const int MaxGrowthCropRows = 96;
		public const int MaxGrowthSectionBytes = 512 * 1024;
		public const int MaxRaidLedgerBytes = 256 * 1024;
		public const int MaxRecentProofs = 64;
		public const int MaxWaterLegs = 24;
		public const int MaxProjections = 64;
		public const int MaxResourceLeases = 32;
		public const int MaxResourceRows = 128;
		public const int MaxCarrySources = 64;
		public const int MaxCarryOutputs = 64;
		public const int MaxSettlementIds = 4;
		public const int MaxLifecycleCollisionIds = 64;
		public const int MaxCoordinate = 4095;
		public const int MaxIdChars = 256;
		public const int MaxNameChars = 512;
		public const int MaxTextChars = 4096;
		public const int MaxIdBytes = MaxIdChars * 4;
		public const int MaxNameBytes = MaxNameChars * 4;
		public const int MaxTextBytes = MaxTextChars * 4;
		public const int MaxPhysicalCount = 1000000;

		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool CanOwnAuthority(KingdomLifecycleBook Book)
		{
			return Book != null && !Book.WireRejected && !Book.Quarantined
				&& Book.FormatVersion == CurrentFormatVersion && ValidRootId(Book.SettlementId)
				&& Book.IdentityBound && ExactSettlementIdentityProof(Book)
				&& LifecycleBookShape(Book);
		}

		public static bool CanOwnAuthority(KingdomCarryBook Book)
		{
			return Book != null && !Book.WireRejected && !Book.Quarantined
				&& Book.OpaqueWireVersion == 0 && Book.OpaquePayload == null
				&& Book.FormatVersion == CurrentCarryFormatVersion && ValidRootId(Book.RealmId)
				&& Book.IdentityBound && ExactCarryIdentityProof(Book)
				&& CarryBookShape(Book);
		}

		/// <summary>Only for one explicit migration. New work accepts Core's exact City.SettlementId.</summary>
		public static string LegacySettlementId(string RealmFaction, long FoundedTick,
			string FirstClaimedZone)
		{
			return HashId("legacy-settlement", delegate(BinaryWriter w)
			{
				CanonicalString(w, RealmFaction);
				w.Write(FoundedTick);
				CanonicalString(w, FirstClaimedZone);
			});
		}

		public static bool BindSettlementIdentity(KingdomLifecycleBook Book, string ExactId,
			bool LegacyMigration, string MigrationKey, ICollection<string> ExistingIds)
		{
			if (Book == null || !ValidRootId(ExactId)) return false;
			if (Book.IdentityBound || !string.IsNullOrEmpty(Book.SettlementId)
				|| !string.IsNullOrEmpty(Book.IdentityProof))
				return ExistingIdsExclude(ExistingIds, ExactId) && CanOwnAuthority(Book)
					&& string.Equals(Book.SettlementId, ExactId, StringComparison.Ordinal)
					&& Book.LegacyIdentity == LegacyMigration
					&& string.Equals(Book.LegacyMigrationKey,
						LegacyMigration ? MigrationKey : null, StringComparison.Ordinal);
			if (LegacyMigration && !ValidRootId(MigrationKey)) return false;
			if (!LegacyMigration && !string.IsNullOrEmpty(MigrationKey)) return false;
			if (!ExistingIdsExclude(ExistingIds, ExactId) || !PristineLifecycleBook(Book) ||
				!PristineGrowthBook(Book.Growth))
				return false;
			KingdomGrowthBook growth = NewBoundGrowth(ExactId);
			if (growth == null) return false;
			Book.SettlementId = ExactId;
			Book.LegacyIdentity = LegacyMigration;
			Book.LegacyMigrationKey = LegacyMigration ? MigrationKey : null;
			Book.IdentityBound = true;
			Book.IdentityProof = SettlementIdentityProof(Book.SettlementId,
				Book.LegacyIdentity, Book.LegacyMigrationKey);
			Book.Growth = growth;
			return ExactSettlementIdentityProof(Book);
		}

		public static bool BindCarryIdentity(KingdomCarryBook Book, string RealmId,
			ICollection<string> SettlementIds, bool LegacyMigration, string MigrationKey)
		{
			if (Book == null || !ValidRootId(RealmId)) return false;
			List<string> frozen;
			if (!TryFrozenSettlementSet(SettlementIds, out frozen)) return false;
			if (Book.IdentityBound || !string.IsNullOrEmpty(Book.RealmId)
				|| !string.IsNullOrEmpty(Book.IdentityProof)
				|| (Book.SettlementIds != null && Book.SettlementIds.Count > 0))
				return CanOwnAuthority(Book)
					&& string.Equals(Book.RealmId, RealmId, StringComparison.Ordinal)
					&& Book.LegacyIdentity == LegacyMigration
					&& string.Equals(Book.LegacyMigrationKey,
						LegacyMigration ? MigrationKey : null, StringComparison.Ordinal)
					&& ExactStringList(Book.SettlementIds, frozen);
			if (!PristineCarryBook(Book)) return false;
			if (LegacyMigration ? !ValidRootId(MigrationKey) : !string.IsNullOrEmpty(MigrationKey))
				return false;
			Book.RealmId = RealmId;
			Book.SettlementIds = frozen;
			Book.LegacyIdentity = LegacyMigration;
			Book.LegacyMigrationKey = LegacyMigration ? MigrationKey : null;
			Book.IdentityBound = true;
			Book.IdentityProof = CarryIdentityProof(Book.RealmId, Book.SettlementIds,
				Book.LegacyIdentity, Book.LegacyMigrationKey);
			return ExactCarryIdentityProof(Book);
		}

		/// <summary>Builds the first city's two authority books off-graph. Dirty dormant
		/// books are evidence and are never overwritten during first publication.</summary>
		public static bool TryPrepareFirstIdentityBooks(KingdomLifecycleBook ExistingLifecycle,
			KingdomCarryBook ExistingCarry, string RealmId, string SettlementId,
			out KingdomLifecycleBook Lifecycle, out KingdomCarryBook Carry)
		{
			Lifecycle = null;
			Carry = null;
			KingdomLifecycleBook sourceLifecycle = ExistingLifecycle ??
				new KingdomLifecycleBook();
			KingdomCarryBook sourceCarry = ExistingCarry ?? new KingdomCarryBook();
			if (!PristineLifecycleBook(sourceLifecycle) ||
				!PristineCarryBook(sourceCarry)) return false;
			KingdomLifecycleBook lifecycle = new KingdomLifecycleBook();
			KingdomCarryBook carry = new KingdomCarryBook();
			if (!BindSettlementIdentity(lifecycle, SettlementId, LegacyMigration: false,
				MigrationKey: null, ExistingIds: new List<string>()) ||
				!BindCarryIdentity(carry, RealmId, new List<string> { SettlementId },
					LegacyMigration: false, MigrationKey: null)) return false;
			Lifecycle = lifecycle;
			Carry = carry;
			return true;
		}

		/// <summary>Preflights a monotone exact-city expansion without changing the book.</summary>
		public static bool CanExpandCarryIdentity(KingdomCarryBook Book, string RealmId,
			ICollection<string> SettlementIds, out string Failure)
		{
			Failure = null;
			if (!CanOwnAuthority(Book))
			{
				Failure = "Carry identity expansion requires bound exact authority.";
				return false;
			}
			List<string> frozen;
			if (!TryFrozenSettlementSet(SettlementIds, out frozen))
			{
				Failure = "Carry identity expansion candidate is malformed or exceeds cap.";
				return false;
			}
			if (!CanOwnAuthority(Book))
			{
				Failure = "Carry authority changed while its expansion candidate was frozen.";
				return false;
			}
			if (!string.Equals(Book.RealmId, RealmId, StringComparison.Ordinal))
			{
				Failure = "The immutable carry realm changed during identity expansion.";
				return false;
			}
			if (ExactStringList(Book.SettlementIds, frozen)) return true;
			for (int i = 0; i < Book.SettlementIds.Count; i++)
				if (!frozen.Contains(Book.SettlementIds[i]))
				{
					Failure = "An exact carry settlement identity was removed or replaced.";
					return false;
				}
			if (Book.Open != null)
			{
				Failure = "Carry identity expansion deferred while a haul receipt is open.";
				return false;
			}
			return true;
		}

		/// <summary>Publishes a preflighted monotone exact-city expansion and its new proof.</summary>
		public static bool ExpandCarryIdentity(KingdomCarryBook Book, string RealmId,
			ICollection<string> SettlementIds, out string Failure)
		{
			Failure = null;
			if (Book == null || !CanOwnAuthority(Book))
			{
				Failure = "Carry identity expansion requires bound exact authority.";
				return false;
			}
			List<string> frozen;
			if (!TryFrozenSettlementSet(SettlementIds, out frozen))
			{
				Failure = "Carry identity expansion candidate is malformed or exceeds cap.";
				return false;
			}
			if (!CanOwnAuthority(Book))
			{
				Deny(Book, "carry authority changed while its expansion candidate was frozen");
				Failure = Book.Fault;
				return false;
			}
			if (!string.Equals(Book.RealmId, RealmId, StringComparison.Ordinal))
			{
				Deny(Book, "immutable carry realm changed during identity expansion");
				Failure = Book.Fault;
				return false;
			}
			if (ExactStringList(Book.SettlementIds, frozen)) return true;
			for (int i = 0; i < Book.SettlementIds.Count; i++)
				if (!frozen.Contains(Book.SettlementIds[i]))
				{
					Deny(Book, "exact carry settlement identity was removed or replaced");
					Failure = Book.Fault;
					return false;
				}
			if (Book.Open != null)
			{
				Failure = "Carry identity expansion deferred while a haul receipt is open.";
				return false;
			}
			List<string> previous = Book.SettlementIds;
			string previousProof = Book.IdentityProof;
			Book.SettlementIds = frozen;
			Book.IdentityProof = CarryIdentityProof(Book.RealmId, Book.SettlementIds,
				Book.LegacyIdentity, Book.LegacyMigrationKey);
			if (CanOwnAuthority(Book)) return true;
			Book.SettlementIds = previous;
			Book.IdentityProof = previousProof;
			Deny(Book, "expanded carry identity did not retain exact authority");
			Failure = Book.Fault;
			return false;
		}

	}
}
