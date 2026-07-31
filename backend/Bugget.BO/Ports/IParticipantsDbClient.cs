namespace Bugget.BO.Ports;

public interface IParticipantsDbClient
{
    Task<string[]?> AddParticipantIfNotExistAsync(int reportId, string userId);
}
