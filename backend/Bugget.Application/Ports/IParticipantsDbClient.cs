namespace Bugget.Application.Ports;

public interface IParticipantsDbClient
{
    Task<string[]?> AddParticipantIfNotExistAsync(int reportId, string userId);
}
