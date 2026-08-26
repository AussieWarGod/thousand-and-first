using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private static void WriteActivatedAbilityProof(System.IO.BinaryWriter Writer,
			ActivatedAbilityEntry Value)
		{
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			Writer.Write(Value.ID.ToByteArray()); WriteProofString(Writer, Value.DisplayName);
			WriteProofString(Writer, Value.Command); WriteProofString(Writer, Value.Class);
			WriteProofString(Writer, Value.Description); WriteProofString(Writer, Value.Icon);
			WriteProofString(Writer, Value.DisabledMessage); Writer.Write(Value.Flags);
			WriteProofString(Writer, Value._DescriptionCommand);
			CommandCooldown cooldown = Value.CommandCooldown;
			Writer.Write(cooldown == null ? (byte)0 : (byte)1);
			if (cooldown != null)
			{
				WriteProofString(Writer, cooldown.Command); Writer.Write(cooldown.Segments);
				Writer.Write(cooldown.Token);
			}
			WriteRenderableProof(Writer, Value.UITileDefault);
			WriteRenderableProof(Writer, Value.UITileToggleOn);
			WriteRenderableProof(Writer, Value.UITileDisabled);
			WriteRenderableProof(Writer, Value.UITileCoolingDown);
		}

		private static void WriteReferenceTopologyProof(System.IO.BinaryWriter Writer,
			object Value, List<object> References)
		{
			if (Value == null) { Writer.Write(-1); return; }
			for (int i = 0; i < References.Count; i++)
				if (ReferenceEquals(References[i], Value)) { Writer.Write(i); return; }
			Writer.Write(-2 - References.Count);
			References.Add(Value);
		}

		private static void WriteActivatedAbilityTemplateProof(System.IO.BinaryWriter Writer,
			ActivatedAbilityEntry Value)
		{
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			WriteProofString(Writer, Value.DisplayName); WriteProofString(Writer, Value.Command);
			WriteProofString(Writer, Value.Class); WriteProofString(Writer, Value.Description);
			WriteProofString(Writer, Value.Icon); WriteProofString(Writer, Value.DisabledMessage);
			Writer.Write(Value.Flags); WriteProofString(Writer, Value._DescriptionCommand);
			CommandCooldown cooldown = Value.CommandCooldown;
			Writer.Write(cooldown == null ? (byte)0 : (byte)1);
			if (cooldown != null)
			{
				WriteProofString(Writer, cooldown.Command); Writer.Write(cooldown.Segments);
				Writer.Write(cooldown.Token);
			}
			WriteRenderableProof(Writer, Value.UITileDefault);
			WriteRenderableProof(Writer, Value.UITileToggleOn);
			WriteRenderableProof(Writer, Value.UITileDisabled);
			WriteRenderableProof(Writer, Value.UITileCoolingDown);
		}

		private static void WriteRenderableProof(System.IO.BinaryWriter Writer,
			ConsoleLib.Console.Renderable Value)
		{
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			WriteProofString(Writer, Value.Tile); WriteProofString(Writer, Value.RenderString);
			WriteProofString(Writer, Value.ColorString); WriteProofString(Writer, Value.TileColor);
			Writer.Write(Value.DetailColor);
		}

		private static bool FinishProofHash(System.IO.MemoryStream Stream,
			System.IO.BinaryWriter Writer, out string Hash)
		{
			Hash = null;
			Writer.Flush();
			if (Stream.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes * 4L) return false;
			using (global::System.Security.Cryptography.SHA256 sha =
				global::System.Security.Cryptography.SHA256.Create())
			{
				byte[] digest = sha.ComputeHash(Stream.ToArray());
				System.Text.StringBuilder text = new System.Text.StringBuilder(64);
				for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
				Hash = text.ToString();
				return true;
			}
		}

		private static void WriteHashText(System.IO.BinaryWriter Writer, string Value,
			System.Text.Encoding Utf8)
		{
			if (Value == null) { Writer.Write(-1); return; }
			int count = Utf8.GetByteCount(Value); Writer.Write(count);
			Writer.Write(Utf8.GetBytes(Value));
		}

		private bool QuarantineReturn(KingdomRealmArchive Archive, string Failure,
			out string Refusal)
		{
			Archive.Quarantine(Failure);
			Refusal = "The returned realm changed during an engine callback and requires inspection.";
			return false;
		}

		private bool CurrentRealmIsCanonicalBlank(KingdomRealmArchive Archive)
		{
			if (Archive == null || Founded || KingdomFactionName != null || RealmId != null ||
				KingdomDisplayName != null ||
				Away != null || Standings == null || Standings.Count != 0 ||
				RealmIdentityVersion != 0 || RealmIdentityOrigin != KingdomIdentityOrigin.None ||
				RealmIdentityTransactionId != null || RealmIdentityLegacyFaction != null ||
				RealmIdentityFoundedTick != 0L || RealmIdentitySeedHigh != 0UL ||
				RealmIdentitySeedLow != 0UL || RealmIdentityFirstClaimedZone != null ||
				IdentityFault != null || PendingSettlementId != null ||
				PendingSettlementTransactionId != null || PendingSettlementZoneId != null ||
				PendingSettlementAuthority != null || SimulationSeedHigh != 0UL ||
				SimulationSeedLow != 0UL || Bindings == null || ResidentCounter != 0 || Jobs == null ||
				LastSliceTick != 0L || ReifyTick != 0L || ReifyThirdsSpent != 0 ||
				ReifyHeavySpent != 0 || ReifyQuietUntilTick != 0L || DedicationCounter != 0 ||
				ChronicleEntries == null || ChronicleEntries.Count != 0 || OutsiderEntries == null ||
				OutsiderEntries.Count != 0 || RegardSpoken != (int)RealmRegard.Beloved ||
				Dissent != 0 || DissentSpoken != 0 || LastDissentTick != 0L ||
				DeclaredCreed != null || DishName != null || DishText != null ||
				DishStaple != null || DishSource != null || LastRiteTick != 0L ||
				LastSoulRiteTick != 0L || Seceded != null || SecededTick != 0L || Haul != null ||
				CarryBook == null || ReturnAskedRegard != int.MinValue || DoorClosedTold)
				return false;
			try
			{
				return KingdomArchivedSettlementCodec.ExactGraph(Capture(),
					new KingdomSettlement(), out string _) &&
					KingdomArchivedSettlementCodec.EmptyRegistries(Bindings, Jobs) &&
					KingdomArchivedSettlementCodec.EmptyCarry(CarryBook);
			}
			catch
			{
				return false;
			}
		}

		private bool RestoreArchivedRealmCore(KingdomRealmArchive Archive,
			out string Failure)
		{
			Failure = null;
			if (Archive == null ||
				!KingdomArchivedSettlementCodec.TryClone(Archive.Seat,
					out KingdomSettlement seat, out Failure) ||
				!KingdomArchivedSettlementCodec.TryClone(Archive.Away,
					out KingdomSettlement away, out Failure) ||
				!KingdomArchivedSettlementCodec.TryClone(Archive.Seceded,
					out KingdomSettlement seceded, out Failure) ||
				!KingdomRealmArchive.TryCloneCarry(Archive.CarryBook,
					out KingdomCarryBook carry, out Failure)) return false;
			Simulation.City.KingdomBindingRegistry bindings =
				KingdomRealmArchive.CloneBindings(Archive.Bindings);
			Simulation.City.KingdomJobRegistry jobs = KingdomRealmArchive.CloneJobs(Archive.Jobs);
			List<string> chronicle = KingdomRealmArchive.CloneStrings(Archive.ChronicleEntries);
			List<string> outsider = KingdomRealmArchive.CloneStrings(Archive.OutsiderEntries);
			Dictionary<string, int> standings = KingdomRealmArchive.CloneStandings(Archive.Standings);
			if (seat == null || bindings == null || jobs == null || chronicle == null ||
				outsider == null || standings == null)
			{
				Failure = "archived realm graph has a null required root";
				return false;
			}
			KingdomFactionName = Archive.FactionName;
			KingdomDisplayName = Archive.DisplayName;
			Restore(seat);
			Away = away;
			Standings = standings;
			RealmId = Archive.RealmId;
			RealmIdentityVersion = Archive.RealmIdentityVersion;
			RealmIdentityOrigin = Archive.RealmIdentityOrigin;
			RealmIdentityTransactionId = Archive.RealmIdentityTransactionId;
			RealmIdentityLegacyFaction = Archive.RealmIdentityLegacyFaction;
			RealmIdentityFoundedTick = Archive.RealmIdentityFoundedTick;
			RealmIdentitySeedHigh = Archive.RealmIdentitySeedHigh;
			RealmIdentitySeedLow = Archive.RealmIdentitySeedLow;
			RealmIdentityFirstClaimedZone = Archive.RealmIdentityFirstClaimedZone;
			IdentityFault = null;
			SimulationSeedHigh = Archive.SimulationSeedHigh;
			SimulationSeedLow = Archive.SimulationSeedLow;
			Bindings = bindings;
			ResidentCounter = Archive.ResidentCounter;
			Jobs = jobs;
			LastSliceTick = Archive.LastSliceTick;
			ReifyTick = Archive.ReifyTick;
			ReifyThirdsSpent = Archive.ReifyThirdsSpent;
			ReifyHeavySpent = Archive.ReifyHeavySpent;
			ReifyQuietUntilTick = Archive.ReifyQuietUntilTick;
			DedicationCounter = Archive.DedicationCounter;
			ChronicleEntries = chronicle;
			OutsiderEntries = outsider;
			RegardSpoken = Archive.RegardSpoken;
			Dissent = Archive.Dissent;
			DissentSpoken = Archive.DissentSpoken;
			LastDissentTick = Archive.LastDissentTick;
			DeclaredCreed = Archive.DeclaredCreed;
			DishName = Archive.DishName;
			DishText = Archive.DishText;
			DishStaple = Archive.DishStaple;
			DishSource = Archive.DishSource;
			LastRiteTick = Archive.LastRiteTick;
			LastSoulRiteTick = Archive.LastSoulRiteTick;
			Seceded = seceded;
			SecededTick = Archive.SecededTick;
			Haul = KingdomRealmArchive.CloneHaul(Archive.Haul);
			CarryBook = carry;
			PendingSettlementId = null;
			PendingSettlementTransactionId = null;
			PendingSettlementZoneId = null;
			PendingSettlementAuthority = null;
			ReturnAskedRegard = int.MinValue;
			DoorClosedTold = false;
			return true;
		}

		private bool CurrentRealmMatchesArchive(KingdomRealmArchive Archive)
		{
			List<string> ids;
			string failure;
			if (Archive == null || Archive.Quarantined ||
				!string.Equals(RealmId, Archive.RealmId, StringComparison.Ordinal) ||
				!TryExactSettlementIds(RequirePublishedClaims: true, out ids, out failure) ||
				Archive.SettlementIds == null || ids.Count != Archive.SettlementIds.Count)
				return false;
			for (int i = 0; i < ids.Count; i++)
				if (!string.Equals(ids[i], Archive.SettlementIds[i],
					StringComparison.Ordinal)) return false;
			return string.Equals(RealmId, Archive.RealmId, StringComparison.Ordinal) &&
				ExactArchivedSettlements(Archive.RealmId, ExiledSeat, ExiledAway,
					Archive.SettlementIds) && Archive.CurrentGraphMatches(this, out failure);
		}

	}
}
