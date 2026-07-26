namespace Bugget.Entities.BO.ReportBo;

// Historical: значение 2 раньше называлось `InProgress` и было переименовано в `Fix`
// с сохранением численного значения; значение 4 (`Test`) добавлено позже. Не менять
// числа — на них завязаны записи в БД.
public enum ReportStatus
{
    Backlog = 0,
    Resolved = 1,
    Fix = 2,
    Rejected = 3,
    Test = 4
}
