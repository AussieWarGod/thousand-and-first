using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Anatomy;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomLab
	{
		/// <summary>Freezes the exact witnessed wording once, before the C18 lease.</summary>
		private static bool TryFreezeCompletedBodyHistoryWitness(GameObject Actor,
			r_KingdomLabJob Job, out string Failure)
		{
			Failure = null;
			if (Job == null)
				return FailBodyEvidence("the lab job is unavailable", out Failure);
			if (!Job.BodyHistoryRequiresRulerLife) return true;
			bool hasTick = Job != null && Job.BodyHistoryWitnessedTick >= 0L;
			bool hasFact = Job != null && !string.IsNullOrEmpty(Job.BodyHistoryPartFact);
			bool hasNonce = !string.IsNullOrEmpty(Job.BodyHistoryEffectNonce);
			bool hasOwner = !string.IsNullOrEmpty(Job.BodyHistoryOwnerReceiptId);
			if (hasTick != hasFact || hasNonce != hasOwner)
				return FailBodyEvidence("the frozen body-history witness is partial", out Failure);
			if (The.Game == null || !GameObject.Validate(Actor)
				|| !ReferenceEquals(The.Player, Actor)
				|| !string.Equals(Actor.IDIfAssigned, Job?.PatientId,
					StringComparison.Ordinal)
				|| !TryReadExactPatientReceipt(Actor, Job,
					out string effectNonce, out BodyPart part))
				return FailBodyEvidence("exact completed patient evidence is unavailable",
					out Failure);
			string fact = Job.BodyHistoryPartFact;
			long tick = Job.BodyHistoryWitnessedTick;
			if (!hasFact)
			{
				string display = string.IsNullOrWhiteSpace(Job.FrozenName)
					? Job.ProcedureKey : Job.FrozenName;
				fact = display + " at " + part.GetOrdinalName();
				tick = Math.Max(0L, The.Game.TimeTicks);
			}
			try
			{
				if (string.IsNullOrWhiteSpace(fact) || fact.IndexOf('\0') >= 0
					|| new UTF8Encoding(false, true).GetByteCount(fact)
						> KingdomBodyHistoryRules.MaxTextBytes)
					return FailBodyEvidence("body-part wording exceeds its cap", out Failure);
			}
			catch (EncoderFallbackException)
			{
				return FailBodyEvidence("body-part wording is not valid UTF-8", out Failure);
			}
			string owner = CompletedBodyHistoryOwner(Job, effectNonce);
			if (!KingdomBodyHistoryRules.ValidEffectNonce(effectNonce)
				|| !KingdomBodyHistoryRules.ValidCompletedLabOwner(owner)
				|| hasNonce && (!string.Equals(Job.BodyHistoryEffectNonce, effectNonce,
					StringComparison.Ordinal) || !string.Equals(Job.BodyHistoryOwnerReceiptId,
					owner, StringComparison.Ordinal)))
				return FailBodyEvidence("the frozen effect nonce or history owner changed",
					out Failure);
			Job.BodyHistoryPartFact = fact;
			Job.BodyHistoryWitnessedTick = tick;
			Job.BodyHistoryEffectNonce = effectNonce;
			Job.BodyHistoryOwnerReceiptId = owner;
			return true;
		}

		/// <summary>
		/// Builds D5 evidence at the exact terminal seam. It writes nothing; C18 owns
		/// the later section commit and the lab receipt remains until readback succeeds.
		/// </summary>
		internal static bool TryBuildCompletedBodyHistoryEvidence(GameObject Actor,
			KingdomSystem System, r_KingdomLabJob Job,
			out KingdomWitnessedBodyEventEvidence Evidence, out string Failure)
		{
			Evidence = null;
			Failure = null;
			string actorId = Actor?.IDIfAssigned;
			GameObject building = Job?.ParentObject;
			string buildingId = building?.IDIfAssigned;
			// Marker cleanup may have committed before the canonical terminal row refused.
			// The still-active registry remains the authority for that exact retry.
			bool preparedTerminal = Job != null && Job.State == KingdomLabJobPhase.Applying
				&& !Job.RegistryFinalized;
			bool finalTerminal = Job != null && Job.State == KingdomLabJobPhase.Complete
				&& Job.MarkerCleaned && Job.RegistryFinalized;
			if (Job == null || !Job.BodyHistoryRequiresRulerLife
				|| (Job.BodyHistoryState != KingdomLabBodyHistoryPhase.Pending
					&& Job.BodyHistoryState != KingdomLabBodyHistoryPhase.Applied))
				return FailBodyEvidence("this job has no pending body-history contract",
					out Failure);
			if (!KingdomBodyHistoryRulerLifeRuntime.TryReadCurrent(System, Actor,
				out KingdomRulerLifeSnapshot rulerLife, out Failure)) return false;
			if (!GameObject.Validate(Actor) || !ReferenceEquals(The.Player, Actor)
				|| Actor.CurrentCell == null
				|| Actor.CurrentZone == null || Actor.Body == null
				|| !GameObject.Validate(building) || building.CurrentZone == null
				|| building.CurrentCell == null
				|| !ReferenceEquals(Actor.CurrentZone, building.CurrentZone)
				|| string.IsNullOrEmpty(actorId) || string.IsNullOrEmpty(buildingId)
				|| Job == null || System == null || The.Game == null || !System.Founded
				|| (!preparedTerminal && !finalTerminal) || Job.SchemaQuarantined
				|| !Job.EffectCommitted || !Job.OwnershipPublished || !Job.StandingApplied
				|| Job.BodyHistoryWitnessedTick < 0L
				|| !string.Equals(actorId, Job.PatientId, StringComparison.Ordinal)
				|| !string.Equals(buildingId, Job.BuildingId, StringComparison.Ordinal)
				|| !string.Equals(The.Game.GameID, Job.GameId, StringComparison.Ordinal)
				|| !string.Equals(System.CurrentRealmId, Job.RealmId, StringComparison.Ordinal)
				|| System.FoundedTick != Job.RealmFoundedTick
				|| rulerLife.SuccessionOrdinal != Job.RulerSuccessionOrdinal
				|| !string.Equals(rulerLife.RulerLifeId, Job.RulerLifeId,
					StringComparison.Ordinal)
				|| !string.Equals(rulerLife.BodyObjectId, "taf:object:" + actorId,
					StringComparison.Ordinal))
				return FailBodyEvidence("completed lab authority is unavailable", out Failure);

			KingdomLabRegistryStatus registryStatus = finalTerminal
				? KingdomLabRegistryStatus.Complete : KingdomLabRegistryStatus.Active;
			if (!TryReadBodyHistoryRegistry(Job, registryStatus,
				out KingdomLabRegistryEntry _))
				return FailBodyEvidence("canonical lab completion cut is unavailable",
					out Failure);
			if (!TryReadExactPatientReceipt(Actor, Job,
				out string effectNonce, out BodyPart _)
				|| !string.Equals(effectNonce, Job.BodyHistoryEffectNonce,
					StringComparison.Ordinal)
				|| !string.Equals(CompletedBodyHistoryOwner(Job, effectNonce),
					Job.BodyHistoryOwnerReceiptId, StringComparison.Ordinal))
				return FailBodyEvidence("exact completed patient receipt is unavailable",
					out Failure);

			KingdomWitnessedBodyEventEvidence candidate =
				new KingdomWitnessedBodyEventEvidence
				{
					OwnerKind = KingdomBodyHistoryRules.CompletedLabProcedureKind,
					OwnerReceiptId = Job.BodyHistoryOwnerReceiptId,
					ResidentIdentity = Job.RulerLifeId,
					BodyObjectId = "taf:object:" + actorId,
					ProcedureKey = Job.ProcedureKey,
					BodyPartFact = Job.BodyHistoryPartFact,
					WitnessedTick = Job.BodyHistoryWitnessedTick
				};
			if (!KingdomBodyHistoryRules.ValidEvidence(candidate))
				return FailBodyEvidence("completed lab evidence exceeds its bounded schema",
					out Failure);
			Evidence = candidate;
			return true;
		}

		private static string CompletedBodyHistoryOwner(r_KingdomLabJob Job,
			string EffectNonce)
		{
			if (Job == null) return null;
			return KingdomBodyHistoryRules.CompletedLabProcedureReceiptId(
				Job.GameId, Job.RealmId,
				Job.RealmFoundedTick.ToString(CultureInfo.InvariantCulture),
				Job.RulerSuccessionOrdinal.ToString(CultureInfo.InvariantCulture), Job.RulerLifeId,
				Job.BuildingId, Job.PatientId, Job.JobId, Job.ProcedureKey,
				Job.FrozenFingerprint, EffectNonce,
				Job.EffectBodyPartId.ToString(CultureInfo.InvariantCulture),
				Job.EffectPartOrdinal.ToString(CultureInfo.InvariantCulture));
		}

		private static bool TryReadBodyHistoryRegistry(r_KingdomLabJob Job,
			KingdomLabRegistryStatus Status,
			out KingdomLabRegistryEntry Entry)
		{
			Entry = null;
			bool quarantined;
			List<KingdomLabRegistryEntry> rows = KingdomLabRules.ParseRegistry(
				The.Game.GetStringGameState(LabRegistryState, ""), out quarantined);
			int index = KingdomLabRules.IndexOfRegistry(rows, Job.JobId);
			if (quarantined || index < 0
				|| rows[index].Status != Status
				|| !KingdomLabRules.RegistryAuthority(rows[index],
					RegistryEntry(Job, Status),
					RequireActive: Status == KingdomLabRegistryStatus.Active)) return false;
			Entry = rows[index].Copy();
			return true;
		}

		private static bool TryReadExactPatientReceipt(GameObject Actor,
			r_KingdomLabJob Job, out string EffectNonce, out BodyPart Part)
		{
			EffectNonce = null;
			Part = KingdomProcedures.ExactLiveBodyPart(Actor, Job.EffectBodyPartId);
			r_KingdomLabRecord record = Actor.GetPart<r_KingdomLabRecord>();
			if (Part == null || Part._ID != Job.EffectBodyPartId || record == null
				|| record.RegistryQuarantined || !ExactRecordShape(record)) return false;
			int found = -1;
			for (int i = 0; i < record.Keys.Count; i++)
			{
				if (!string.Equals(record.JobIds[i], Job.JobId, StringComparison.Ordinal))
					continue;
				if (found >= 0) return false;
				found = i;
			}
			if (found < 0 || !ExactRecordRow(record, found, Actor, Part, Job)) return false;
			EffectNonce = record.EffectNonces[found];
			return KingdomBodyHistoryRules.ValidEffectNonce(EffectNonce);
		}

		private static bool ExactRecordShape(r_KingdomLabRecord Record)
		{
			int count = Record.Keys?.Count ?? -1;
			return count >= 0 && count <= KingdomLabRules.MaxEffectRows
				&& Count(Record.Places) == count && Count(Record.OnWeapon) == count
				&& Count(Record.BodyPartIds) == count && Count(Record.BearerIds) == count
				&& Count(Record.JobIds) == count && Count(Record.DisplayNames) == count
				&& Count(Record.Grants) == count && Count(Record.Sources) == count
				&& Count(Record.Attaches) == count && Count(Record.Managers) == count
				&& Count(Record.Details) == count && Count(Record.Fingerprints) == count
				&& Count(Record.PartOrdinals) == count && Count(Record.EffectNonces) == count;
		}

		private static bool ExactRecordRow(r_KingdomLabRecord Record, int At,
			GameObject Actor, BodyPart Part, r_KingdomLabJob Job)
		{
			GameObject bearer = Job.FrozenAttach == (int)LabAttach.Weapon
				? Part.DefaultBehavior : Actor;
			string expectedPlace = Job.FrozenSource == (int)LabSource.Mutation
				? "" : Part.Type;
			return GameObject.Validate(bearer)
				&& string.Equals(bearer.IDIfAssigned, Job.BearerId, StringComparison.Ordinal)
				&& string.Equals(Record.Keys[At], Job.ProcedureKey,
					StringComparison.OrdinalIgnoreCase)
				&& string.Equals(Record.Places[At], expectedPlace, StringComparison.Ordinal)
				&& Record.OnWeapon[At] == (Job.FrozenAttach == (int)LabAttach.Weapon)
				&& Record.BodyPartIds[At] == Job.EffectBodyPartId
				&& string.Equals(Record.BearerIds[At], Job.BearerId, StringComparison.Ordinal)
				&& string.Equals(Record.DisplayNames[At], Job.FrozenName,
					StringComparison.Ordinal)
				&& string.Equals(Record.Grants[At], Job.FrozenGrants, StringComparison.Ordinal)
				&& Record.Sources[At] == Job.FrozenSource
				&& Record.Attaches[At] == Job.FrozenAttach
				&& string.Equals(Record.Managers[At], Job.FrozenManager,
					StringComparison.Ordinal)
				&& string.Equals(Record.Details[At], Job.FrozenDetail,
					StringComparison.Ordinal)
				&& string.Equals(Record.Fingerprints[At], Job.FrozenFingerprint,
					StringComparison.Ordinal)
				&& Record.PartOrdinals[At] == Job.EffectPartOrdinal;
		}

		private static int Count<T>(List<T> Values)
		{
			return Values == null ? -1 : Values.Count;
		}

		private static bool FailBodyEvidence(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
