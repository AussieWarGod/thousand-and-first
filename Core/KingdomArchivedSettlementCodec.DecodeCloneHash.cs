using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomArchivedSettlementCodec
	{

		/// <summary>Returns false for malformed/current-unsupported data. A strictly newer version
		/// returns false with <paramref name="FutureVersion"/> set so the caller can retain the exact
		/// opaque bytes and quarantine instead of interpreting a prefix.</summary>
		public static bool TryDecode(byte[] Payload, out KingdomSettlement Value,
			out int FutureVersion, out string Failure)
		{
			Value = null;
			FutureVersion = 0;
			Failure = null;
			try
			{
				if (Payload == null || Payload.Length < 8 || Payload.Length > MaxPayloadBytes)
					throw new InvalidDataException("Archived settlement payload length is invalid.");
				using (MemoryStream stream = new MemoryStream(Payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
				{
					if (reader.ReadInt32() != Magic)
						throw new InvalidDataException("Archived settlement marker is invalid.");
					int version = reader.ReadInt32();
					if (version > CurrentVersion)
					{
						FutureVersion = version;
						Failure = "Archived settlement uses future version " + version + ".";
						return false;
					}
					if (version != LegacyVersion && version != PreviousVersion
						&& version != RaidVersion
						&& version != ResidentIdentityVersion
						&& version != ExtensionIdentityVersion
						&& version != SalvageVersion
						&& version != BehaviourVersion
						&& version != PhysicalHappeningVersion
						&& version != ExactLogisticsVersion
						&& version != DefensiveReservationVersion
						&& version != SemanticSelectionVersion
						&& version != CurrentVersion)
						throw new InvalidDataException("Archived settlement version is unsupported.");
					string shape = ReadString(reader, MaxShapeBytes, Required: true);
					if (!string.Equals(shape, Shape(typeof(KingdomSettlement), version),
						StringComparison.Ordinal))
						throw new InvalidDataException("Archived settlement schema shape is unknown.");
					object decoded = ReadValue(reader, typeof(KingdomSettlement), 0,
						new Budget(), version);
					if (stream.Position != stream.Length)
						throw new InvalidDataException("Archived settlement has trailing bytes.");
					Value = (KingdomSettlement)decoded;
					if (version < SemanticSelectionVersion)
						StageHistoricalSemanticPlan(Value);
					if (version == LegacyVersion && Value != null)
					{
						if (!KingdomLifecycleRules.StageLegacyGrowthMigration(Value.LifecycleBook))
							throw new InvalidDataException(
								"Archived settlement legacy lifecycle could not stage Growth migration.");
						KingdomLifecycleRules.QuarantineLegacyRaidAuthority(Value.LifecycleBook);
					}
					else if (version == PreviousVersion && Value != null
						&& !KingdomLifecycleRules.StageRaidMigrationFromV6(Value.LifecycleBook))
						throw new InvalidDataException(
							"Archived settlement v2 lifecycle could not stage raid migration.");
					else if (version >= RaidVersion && version < CurrentVersion && Value != null
						&& !KingdomLifecycleWireCodec.UpgradeArchivedRaidLedgerV1(
							Value.LifecycleBook))
						throw new InvalidDataException(
							"Archived settlement historical raid ledger could not migrate.");
					if (Value != null && (Value.LifecycleBook == null
						|| Value.LifecycleBook.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion
						|| !KingdomRaidIncidentRules.ValidLedger(Value.LifecycleBook.RaidLedger)))
						throw new InvalidDataException(
							"Archived settlement raid evidence is malformed.");
					return true;
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message);
				Value = null;
				return false;
			}
		}

		private static void StageHistoricalSemanticPlan(KingdomSettlement Value)
		{
			// Archive v1-v10 wrote the city-v2 carrier and omitted v3's exact resident-origin
			// and frozen-arrival columns. Normalize that carrier at the decoder boundary so a
			// successfully decoded historical settlement is immediately readable by current city
			// rules; callers must not need an unrelated save-load normalization pass to finish the
			// archive migration.
			Value?.City?.Normalize();
			KingdomGrowthBook growth = Value?.LifecycleBook?.Growth;
			if (growth == null) return;
			if (growth.FormatVersion == KingdomLifecycleRules.PreviousGrowthFormatVersion)
				growth.FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion;
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			if (candidate == null) return;
			candidate.LegacySemanticPlan = true;
			candidate.SemanticPlanVersion = 0;
			candidate.SemanticStreamId = null;
			candidate.SemanticEventKind = 0U;
			candidate.PlannedOrigin = null;
			candidate.PlannedCreed = null;
			candidate.PlannedName = null;
			candidate.PlannedArrived = null;
			candidate.ArrivalX = -1;
			candidate.ArrivalY = -1;
		}

		public static bool TryClone(KingdomSettlement Source, out KingdomSettlement Clone,
			out string Failure)
		{
			Clone = null;
			if (!TryEncode(Source, out byte[] payload, out Failure)) return false;
			int future;
			return TryDecode(payload, out Clone, out future, out Failure) && future == 0;
		}

		public static bool TryHash(KingdomSettlement Value, out string Hash,
			out string Failure)
		{
			Hash = null;
			if (!TryEncode(Value, out byte[] payload, out Failure)) return false;
			using (SHA256 algorithm = SHA256.Create())
			{
				byte[] digest = algorithm.ComputeHash(payload);
				StringBuilder text = new StringBuilder(digest.Length * 2);
				for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
				Hash = text.ToString();
				return true;
			}
		}

		public static bool ExactGraph(KingdomSettlement Left, KingdomSettlement Right,
			out string Failure)
		{
			Failure = null;
			if (!StrictMutableRoot(Left, typeof(KingdomSettlement), out Failure) ||
				!StrictMutableRoot(Right, typeof(KingdomSettlement), out Failure) ||
				!ExactReferenceTopology(Left, Right, typeof(KingdomSettlement), 0,
					new Budget(),
					new Dictionary<object, object>(new ReferenceComparer()),
					new Dictionary<object, object>(new ReferenceComparer()), out Failure))
				return false;
			if (!TryEncode(Left, out byte[] left, out Failure) ||
				!TryEncode(Right, out byte[] right, out Failure)) return false;
			if (left.Length != right.Length)
			{
				Failure = "Settlement graph lengths differ.";
				return false;
			}
			int difference = 0;
			for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
			if (difference != 0) Failure = "Settlement graphs differ.";
			return difference == 0;
		}

	}
}
