using Microsoft.Extensions.DependencyInjection;
using OpenCAD.TextRendering;
using System.Configuration;
using System.Data;
using System.Windows;

namespace UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider Services { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            // Register font provider as internal implementation detail (singleton)
            services.AddSingleton<IFontProvider, FontProvider>();

            // Register text metrics provider as the PUBLIC service (depends on IFontProvider internally)
            services.AddSingleton<ITextMetricsProvider, TextMetricsProvider>();

            Services = services.BuildServiceProvider();

            // Consumers only use ITextMetricsProvider - IFontProvider is encapsulated
        }
    }
}
