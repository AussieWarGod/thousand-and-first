using System;
using System.Collections.Generic;
using System.Reflection;

using Qud.API;
using XRL;
using XRL.World;
using XRL.World.Anatomy;

namespace ThousandAndFirst
{
	internal readonly struct KingdomLabOwnershipSnapshot
	{
		public readonly string ProcedureKey;
		public readonly string JobId;
		public readonly string PatientId;
		public readonly int BodyPartId;
		public readonly string BearerId;
		public readonly string Grants;
		public readonly int Source;
		public readonly int Attach;
		public readonly string Manager;
		public readonly string Detail;
		public readonly string Fingerprint;
		public readonly int PartOrdinal;
		public readonly string EffectNonce;

		public KingdomLabOwnershipSnapshot(string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, string BearerId)
		{
			this.ProcedureKey = ProcedureKey ?? "";
			this.JobId = JobId ?? "";
			this.PatientId = PatientId ?? "";
			this.BodyPartId = BodyPartId;
			this.BearerId = BearerId ?? "";
			Grants = "";
			Source = -1;
			Attach = -1;
			Manager = "";
			Detail = "";
			Fingerprint = "";
			PartOrdinal = -1;
			EffectNonce = "";
		}

		public KingdomLabOwnershipSnapshot(string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, string BearerId, string Grants, int Source, int Attach,
			string Manager, string Detail, string Fingerprint, int PartOrdinal,
			string EffectNonce = "")
		{
			this.ProcedureKey = ProcedureKey ?? "";
			this.JobId = JobId ?? "";
			this.PatientId = PatientId ?? "";
			this.BodyPartId = BodyPartId;
			this.BearerId = BearerId ?? "";
			this.Grants = Grants ?? "";
			this.Source = Source;
			this.Attach = Attach;
			this.Manager = Manager ?? "";
			this.Detail = Detail ?? "";
			this.Fingerprint = Fingerprint ?? "";
			this.PartOrdinal = PartOrdinal;
			this.EffectNonce = EffectNonce ?? "";
		}
	}

	internal sealed class KingdomLabGrantAttempt
	{
		public KingdomLabOwnedTargetState State = KingdomLabOwnedTargetState.Uncertain;
		public IPart ExactPart;
		public BodyPart ExactBodyPart;
		public int BodyPartId;
		public int PartOrdinal = -1;
		public string BearerId = "";
		public string Failure = "";
	}

	internal sealed class KingdomLabOwnedTarget
	{
		public GameObject Bearer;
		public XRL.World.Parts.r_KingdomLabEffectLedger Ledger;
		public IPart ExactPart;
		public BodyPart ExactBodyPart;
	}
}
