namespace Bugget.Domain.Users;

public static class Constants
{
    /// <summary>
    /// БД модуля users (users_db). Отдельная от БД модуля reports (app_db):
    /// после объединения сервисов в один процесс обе строки подключения живут рядом.
    /// </summary>
    public const string PostgresConnectionStringEnv = "USERS_POSTGRES_CONNECTION_STRING";
    public const int MaxFreeUsersCount = 100;
}
