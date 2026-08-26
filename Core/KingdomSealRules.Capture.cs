using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomSealRules
	{
		/// <summary>
		/// Seals a settlement: the whole of what crosses, and nothing else.
		/// <para>
		/// Preconditions: <paramref name="Seat"/> is a settlement whose <c>City</c> book has been
		/// normalized (which <c>KingdomSettlement.Normalize</c> guarantees on every read and every
		/// seat swap). Side effects: none &mdash; the settlement is read and never written, so a
		/// seal taken mid-play cannot perturb the run it is describing.
		/// </para>
		/// <para>
		/// The record comes back <see cref="KingdomSealStatus.Living"/> and unresolved. A stage is
		/// not a fate; the draw happens once, at promotion, and never here.
		/// </para>
		/// </summary>
		/// <param name="Seat">The settlement being sealed.</param>
		/// <param name="Lineage">Who this is, and where it came from.</param>
		/// <param name="RealmName">The realm's display name.</param>
		/// <param name="FounderName">The founder as the world would name them.</param>
		/// <param name="Chronicle">The official register.</param>
		/// <param name="Outsider">The rumour register.</param>
		/// <param name="WrittenTick">The world tick this record was taken at. Diagnostics only.</param>
		/// <returns>A complete record; never null.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="Seat"/> or
		/// <paramref name="Lineage"/> is null.</exception>
		public static KingdomSealRecord Capture(KingdomSettlement Seat, KingdomSealIdentity Identity,
			KingdomSealLineage Lineage, string RealmName, string FounderName,
			IList<string> Chronicle, IList<string> Outsider, long WrittenTick)
		{
			if (Seat == null)
			{
				throw new ArgumentNullException("Seat");
			}
			if (Lineage == null)
			{
				throw new ArgumentNullException("Lineage");
			}
			if (!ExactIdentity(Identity, Seat))
				throw new ArgumentException("Seal capture requires exact immutable realm topology and provenance.",
					"Identity");
			Simulation.City.KingdomCityBook book = Seat.City ?? new Simulation.City.KingdomCityBook();
			KingdomSealRecord record = new KingdomSealRecord();
			record.Status = KingdomSealStatus.Living;
			record.LineageId = SanitizeToken(Lineage.LineageId, KingdomSealRecord.MaxIdChars);
			record.LegacyId = SanitizeToken(Lineage.LegacyId, KingdomSealRecord.MaxIdChars);
			record.OriginGameId = SanitizeToken(Lineage.OriginGameId, KingdomSealRecord.MaxIdChars);
			record.Generation = (Lineage.Generation > 0) ? Lineage.Generation : 0;
			record.Revision = (Lineage.Revision > 0) ? Lineage.Revision : 0;
			record.WrittenTick = (WrittenTick > 0L) ? WrittenTick : 0L;
			record.FounderName = SanitizeText(FounderName, KingdomSealRecord.MaxNameChars);
			record.RealmName = SanitizeText(RealmName, KingdomSealRecord.MaxNameChars);
			record.SettlementName = SanitizeText(Seat.SettlementName, KingdomSealRecord.MaxNameChars);
			// Identity-labelled seal payloads never promote a mutable display name. A corrupt or
			// pre-v8 city remains visibly unbound until an explicit migration supplies exact proof.
			record.RealmId = Identity.RealmId;
			record.RealmSettlementIds = new List<string>(Identity.SettlementIds);
			record.RealmSettlementIds.Sort(StringComparer.Ordinal);
			record.RealmSettlementProvenance =
				new List<string>(Identity.SettlementProvenanceRows);
			record.RealmIdentityVersion = Identity.RealmIdentityVersion;
			record.RealmIdentityOrigin = Identity.RealmIdentityOrigin;
			record.RealmIdentityTransactionId = Identity.RealmIdentityTransactionId ?? "";
			record.RealmIdentityLegacyFaction = Identity.RealmIdentityLegacyFaction ?? "";
			record.RealmIdentityFoundedTick = Identity.RealmIdentityFoundedTick;
			record.RealmIdentitySeedHigh = Identity.RealmIdentitySeedHigh;
			record.RealmIdentitySeedLow = Identity.RealmIdentitySeedLow;
			record.RealmIdentityFirstClaimedZone = Identity.RealmIdentityFirstClaimedZone ?? "";
			record.SettlementId = Identity.SettlementId;
			record.SettlementIdentityVersion = Identity.SettlementIdentityVersion;
			record.SettlementIdentityOrigin = Identity.SettlementIdentityOrigin;
			record.SettlementIdentityTransactionId = Identity.SettlementIdentityTransactionId ?? "";
			record.SettlementIdentityFoundedTick = Identity.SettlementIdentityFoundedTick;
			record.SettlementIdentityFirstClaimedZone =
				Identity.SettlementIdentityFirstClaimedZone ?? "";
			record.SettlementIdentityLegacyId = Identity.SettlementIdentityLegacyId ?? "";
			record.Vocation = SanitizeText(Seat.Vocation, KingdomSealRecord.MaxNameChars);
			record.Style = SanitizeText(Seat.Style, KingdomSealRecord.MaxNameChars);
			record.FoundedTick = (Seat.FoundedTick > 0L) ? Seat.FoundedTick : 0L;
			record.RegionName = SanitizeText(Seat.FoundingRegionName, KingdomSealRecord.MaxNameChars);
			record.TerrainBlueprint = SanitizeToken(Seat.FoundingTerrainBlueprint, KingdomSealRecord.MaxIdChars);
			record.Depth = Clamp(Seat.FoundingZLevel, -128, 128);

			string ground = ChooseGround(book, Seat.ClaimedZones);
			record.GroundZoneId = SanitizeToken(ground, KingdomSealRecord.MaxIdChars);

			record.Stage = Clamp((int)Seat.Stage, 0, 8);
			record.Population = Clamp(Seat.Population, 0, KingdomSealRecord.MaxRoll);
			record.Defence = Clamp(DefenceOf(book, ground), 0, 4096);
			record.StoredWater = Clamp((int)ClampLong(book.WaterLevel, 0L, 1000000L), 0, 1000000);
			record.Withered = Seat.Withered;
			record.Vigour = KingdomRules.SealedVigour((GrowthStage)record.Stage, record.Population, record.Defence, record.StoredWater, record.Withered);

			CaptureWorks(book, ground, record);
			CaptureRoll(Seat, record);
			CaptureTallies(Seat.OriginCounts, KingdomSealRecord.MaxTallies, record.OriginKeys, record.OriginCounts);
			CaptureTallies(Seat.CreedCounts, KingdomSealRecord.MaxTallies, record.CreedKeys, record.CreedCounts);
			record.Chronicle = PinChronicle(Chronicle, KingdomSealRecord.MaxChronicle);
			record.Outsider = PinChronicle(Outsider, KingdomSealRecord.MaxChronicle);
			CaptureDead(Seat, record);
			return record;
		}

		/// <summary>
		/// The same record with a death written into it. Copy-on-write: the staged record is not
		/// touched, because a terminal attempt that a checkpoint later undoes must leave the stage
		/// exactly as it was.
		/// </summary>
		/// <param name="Record">The staged record.</param>
		/// <param name="CauseText">One clause naming how the founder died.</param>
		/// <param name="CauseKind">A short token for the kind of death.</param>
		/// <param name="CauseTurn">The turn it happened on.</param>
		/// <returns>A new record at <see cref="KingdomSealStatus.Terminal"/>.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="Record"/> is null.</exception>
		public static KingdomSealRecord WithTerminalCause(KingdomSealRecord Record, string CauseText, string CauseKind, long CauseTurn)
		{
			KingdomSealRecord copy = Copy(Record);
			if (Record.Status != KingdomSealStatus.Living)
			{
				throw new InvalidOperationException("Only a living stage can become a terminal attempt.");
			}
			if (Record.Revision == int.MaxValue)
			{
				throw new InvalidOperationException("The seal revision cannot advance further.");
			}
			copy.Status = KingdomSealStatus.Terminal;
			copy.CauseText = SanitizeText(CauseText, KingdomSealRecord.MaxLineChars);
			copy.CauseKind = SanitizeToken(CauseKind, KingdomSealRecord.MaxIdChars);
			if (copy.CauseText.Length == 0)
			{
				copy.CauseText = "death";
			}
			if (copy.CauseKind.Length == 0)
			{
				copy.CauseKind = "unknown";
			}
			copy.CauseTurn = (CauseTurn > 0L) ? CauseTurn : 0L;
			copy.Revision = Record.Revision + 1;
			return copy;
		}

		/// <summary>
		/// The same record sealed by the founder's own hand. Retirement is a separate act from
		/// death and keeps the save alive; what it settles is that <i>this generation</i> of the
		/// lineage can no longer be rewritten by playing on.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="Record"/> is null.</exception>
		public static KingdomSealRecord WithRetirement(KingdomSealRecord Record)
		{
			KingdomSealRecord copy = Copy(Record);
			if (Record.Status != KingdomSealStatus.Living)
			{
				throw new InvalidOperationException("Only a living stage can be retired explicitly.");
			}
			if (Record.Revision == int.MaxValue)
			{
				throw new InvalidOperationException("The seal revision cannot advance further.");
			}
			copy.Status = KingdomSealStatus.Retired;
			copy.Revision = Record.Revision + 1;
			return copy;
		}

		/// <summary>
		/// The seed the interregnum is drawn from: lineage, origin, generation, revision, and
		/// nothing else in the world.
		/// <para>
		/// Never the target world's seed, the calendar, system time, the founder's last visit, or
		/// any stream a player can turn over again. An earlier draft of the design mixed in the
		/// destination's seed, which would have handed back exactly the reroll the whole rule
		/// exists to prevent: regenerate the world, draw again. Because the seed is the legacy's
		/// own, a legacy's fate is fixed the moment it is promoted and arrives in every world the
		/// same way.
		/// </para>
		/// </summary>
		/// <param name="Lineage">The immutable legacy identity.</param>
		/// <returns>A stable seed for <c>KingdomRules.InterregnumRoll</c>.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="Lineage"/> is null.</exception>
		public static long InterregnumSeed(KingdomSealLineage Lineage)
		{
			if (Lineage == null)
			{
				throw new ArgumentNullException("Lineage");
			}
			// FNV-1a over the four immutable fields, with a separator no token alphabet contains
			// so that ("ab","c") and ("a","bc") cannot fold to the same seed.
			ulong hash = 14695981039346656037UL;
			hash = Fold(hash, Lineage.LineageId);
			hash = FoldByte(hash, 0x1F);
			hash = Fold(hash, Lineage.OriginGameId);
			hash = FoldByte(hash, 0x1F);
			hash = FoldInt(hash, Lineage.Generation);
			hash = FoldByte(hash, 0x1F);
			hash = FoldInt(hash, Lineage.Revision);
			return unchecked((long)hash);
		}

		/// <summary>
		/// Draws the one fortune and fixes the inherited state. The record comes back
		/// <see cref="KingdomSealStatus.Promoted"/> and immutable in meaning: nothing later
		/// redraws it, and retrying world generation reproduces it exactly.
		/// </summary>
		/// <param name="Record">A terminal attempt.</param>
		/// <param name="Verdict">The engine's score/save verdict for its origin.</param>
		/// <returns>A new promoted record.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="Record"/> is null.</exception>
		/// <exception cref="InvalidOperationException"><paramref name="Record"/> was already
		/// promoted. A second promotion would be a second fate for one life.</exception>
		public static KingdomSealRecord Promote(KingdomSealRecord Record, KingdomSealEligibility Verdict)
		{
			if (Record == null)
			{
				throw new ArgumentNullException("Record");
			}
			if (!MayPromote(Record.Status, Verdict))
			{
				throw new InvalidOperationException("Automatic promotion requires a terminal attempt from an ended run.");
			}
			return ResolvePromotion(Record);
		}

		/// <summary>Resolves an explicit retirement. Separate from automatic terminal promotion so
		/// engine eligibility can never turn a living or retired stage into an automatic import.</summary>
		public static KingdomSealRecord PromoteRetirement(KingdomSealRecord Record)
		{
			if (Record == null)
			{
				throw new ArgumentNullException("Record");
			}
			if (Record.Status != KingdomSealStatus.Retired)
			{
				throw new InvalidOperationException("Explicit retirement promotion requires a retired record.");
			}
			return ResolvePromotion(Record);
		}

		private static KingdomSealRecord ResolvePromotion(KingdomSealRecord Record)
		{
			KingdomSealRecord copy = Copy(Record);
			long seed = InterregnumSeed(new KingdomSealLineage(Record.LineageId, Record.LegacyId,
				Record.OriginGameId, Record.Generation, Record.Revision));
			int roll = KingdomRules.InterregnumRoll(seed);
			copy.Status = KingdomSealStatus.Promoted;
			copy.InterregnumRoll = roll;
			copy.InheritedState = (int)KingdomRules.ResolveInheritedState(Record.Vigour, roll, Record.Population);
			return copy;
		}
	}
}
