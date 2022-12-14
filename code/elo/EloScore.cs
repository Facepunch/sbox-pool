using Sandbox;
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

		public async Task Update( IClient client )
		{
			try
			{
				/*
				var score = await client.FetchGameRankAsync();
				var delta = (score.Level - Rating);

				Rating = score.Level;
				Delta = delta;
				*/

				Rating = 0;
				Delta = 0;
			}
			catch ( TaskCanceledException _ )
			{
				Log.Warning( $"Unable to fetch game rank data for {client}!" );
			}
		}
	}
}
