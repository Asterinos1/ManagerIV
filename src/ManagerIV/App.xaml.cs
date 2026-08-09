using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using ManagerIV.Core;
using ManagerIV.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ManagerIV;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IServiceProvider Services { get; }
    public new static App Current => (App)Application.Current;

    public App()
    {
        Services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        
        string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ManagerIV");
        
        // Setup Logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug()
            .WriteTo.File(Path.Combine(baseDir, "Logs", "manageriv_.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        // Core Services
        services.AddSingleton<ArchiveHandler>();
        services.AddSingleton<MetadataService>();
        services.AddSingleton<ProfileManager>();
        services.AddSingleton<LoadOrderService>();
        services.AddSingleton<ConflictDetector>();
        services.AddSingleton<IFileSystemLinker, NativeFileSystemLinker>();
        services.AddSingleton<UpdateWatchdog>();
        services.AddSingleton<IModStructureAnalyzer, ModStructureAnalyzer>();
        services.AddSingleton(new BackendToolManager(Path.Combine(baseDir, "Cache")));
        
        // Backup Service depends on Linker and backup dir
        services.AddSingleton(sp => new BackupRollbackService(sp.GetRequiredService<IFileSystemLinker>(), Path.Combine(baseDir, "Backup")));

        services.AddSingleton<SaveProfileViewModel>();
        services.AddSingleton<LibraryViewModel>();

        // ViewModels
        services.AddSingleton(sp => 
            new MainViewModel(
                baseDir,
                sp.GetRequiredService<ArchiveHandler>(),
                sp.GetRequiredService<MetadataService>(),
                sp.GetRequiredService<ProfileManager>(),
                sp.GetRequiredService<LoadOrderService>(),
                sp.GetRequiredService<ConflictDetector>(),
                sp.GetRequiredService<IFileSystemLinker>(),
                sp.GetRequiredService<BackupRollbackService>(),
                sp.GetRequiredService<UpdateWatchdog>(),
                sp.GetRequiredService<BackendToolManager>(),
                sp.GetRequiredService<IModStructureAnalyzer>(),
                sp.GetRequiredService<SaveProfileViewModel>(),
                sp.GetRequiredService<LibraryViewModel>(),
                sp.GetRequiredService<ILogger<MainViewModel>>()
            ));

        return services.BuildServiceProvider();
    }
}
