using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomPorters
	{
		/// <summary>Mints or adopts only an exact empty SourceDebitPrepared projection on the
		/// already-active frozen source. Generic Render never owns construction-input bodies.</summary>
		internal static bool RenderConstructionInputSource(KingdomSystem system, Zone zone,
			string owner, int jobId, int tripId, int schema, string digest, long revision, long now)
		{
			if (system?.Jobs == null || zone == null || The.ZoneManager == null
				|| !ReferenceEquals(The.ZoneManager.ActiveZone, zone)
				|| KingdomSurvey.ActiveFor(zone) == null
				|| !KingdomCentralLogistics.TryProveConstructionInputSourceRow(system, owner,
					jobId, tripId, schema, digest, revision, zone.ZoneID, out KingdomCityFault _)
				|| !system.Jobs.TryRead(out KingdomJobTable jobs, out _)
				|| !jobs.TryGet(jobId, out KingdomJobRow row)) return false;
			KingdomBindingVerdict verdict = KingdomResidents.Judge(system, tripId,
				KingdomBindingKind.Transient, zone.ZoneID);
			bool minted = false;
			if (verdict == KingdomBindingVerdict.Mint)
			{
				minted = true;
				GameObject created = Mint(system, zone, tripId, (short)row.DeliverySourceX,
					(short)row.DeliverySourceY, row.OriginCode, now);
				if (!GameObject.Validate(created)) return false;
				r_KingdomPorter part = created.RequirePart<r_KingdomPorter>();
				part.JobId = tripId;
				part.DestX = row.DeliverySourceX; part.DestY = row.DeliverySourceY;
				part.ExitX = row.DeliverySourceX; part.ExitY = row.DeliverySourceY;
			}
			else if (verdict != KingdomBindingVerdict.Move) return false;
			if (!KingdomCentralLogistics.TryResolveConstructionInputSourceCarrier(system,
				owner, jobId, tripId, schema, digest, revision,
				out GameObject exact, out KingdomCityFault _)) return false;
			return exact.Blueprint == KingdomGrowth.DefaultSettlerBlueprint
				&& (!minted || KingdomOrdinaryCustody.TryProveEmpty(exact, out string _));
		}
	}
}
