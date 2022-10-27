using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;

namespace Facepunch.Pool
{
	public class ToastItem : Panel
	{
		public Label Text { get; set; }
		public Image Avatar { get; set; }
		public Panel Circle { get; set; }
		public Panel Icon { get; set; }

		private float EndTime;

		public ToastItem()
		{
			Avatar = Add.Image( $"", "avatar" );
			Text = Add.Label( "", "text" );
			Circle = Add.Panel( "circle" );
			Icon = Circle.Add.Panel( "icon" );
		}

		public void Update( Player player, string text, string iconClass = "" )
		{
			Avatar.SetTexture( $"avatar:{ player.Client.PlayerId }" );
			Text.Text = text;

			if ( !string.IsNullOrEmpty( iconClass ) )
				Icon.AddClass( iconClass );
			else
				Circle.AddClass( "hidden" );

			EndTime = Time.Now + 3f;
		}

		public override void Tick()
		{
			if ( !IsDeleting && Time.Now >= EndTime )
				Delete();
		}
	}

	public class ToastList : Panel
	{
		public static ToastList Current { get; private set; }

		public ToastList()
		{
			StyleSheet.Load( "/ui/ToastList.scss" );
			Current = this;
		}

		public void AddItem( Player player, string text, string iconClass = "" )
		{
			var item = AddChild<ToastItem>();
			item.Update( player, text, iconClass );
		}
	}
}
