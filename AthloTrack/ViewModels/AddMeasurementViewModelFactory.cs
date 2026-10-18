using System;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;

namespace AthloTrack.ViewModels;

public sealed class AddMeasurementViewModelFactory
{
    private readonly IMeasurementRepository _measurements;
    private readonly SessionState _session;
    private readonly IMeasurementFieldRepository _fields;

    public AddMeasurementViewModelFactory(IMeasurementRepository measurements, SessionState session,
        IMeasurementFieldRepository fields)
    {
        _measurements = measurements;
        _session = session;
        _fields = fields;
    }

    public AddMeasurementViewModel Create(Guid athleteId) => new(athleteId, _measurements, _session, _fields);

    public AddMeasurementViewModel CreateForEdit(AthloTrack.Core.Models.Measurement measurement)
    {
        var vm = new AddMeasurementViewModel(measurement.AthleteId, _measurements, _session, _fields);
        vm.BeginEdit(measurement);
        return vm;
    }
}
