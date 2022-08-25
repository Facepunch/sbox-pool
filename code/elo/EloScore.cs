using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Facepunch.Pool
{
	public enum EloOutcome
	{
		Loss = 0,
		Win = 1
	}

	public partial class EloScore : BaseNetworkable
	{
		[Net] public int Rating { get; set; }
		[Net] public int Delta { get; set; }

		public PlayerRank GetRank()
		{
			return Elo.GetRank( Rating );
		}

		public PlayerRank GetNextRank()
		{
			return Elo.GetNextRank( Rating );
		}

		public int GetLevel()
		{
			return Elo.GetLevel( Rating );
		}

		public async Task Update( Client client )
		{
			try
			{
				Log.Info( "Awaiting Rank Update" );

				var score = await client.FetchGameRankAsync();
				var delta = (score.Level - Rating);

				Log.Info( "Update: " + score );
				Log.Info( "Delta: " + delta );

				Rating = score.Level;
				Delta = delta;
			}
			catch ( TaskCanceledException _ )
			{
				Log.Warning( $"Unable to fetch game rank data for {client}!" );
			}
		}
	}
}
