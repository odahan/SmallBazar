using Microsoft.Maui.Controls;
namespace SmallBazar.Utils
{
    public static class Animator
    {
        public static async Task AnimateClicView(View button)
        {
            if (button is null) return;
            await Task.WhenAll(
                button.RotateToAsync(45, 100),
                button.ScaleToAsync(1.2, 100));
            await Task.WhenAll(
                button.RotateToAsync(0, 100),
                button.ScaleToAsync(1.0, 200));

        }
    }
}
