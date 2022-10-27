using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;

namespace Facepunch.Pool
{
	public class RankIcon : Panel
	{
		public Panel Rank;
		public Label Level;

		public RankIcon()
		{
			Rank = Add.Panel( "rank" );
			Level = Add.Label( "0", "level" );
		}

		public void Update( PlayerRank rank, int level )
		{
			Rank.AddClass( rank.ToString().ToLower() );
			Level.Text = level.ToString();
		}
	}

	public class OpponentDisplay : Panel
	{
		public Label Text;
		public Panel Container;
		public Image Avatar;
		public Label Name;
		public RankIcon RankIcon;

		public OpponentDisplay()
		{
			Text = Add.Label( "", "opponent-text" );
			Container = Add.Panel( "opponent-background" );
			Avatar = Container.Add.Image( "", "opponent-avatar" );
			Name = Container.Add.Label( "", "opponent-name" );
			RankIcon = Container.AddChild<RankIcon>();
		}

		public void Update( EloOutcome outcome, Player opponent )
		{
			if ( outcome == EloOutcome.Win )
				Text.Text = "You beat";
			else
				Text.Text = "You lost to";

			var opponentClient = opponent.Client;

			Avatar.SetTexture( $"avatar:{opponentClient.PlayerId}" );
			Name.Text = opponentClient.Name;

			RankIcon.Update( opponent.Elo.GetRank(), opponent.Elo.GetLevel() );
		}
	}

	public class RankProgress : Panel
	{
		public RankIcon LeftRank;
		public RankIcon RightRank;
		public Panel BarBackground;
		public Panel BarProgress;
		public Panel BarDelta;

		public RankProgress()
		{
			LeftRank = AddChild<RankIcon>( "leftrank" );
			RightRank = AddChild<RankIcon>( "rightrank" );
			BarBackground = Add.Panel( "rank-bg" );
			BarProgress = BarBackground.Add.Panel( "rank-progress" );
			BarDelta = BarBackground.Add.Panel( "rank-delta" );
		}

		public void Update( int rating, int delta )
		{
			var previousScore = Math.Max( rating - delta, 0 );
			var nextScore = Elo.GetNextLevelRating( previousScore );
			var progress = 100 - (nextScore - previousScore);

			LeftRank.Update( Elo.GetRank( previousScore  ), Elo.GetLevel( previousScore ) );
			RightRank.Update( Elo.GetRank( nextScore ), Elo.GetLevel( nextScore ) );

			if ( delta < 0 ) progress += delta;

			BarProgress.Style.Width = Length.Percent( progress );
			BarDelta.Style.Width = Length.Percent( Math.Min( Math.Abs( delta ), 100 - progress ) );
			BarDelta.SetClass( "loss", delta < 0 );

			Style.Dirty();
		}
	}

	public class WinSummary : Panel
	{
		public Panel Background;
		public Panel Container;
		public Panel Header;
		public RankProgress RankProgress;
		public OpponentDisplay OpponentDisplay;

		public WinSummary()
		{
			StyleSheet.Load( "/ui/WinSummary.scss" );

			Background = Add.Panel( "background" );
			Container = Add.Panel( "container" );

			Header = Container.Add.Panel( "win-header" );
			OpponentDisplay = Container.AddChild<OpponentDisplay>();

			if ( Game.Rules.IsRanked )
				RankProgress = Container.AddChild<RankProgress>();

			AcceptsFocus = true;
		}

		public void Update( EloOutcome outcome, Player opponent, int rating, int delta )
		{
			if ( Local.Pawn is Player )
			{
				if ( outcome == EloOutcome.Win )
					Header.AddClass( "win" );
				else
					Header.AddClass( "loss" );

				OpponentDisplay.Update( outcome, opponent );
				RankProgress?.Update( rating, delta );
			}
		}
	}
}
