using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// What the save had to say about this realm's covenants, and every answer is one of these.
	/// <para>
	/// The first two are both "no covenant" and are still kept apart on purpose. A save that has no
	/// archive at all predates this family or has simply never sealed anything; a bound archive
	/// with no rows is a realm that demonstrably has none. Neither is a covenant, and neither is
	/// allowed to be reported as an empty success that a reader might mistake for a completed
	/// lookup. The rest are refusals, and every one of them fails closed.
	/// </para>
	/// </summary>
	public enum KingdomVillageCovenantEvidence : byte
	{
		/// <summary>This save carries no covenant archive. Older saves land here, and correctly.</summary>
		ArchiveAbsent = 0,

		/// <summary>The realm has an archive and has recorded no covenant in it.</summary>
		NoneRecorded = 1,

		/// <summary>At least one exact completed covenant is on record.</summary>
		Recorded = 2,

		/// <summary>A later build wrote the archive. It is carried, and it is not read.</summary>
		Future = 3,

		/// <summary>The archive would not read. Its bytes are kept as the evidence.</summary>
		Quarantined = 4,

		/// <summary>The archive in this save belongs to another realm.</summary>
		WrongRealm = 5,

		/// <summary>A recorded covenant's village no longer passes its own native gate.</summary>
		NativeInvalid = 6
	}

	/// <summary>
	/// What the living world says about one archived covenant, right now.
	/// <para>
	/// Everything here is read from the current game and none of it is evidence that the covenant
	/// happened &mdash; the row in the archive is that, and it is the only thing that is. Standing
	/// in particular is carried as a projection and never as a gate: a village that has since come
	/// to resent the realm still sealed the covenant it sealed, and a village that adores it never
	/// sealed one it did not.
	/// </para>
	/// </summary>
	public sealed class KingdomVillageCovenantProjection
	{
		public string ReceiptId = "";

		/// <summary>Whether the village's faction still exists and both engine registries agree.</summary>
		public bool FactionCoherent;

		/// <summary>Whether that faction still declares itself a village.</summary>
		public bool DeclaresVillage;

		/// <summary>The realm's standing with it today. Reported, never believed.</summary>
		public int CurrentStanding;
	}

	/// <summary>
	/// Turns archived covenants and today's read-only observations into one owner of the joint
	/// civic view. It looks nothing up and changes nothing: every fact it uses arrives as an
	/// argument, which is what makes the whole judgement testable without a game running.
	/// </summary>
	public static class KingdomVillageCovenantView
	{
		public const string OwnerKey = "covenant";
		public const string ReceiptPrefix = "taf:village-covenant-view:v1:";
		private const string ReceiptDomain = "TAF-VILLAGE-COVENANT-VIEW-V1";

		/// <summary>How many covenants the summary names before it starts counting instead.</summary>
		public const int MaxNamedInSummary = 6;

		public static KingdomJointCivicOwnerView Owner(KingdomVillageCovenantEvidence evidence,
			string realmId, KingdomVillageCovenantArchive archive,
			IList<KingdomVillageCovenantProjection> projections, string fault)
		{
			switch (evidence)
			{
			case KingdomVillageCovenantEvidence.ArchiveAbsent:
				return KingdomJointCivicViewAdapters.CovenantMissing();
			case KingdomVillageCovenantEvidence.NoneRecorded:
				return KingdomJointCivicViewAdapters.Missing(OwnerKey,
					"The realm keeps a village-covenant archive and has recorded no covenant in it.");
			case KingdomVillageCovenantEvidence.Recorded:
				return Recorded(realmId, archive, projections, fault);
			case KingdomVillageCovenantEvidence.Future:
				return KingdomJointCivicViewAdapters.Invalid(OwnerKey, Reason(fault,
					"The village-covenant archive was written by a newer build; it is carried "
					+ "whole and cannot be read here."));
			case KingdomVillageCovenantEvidence.Quarantined:
				return KingdomJointCivicViewAdapters.Invalid(OwnerKey, Reason(fault,
					"The village-covenant archive would not read and is kept as evidence."));
			case KingdomVillageCovenantEvidence.WrongRealm:
				return KingdomJointCivicViewAdapters.Invalid(OwnerKey, Reason(fault,
					"The village-covenant archive in this save belongs to another realm."));
			case KingdomVillageCovenantEvidence.NativeInvalid:
				return KingdomJointCivicViewAdapters.Invalid(OwnerKey, Reason(fault,
					"An archived covenant's village no longer passes its own native gate."));
			default:
				return KingdomJointCivicViewAdapters.Invalid(OwnerKey,
					"The village-covenant archive reported a state this build does not define.");
			}
		}

		/// <summary>
		/// A valid covenant owner is built from a whole archive, never from a handful of rows.
		/// <para>
		/// Per-row validation is not enough and the gap is not theoretical. Rows that each pass on
		/// their own can still be two covenants under one founding transaction, the same receipt
		/// twice, covenants from two realms side by side, or a set whose count and revision
		/// disagree &mdash; and an aggregate receipt id computed over any of those would be a
		/// stable, confident name for a history that never happened. So the archive is put through
		/// its own rules first, and only then is anything said about what it contains.
		/// </para>
		/// </summary>
		private static KingdomJointCivicOwnerView Recorded(string realmId,
			KingdomVillageCovenantArchive archive,
			IList<KingdomVillageCovenantProjection> projections, string fault)
		{
			string invalid = "There is no covenant archive to report.";
			if (archive == null || !KingdomVillageCovenantRules.TryValidate(archive, out invalid))
				return KingdomJointCivicViewAdapters.Invalid(OwnerKey, Reason(fault, invalid));
			if (archive.State != KingdomVillageCovenantState.Compatible)
				return KingdomJointCivicViewAdapters.Invalid(OwnerKey, Reason(fault,
					"The village-covenant archive is " + archive.State + " and reports nothing."));
			if (!KingdomIdentityRules.IsRealmId(realmId) || !archive.IdentityBound
				|| !string.Equals(archive.RealmId, realmId, StringComparison.Ordinal))
				return KingdomJointCivicViewAdapters.Invalid(OwnerKey, Reason(fault,
					"The village-covenant archive is not bound to this exact realm."));
			IList<KingdomVillageCovenantReceipt> rows = archive.Rows;
			if (rows.Count == 0 || projections == null || projections.Count != rows.Count)
				return KingdomJointCivicViewAdapters.Invalid(OwnerKey, Reason(fault,
					"The archived covenants and today's observations of them do not correspond."));
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomVillageCovenantProjection seen = projections[i];
				if (seen == null || !string.Equals(seen.ReceiptId, rows[i].ReceiptId,
					StringComparison.Ordinal))
					return KingdomJointCivicViewAdapters.Invalid(OwnerKey,
						"An archived covenant was observed against the wrong receipt.");
				if (!seen.FactionCoherent || !seen.DeclaresVillage)
					return KingdomJointCivicViewAdapters.Invalid(OwnerKey,
						"The village named by an archived covenant no longer passes its own "
						+ "native gate, so this owner reports nothing rather than guessing.");
			}
			string receipt = AggregateReceiptId(realmId, rows);
			string text = Summary(rows, projections);
			if (receipt == null || !KingdomJointCivicViewRules.Report(text))
				return KingdomJointCivicViewAdapters.Invalid(OwnerKey,
					"The covenant owner could not be summarised inside its own bounds.");
			return new KingdomJointCivicOwnerView
			{
				OwnerKey = OwnerKey,
				State = KingdomJointOwnerState.Valid,
				SourceVersion = KingdomVillageCovenantCodec.CurrentWireVersion,
				SourceReceiptId = receipt,
				Text = text
			};
		}

		/// <summary>
		/// One stable name for the whole set of covenants on record.
		/// <para>
		/// It is derived from the realm and from the archived receipt ids in their canonical order,
		/// and from nothing that moves. Folding today's standing into it would give the joint view
		/// a source id that changed whenever a village's mood did, which is the opposite of what a
		/// source id is for.
		/// </para>
		/// </summary>
		public static string AggregateReceiptId(string realmId,
			IList<KingdomVillageCovenantReceipt> rows)
		{
			if (!KingdomIdentityRules.IsRealmId(realmId) || rows == null) return null;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				{
					using (BinaryWriter writer = new BinaryWriter(stream,
						new UTF8Encoding(false, true), true))
					{
						writer.Write(ReceiptDomain);
						writer.Write(realmId);
						writer.Write(rows.Count);
						for (int i = 0; i < rows.Count; i++)
						{
							if (rows[i] == null || rows[i].ReceiptId == null) return null;
							writer.Write(rows[i].ReceiptId);
						}
						writer.Flush();
					}
					using (SHA256 sha = SHA256.Create())
					{
						byte[] digest = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(ReceiptPrefix);
						for (int i = 0; i < digest.Length; i++)
							text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
						return text.ToString();
					}
				}
			}
			catch (EncoderFallbackException) { return null; }
		}

		/// <summary>
		/// A bounded account of the covenants, naming the first few and counting the rest.
		/// <para>
		/// The owner's report has a ceiling, and forty-eight covenants would not fit under it. The
		/// answer is not to truncate mid-sentence but to stop naming and start counting, and to say
		/// where the rest can be read &mdash; <see cref="KingdomVillageCovenantRegister"/> hands
		/// them back a page at a time without any of them passing through a bounded summary.
		/// </para>
		/// </summary>
		public static string Summary(IList<KingdomVillageCovenantReceipt> rows,
			IList<KingdomVillageCovenantProjection> projections)
		{
			string counted = Counted(rows.Count);
			string named = Named(rows, projections, counted);
			// Names are the better report and the count is the one that always fits. A village
			// whose display name is at its own cap must not be able to push this owner into
			// reporting nothing at all, so the shorter form is the floor rather than a failure.
			return KingdomJointCivicViewRules.Report(named) ? named : counted;
		}

		private static string Counted(int count)
		{
			return count == 1
				? "One village covenant stands on record, a completed rite this realm paid for."
				: count.ToString(CultureInfo.InvariantCulture)
					+ " village covenants stand on record, every one of them a completed rite "
					+ "this realm paid for.";
		}

		private static string Named(IList<KingdomVillageCovenantReceipt> rows,
			IList<KingdomVillageCovenantProjection> projections, string opening)
		{
			StringBuilder text = new StringBuilder(opening);
			int named = rows.Count < MaxNamedInSummary ? rows.Count : MaxNamedInSummary;
			for (int i = 0; i < named; i++)
			{
				text.Append("\n— ");
				text.Append(rows[i].VillageDisplayName);
				text.Append(", sealed at standing ");
				text.Append(rows[i].SealedStanding.ToString(CultureInfo.InvariantCulture));
				text.Append("; the realm stands at ");
				text.Append(projections[i].CurrentStanding.ToString(CultureInfo.InvariantCulture));
				text.Append(" with them today.");
			}
			if (rows.Count > named)
			{
				text.Append("\n— and ");
				text.Append((rows.Count - named).ToString(CultureInfo.InvariantCulture));
				text.Append(" more, which the covenant register reads a page at a time.");
			}
			return text.ToString();
		}

		private static string Reason(string fault, string fallback)
		{
			return string.IsNullOrWhiteSpace(fault) ? fallback : fault;
		}
	}
}
