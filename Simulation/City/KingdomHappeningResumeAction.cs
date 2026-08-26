using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal enum KingdomHappeningResumeAction : byte
	{
		Refuse = 0,
		PreparePosts = 1,
		WaitForArrival = 2,
		BeginHold = 3,
		WaitHold = 4,
		Publish = 5,
		WaitExternal = 6,
		Restore = 7
	}
}
