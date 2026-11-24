using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Offline;
using AthloTrack.Core.Supabase;
using Microsoft.Extensions.DependencyInjection;

namespace AthloTrack.Core.DependencyInjection;

public static class CoreServiceRegistration
{
    /// <summary>
    /// Registers the shared, platform-agnostic services. The caller supplies the Supabase
    /// config and must also register a platform-specific <see cref="ICredentialStore"/>.
    /// </summary>
    public static IServiceCollection AddAthloTrackCore(this IServiceCollection services, SupabaseConfig config)
    {
        services.AddSingleton(config);
        services.AddSingleton<SupabaseClientFactory>();
        services.AddSingleton<SessionState>();

        // Offline copies (Android registers a real store after this; the others keep nothing).
        services.AddSingleton<IOfflineStore, NullOfflineStore>();
        services.AddSingleton<OfflineState>();
        services.AddSingleton<OfflineCache>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IAccountService, AccountService>();
        services.AddSingleton<ISessionInitializer, SessionInitializer>();

        services.AddSingleton<ICoachRepository, CoachRepository>();
        // Προπονήσεις and Ημερολόγιο read through these, so they also work without internet.
        services.AddSingleton<AthleteRepository>();
        services.AddSingleton<IAthleteRepository>(sp => new CachedAthleteRepository(
            sp.GetRequiredService<AthleteRepository>(), sp.GetRequiredService<OfflineCache>(), sp.GetRequiredService<OfflineState>()));
        services.AddSingleton<MeasurementRepository>();
        services.AddSingleton<IMeasurementRepository>(sp => new CachedMeasurementRepository(
            sp.GetRequiredService<MeasurementRepository>(), sp.GetRequiredService<OfflineCache>(), sp.GetRequiredService<OfflineState>()));
        services.AddSingleton<MeasurementFieldRepository>();
        services.AddSingleton<IMeasurementFieldRepository>(sp => new CachedMeasurementFieldRepository(
            sp.GetRequiredService<MeasurementFieldRepository>(), sp.GetRequiredService<OfflineCache>(), sp.GetRequiredService<OfflineState>()));
        services.AddSingleton<WorkoutRepository>();
        services.AddSingleton<CachedWorkoutRepository>(sp => new CachedWorkoutRepository(
            sp.GetRequiredService<WorkoutRepository>(), sp.GetRequiredService<OfflineCache>(), sp.GetRequiredService<OfflineState>()));
        services.AddSingleton<IWorkoutRepository>(sp => sp.GetRequiredService<CachedWorkoutRepository>());
        services.AddSingleton<IWorkoutTemplateRepository, WorkoutTemplateRepository>();
        services.AddSingleton<IFormRepository, FormRepository>();
        services.AddSingleton<INotificationRepository, NotificationRepository>();
        services.AddSingleton<IAvatarService, AvatarService>();
        services.AddSingleton<IAdminRepository, AdminRepository>();
        services.AddSingleton<AthloTrack.Core.Push.PushRegistrationService>();

        return services;
    }
}
