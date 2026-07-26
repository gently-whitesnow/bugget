namespace Bugget.DA.Interfaces;

public interface IParticipantsDbClient
{
    Task<string[]?> AddParticipantIfNotExistAsync(int reportId, string userId);
}
