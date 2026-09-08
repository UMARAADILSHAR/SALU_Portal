using SaluExamPortal.Application.UniversityAdmission.Models;

namespace SaluExamPortal.Application.UniversityAdmission.Services;

/// <summary>
/// Scoped per-user service that holds the entire multi-step admission form state.
/// In Blazor Server, one instance per SignalR circuit (one per browser tab).
/// </summary>
public class AdmissionFormService
{
    public AdmissionFormState State { get; } = new();

    public event Action? OnChange;

    public void SavePersonal()
    {
        State.PersonalDone = true;
        OnChange?.Invoke();
    }

    public void SaveAcademics()
    {
        State.AcademicsDone = true;
        OnChange?.Invoke();
    }

    public void SavePreferences()
    {
        State.PreferencesDone = true;
        OnChange?.Invoke();
    }

    public void SaveDocuments()
    {
        State.DocumentsDone = true;
        OnChange?.Invoke();
    }

    public void SaveUndertaking()
    {
        if (State.UndertakingAccepted)
        {
            State.UndertakingDone = true;
            OnChange?.Invoke();
        }
    }
}

