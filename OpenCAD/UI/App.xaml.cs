using Microsoft.Extensions.DependencyInjection;
using OpenCAD.Interfaces;
using OpenCAD.TextRendering;
using System.Configuration;
using System.Data;
using System.Windows;
using UI.Commands;

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
            var splashScreen = new SplashScreen("Resources/OpenCAD.jpg");
            splashScreen.Show(autoClose: true, topMost: true);

            base.OnStartup(e);

            var services = new ServiceCollection();

            // Register font provider as internal implementation detail (singleton)
            services.AddSingleton<IFontProvider, FontProvider>();

            // Register text metrics provider as the PUBLIC service (depends on IFontProvider internally)
            services.AddSingleton<ITextMetricsProvider, TextMetricsProvider>();

            services.AddSingleton<IDispatcher>(sp =>
            {
                return new WpfDispatcher();
            });


            Services = services.BuildServiceProvider();

            // Consumers only use ITextMetricsProvider - IFontProvider is encapsulated
        }
    }
}
