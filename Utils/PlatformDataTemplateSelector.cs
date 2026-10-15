using Microsoft.Maui.Controls;

namespace SmallBazar.Utils
{
    public class PlatformDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DefaultTemplate { get; set; }
        public DataTemplate WindowsTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                if (WindowsTemplate == null)
                {
                    return new DataTemplate(() =>
                    {
                        var label = new Label { Text = "Error: WindowsTemplate is null" };
                        return new ContentView { Content = label };
                    });
                }
                return WindowsTemplate;
            }

            if (DefaultTemplate == null)
            {
                return new DataTemplate(() =>
                {
                    var label = new Label { Text = "Error: DefaultTemplate is null" };
                    return new ContentView { Content = label };
                });
            }

            return DefaultTemplate;
        }
    }
}