using System;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;

namespace AthloTrack.ViewModels;

public sealed class AddMeasurementViewModelFactory
{
    private readonly IMeasurementRepository _measurements;
    private readonly SessionState _session;

    public AddMeasurementViewModelFactory(IMeasurementRepository measurements, SessionState session)
    {
        _measurements = measurements;
        _session = session;
    }

    public AddMeasurementViewModel Create(Guid athleteId) => new(athleteId, _measurements, _session);

    public AddMeasurementViewModel CreateForEdit(AthloTrack.Core.Models.Measurement measurement)
    {
        var vm = new AddMeasurementViewModel(measurement.AthleteId, _measurements, _session);
        vm.BeginEdit(measurement);
        return vm;
    }
}
