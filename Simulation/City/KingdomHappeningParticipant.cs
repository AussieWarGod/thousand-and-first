using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal readonly struct KingdomHappeningParticipant
	{
		internal readonly int ResidentId;
		internal readonly string ObjectId;
		internal readonly string Name;
		internal readonly string Home;
		internal readonly string Anchor;
		internal readonly int OriginalX;
		internal readonly int OriginalY;
		internal readonly int TargetX;
		internal readonly int TargetY;
		internal readonly int PostWorkId;
		internal readonly int PostKind;
		internal readonly bool Wanders;
		internal readonly bool WandersRandomly;
		internal readonly bool Staying;
		internal readonly bool Restored;

		internal KingdomHappeningParticipant(int residentId, string objectId, string name,
			string home, string anchor, int originalX, int originalY, int targetX, int targetY,
			int postWorkId, int postKind, bool wanders, bool wandersRandomly, bool staying,
			bool restored = false)
		{
			ResidentId = residentId;
			ObjectId = objectId ?? "";
			Name = name ?? "";
			Home = home ?? "";
			Anchor = anchor ?? "";
			OriginalX = originalX;
			OriginalY = originalY;
			TargetX = targetX;
			TargetY = targetY;
			PostWorkId = postWorkId;
			PostKind = postKind;
			Wanders = wanders;
			WandersRandomly = wandersRandomly;
			Staying = staying;
			Restored = restored;
		}

		internal KingdomHappeningParticipant WithRestored()
		{
			return new KingdomHappeningParticipant(ResidentId, ObjectId, Name, Home, Anchor,
				OriginalX, OriginalY, TargetX, TargetY, PostWorkId, PostKind, Wanders,
				WandersRandomly, Staying, true);
		}
	}
}
