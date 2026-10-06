using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
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
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ISessionInitializer, SessionInitializer>();

        services.AddSingleton<ICoachRepository, CoachRepository>();
        services.AddSingleton<IAthleteRepository, AthleteRepository>();
        services.AddSingleton<IMeasurementRepository, MeasurementRepository>();
        services.AddSingleton<IWorkoutRepository, WorkoutRepository>();
        services.AddSingleton<IWorkoutTemplateRepository, WorkoutTemplateRepository>();
        services.AddSingleton<INotificationRepository, NotificationRepository>();
        services.AddSingleton<IAvatarService, AvatarService>();
        services.AddSingleton<IAdminRepository, AdminRepository>();
        services.AddSingleton<AthloTrack.Core.Push.PushRegistrationService>();

        return services;
    }
}
