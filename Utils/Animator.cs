using Microsoft.Maui.Controls;
namespace SmallBazar.Utils
{
    public static class Animator
    {
        public static async Task AnimateClicView(View button)
        {
            if (button is null) return;
#pragma warning disable CS4014
#pragma warning disable JMA001
            button.RotateTo(45, 100);
            await button.ScaleTo(1.2, 100);
            button.RotateTo(0, 100);
            await button.ScaleTo(1.0, 200);
#pragma warning restore JMA001
#pragma warning restore CS4014

        }
    }
}
