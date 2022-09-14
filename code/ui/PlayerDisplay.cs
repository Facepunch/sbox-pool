
using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;

namespace Facepunch.Pool
{
	public class PlayerDisplayItem : Panel
	{
		public Panel PlayerContainer;
		public Label Name;
		public Image Avatar;
		public Panel Rank;
		public Label Level;

		public Panel ScoreContainer;
		public Panel BallType;

		private PlayerRank LastRank;

		public PlayerDisplayItem()
		{
			LastRank = PlayerRank.Bronze;

			PlayerContainer = Add.Panel( "player-container" );
			Avatar = PlayerContainer.Add.Image( "", "avatar" );
			Name = PlayerContainer.Add.Label( "Name", "name" );

			ScoreContainer = Add.Panel( "score-container" );
			BallType = ScoreContainer.Add.Panel( "ball" );

			Rank = PlayerContainer.Add.Panel( "division" );
			Rank.AddClass( LastRank.ToString().ToLower() );
			Rank.BindClass( "hidden", () => !Game.Rules?.IsRanked ?? true );

			Level = Rank.Add.Label( "0", "rank" );
		}

		public void Update( Player player )
		{
			var isValid = (player != null && player.IsValid());

			var game = Game.Instance;
			if ( game == null ) return;

			var round = game.Round;
			if ( round == null ) return;

			if ( isValid )
			{
				var owner = player.Client;

				Name.Text = owner.Name;

				Avatar.SetTexture( $"avatar:{ owner.PlayerId }" );

				var rank = player.Elo.GetRank();
				var level = player.Elo.GetLevel();

				if ( LastRank != rank )
				{
					Rank.RemoveClass( LastRank.ToString().ToLower() );
					Rank.AddClass( rank.ToString().ToLower() );
					LastRank = rank;
				}

				Level.Text = level.ToString();

				BallType.SetClass( "spots", player.BallType == PoolBallType.Spots );
				BallType.SetClass( "black", player.BallsLeft == 0 );
				BallType.SetClass( "stripes", player.BallType == PoolBallType.Stripes );

				SetClass( "active", player.IsTurn );	
			}

			SetClass( "hidden", !isValid );
		}
	}

	public class PlayerDisplay : Panel
	{
		public PlayerDisplayItem PlayerOne;
		public PlayerDisplayItem PlayerTwo;

		public Panel TimeRemainingNumber;
		public Label TimeRemainingLabel;

		public Panel TimeBarWrapper;
		public Panel TimeRemainingBar;
		public Panel TimeRemainingProgress;

		public PlayerDisplay()
		{
			StyleSheet.Load( "/ui/PlayerDisplay.scss" );

			PlayerOne = AddChild<PlayerDisplayItem>( "one" );

			TimeRemainingNumber = Add.Panel( "time-remaining-number" );
			TimeRemainingLabel = TimeRemainingNumber.Add.Label( "30", "time-remaining-label" );

			TimeBarWrapper = Add.Panel( "time-bar-wrapper" );
			TimeRemainingBar = TimeBarWrapper.Add.Panel( "time-remaining" );
			TimeRemainingProgress = TimeRemainingBar.Add.Panel( "time-remaining-progress" );

			PlayerTwo = AddChild<PlayerDisplayItem>( "two" );
		}

		public override void Tick()
		{
			var player = Local.Pawn as Player;
			if ( player == null ) return;

			var game = Game.Instance;
			if ( game == null ) return;

			var round = game.Round;


			if ( round is not PlayRound )
			{
				SetClass( "hidden", true );
				return;
			}

			PlayerOne.Update( Game.Instance.PlayerOne );
			PlayerTwo.Update( Game.Instance.PlayerTwo );


			TimeRemainingLabel.Text = round.TimeLeftSeconds.ToString();

			TimeRemainingProgress.Style.Width = Length.Percent( (100f / 30f) * round.TimeLeftSeconds );
			TimeRemainingProgress.Style.Dirty();

			if ( round.TimeLeftSeconds <= 5 )
			{
				TimeBarWrapper.SetClass( "low", true );
				TimeRemainingNumber.SetClass( "low", true );
			}
			else
			{
				TimeBarWrapper.SetClass( "low", false );
				TimeRemainingNumber.SetClass( "low", false );
			}

			SetClass( "hidden", false );
		}
	}
}
