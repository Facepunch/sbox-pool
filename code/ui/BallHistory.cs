using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;

namespace Facepunch.Pool
{
	public class BallHistoryItem : Panel
	{
		public string IconClass { get; private set; }
		public Panel Icon { get; private set; }

		public BallHistoryItem()
		{
			Icon = Add.Panel( "icon" );
		}

		public void Update( PoolBall ball )
		{
			if ( ball == null )
			{
				SetIconClass( "none" );
				return;
			}

			SetIconClass( ball.GetIconClass() );
		}

		public void UpdateFrom( PoolBallType type, PoolBallNumber number )
		{
			if ( type == PoolBallType.Black )
				SetIconClass( "black" );
			else
				SetIconClass( $"{ type.ToString().ToLower() }_{ (int)number }" );
		}

		public void SetIconClass( string iconClass )
		{
			if ( !string.IsNullOrEmpty( IconClass ) )
				Icon.RemoveClass( IconClass );

			Icon.AddClass( iconClass );
			IconClass = iconClass;
		}
	}

	public class BallHistory : Panel
	{
		public static BallHistory Current { get; set; }

		public List<BallHistoryItem> Items { get; private set; }
		public int Index { get; private set; }

		public BallHistory()
		{
			StyleSheet.Load( "/ui/BallHistory.scss" );

			Current = this;
			Index = 0;
			Items = new List<BallHistoryItem>();

			for ( var i = 0; i < 15; i++ )
			{
				var item = AddChild<BallHistoryItem>();
				item.Update( null );
				Items.Add( item );
			}

			var game = Game.Instance;

			if ( game != null )
				game.UpdatePotHistory();
		}

		public void AddBall( PoolBall ball )
		{
			if ( Index < Items.Count )
			{
				Items[Index].Update( ball );
				Index++;
			}
		}

		public void AddByType( PoolBallType type, PoolBallNumber number )
		{
			if ( Index < Items.Count )
			{
				Items[Index].UpdateFrom( type, number );
				Index++;
			}
		}

		public void Clear()
		{
			foreach ( var item in Items )
				item.Update( null );

			Index = 0;
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

			SetClass( "hidden", false );

			base.Tick();
		}
	}
}
