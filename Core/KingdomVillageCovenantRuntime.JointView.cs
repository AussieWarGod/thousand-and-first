#if !TAF_TESTS
using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The covenant owner of the joint civic view, and the strictest read-only thing in this
	/// family.
	/// <para>
	/// It reads the archive for the exact current realm, validates every row it finds, and then
	/// asks the living world two questions about each of them: does that village's faction still
	/// exist coherently, and does it still call itself a village. Both are answered from the
	/// faction registry that is already loaded. Nothing here loads or thaws a zone, looks up or
	/// borrows an actor, reads a worship default, infers a reaction, or writes one byte anywhere.
	/// </para>
	/// <para>
	/// It reads the whole civic-memory state rather than leasing the section, and that is the
	/// point rather than a shortcut. A lease is the right instrument for changing a section and
	/// the wrong one for looking at it: leases are refused outright when the session has gone
	/// read-only or when the payload came from a later build, and both of those are answers this
	/// view is supposed to give the founder rather than answers it should choke on. A reading that
	/// cannot report "a newer build wrote this" is a reading that will eventually report "no
	/// covenant" instead.
	/// </para>
	/// <para>
	/// Standing is read and reported and is never a gate. A covenant is a thing that happened, and
	/// what a village feels about the realm today is a separate fact that arrived later; letting
	/// today's number decide whether yesterday's rite counted would be exactly the inference this
	/// whole family was built to refuse. So the standing goes into the report as a projection, and
	/// the row in the archive remains the only evidence that anything was ever sealed.
	/// </para>
	/// </summary>
	public static partial class KingdomVillageCovenantRuntime
	{
		/// <summary>Reads the covenant owner for D9's four-owner fan-in.</summary>
		public static KingdomJointCivicOwnerView ReadOwnerForJointView(KingdomSystem System)
		{
			if (!TryOpenAuthority(System, out IKingdomCivicMemoryAuthority authority,
				out string realmId, out string failure))
				return KingdomJointCivicViewAdapters.Invalid(KingdomVillageCovenantView.OwnerKey,
					failure);
			KingdomCivicMemoryState state = authority.Read();
			if (state.Quarantined)
				return KingdomVillageCovenantView.Owner(KingdomVillageCovenantEvidence.Quarantined,
					realmId, null, null, state.Fault);
			if (state.IsFutureOuter)
				return KingdomVillageCovenantView.Owner(KingdomVillageCovenantEvidence.Future,
					realmId, null, null, "This save's civic memory was written by a newer build "
					+ "(envelope version " + state.OuterVersion + "); its covenant archive is "
					+ "carried whole and cannot be read here.");
			KingdomCivicMemorySection section =
				state.Section(KingdomVillageCovenantLease.SectionId);
			if (section == null)
				return KingdomVillageCovenantView.Owner(
					KingdomVillageCovenantEvidence.ArchiveAbsent, realmId, null, null, null);
			return Held(System, realmId, KingdomVillageCovenantCodec.Decode(section.Payload()));
		}

		private static KingdomJointCivicOwnerView Held(KingdomSystem System, string realmId,
			KingdomVillageCovenantArchive archive)
		{
			if (archive.State == KingdomVillageCovenantState.FutureOpaque)
				return KingdomVillageCovenantView.Owner(KingdomVillageCovenantEvidence.Future,
					realmId, null, null, archive.Fault);
			// Held apart from the short circuit above it: a state that is not Compatible carries
			// its own fault, and only a Compatible archive that fails its rules has a validation
			// message worth relaying instead.
			string failure = "";
			if (archive.State != KingdomVillageCovenantState.Compatible
				|| !KingdomVillageCovenantRules.TryValidate(archive, out failure))
				return KingdomVillageCovenantView.Owner(KingdomVillageCovenantEvidence.Quarantined,
					realmId, null, null,
					string.IsNullOrEmpty(archive.Fault) ? failure : archive.Fault);
			if (!archive.IdentityBound
				|| !string.Equals(archive.RealmId, realmId, StringComparison.Ordinal))
				return KingdomVillageCovenantView.Owner(KingdomVillageCovenantEvidence.WrongRealm,
					realmId, null, null, null);
			if (archive.Rows.Count == 0)
				return KingdomVillageCovenantView.Owner(
					KingdomVillageCovenantEvidence.NoneRecorded, realmId, null, null, null);
			return Observed(System, realmId, archive);
		}

		/// <summary>
		/// Applies the two native gates to each archived covenant and reports today's standing
		/// beside each of them, without touching anything.
		/// </summary>
		private static KingdomJointCivicOwnerView Observed(KingdomSystem System, string realmId,
			KingdomVillageCovenantArchive archive)
		{
			List<KingdomVillageCovenantProjection> seen =
				new List<KingdomVillageCovenantProjection>(archive.Rows.Count);
			for (int i = 0; i < archive.Rows.Count; i++)
			{
				KingdomVillageCovenantReceipt row = archive.Rows[i];
				// GetIfExists, not Get: Factions.Get throws on an unknown name, and an archived
				// covenant can name a faction from a mod that is no longer installed. A stranger's
				// name must make this owner report a refusal, not crash the view.
				Faction village = Factions.GetIfExists(row.VillageFactionId);
				bool coherent = KingdomFoundingTransaction.FactionRegistryCoherent(
					row.VillageFactionId, village);
				seen.Add(new KingdomVillageCovenantProjection
				{
					ReceiptId = row.ReceiptId,
					FactionCoherent = coherent,
					DeclaresVillage = coherent && village.GetIntProperty("Village") == 1,
					CurrentStanding = System.GetRegardForRealm(row.VillageFactionId)
				});
			}
			return KingdomVillageCovenantView.Owner(KingdomVillageCovenantEvidence.Recorded,
				realmId, archive, seen, null);
		}
	}
}
#endif
