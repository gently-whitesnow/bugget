import { NotificationDemoForm } from "@/shared/ui";
import { notificationMessages, useNotifications } from "@/shared/model";

export const DevNotificationsPage = () => {
  const {
    notify,
    notifyError,
    notifySuccess,
    clearAll,
    setDegradedMode,
    clearDegradedMode,
  } = useNotifications();

  const triggerOverflow = () => {
    for (let index = 1; index <= 7; index += 1) {
      notify({
        type: "info",
        title: `Info toast #${index}`,
        message: "Проверка лимита: должно остаться максимум 5 уведомлений",
      });
    }
  };

  return (
    <div className="mx-auto w-full max-w-3xl p-6 space-y-6">
      <h1 className="text-2xl font-semibold">Notifications Dev Page</h1>

      <div className="card bg-base-100 border border-base-300">
        <div className="card-body gap-3">
          <h2 className="card-title text-lg">Toast Scenarios</h2>
          <div className="flex flex-wrap gap-2">
            <button
              className="btn btn-success btn-sm"
              type="button"
              onClick={() =>
                notifySuccess("Успех", "Операция выполнена успешно")
              }
            >
              Success
            </button>
            <button
              className="btn btn-info btn-sm"
              type="button"
              onClick={() =>
                notify({
                  type: "info",
                  title: "Инфо",
                  message: "Информационное уведомление",
                })
              }
            >
              Info
            </button>
            <button
              className="btn btn-warning btn-sm"
              type="button"
              onClick={() =>
                notify({
                  type: "warning",
                  title: "Предупреждение",
                  message: "Проверьте введенные данные",
                })
              }
            >
              Warning
            </button>
            <button
              className="btn btn-error btn-sm"
              type="button"
              onClick={() =>
                notifyError("Ошибка запроса", notificationMessages.errorRetry, {
                  dedupeKey: "dev-error-request",
                  retry: () =>
                    notifySuccess("Retry", "Повторная попытка выполнена"),
                })
              }
            >
              Error + Retry + Dedupe
            </button>
            <button
              className="btn btn-outline btn-sm"
              type="button"
              onClick={triggerOverflow}
            >
              Push 7 toasts
            </button>
            <button
              className="btn btn-ghost btn-sm"
              type="button"
              onClick={clearAll}
            >
              Clear all
            </button>
          </div>
          <p className="text-sm opacity-70">
            Нажмите <kbd className="kbd kbd-sm">Esc</kbd>, чтобы закрыть
            последний toast. Наведение мыши останавливает auto-close.
          </p>
        </div>
      </div>

      <div className="card bg-base-100 border border-base-300">
        <div className="card-body gap-3">
          <h2 className="card-title text-lg">System Banner Scenarios</h2>
          <div className="flex flex-wrap gap-2">
            <button
              className="btn btn-sm btn-outline"
              type="button"
              onClick={() =>
                setDegradedMode(
                  "Деградированный режим: часть функций недоступна"
                )
              }
            >
              Enable degraded mode banner
            </button>
            <button
              className="btn btn-sm btn-ghost"
              type="button"
              onClick={clearDegradedMode}
            >
              Clear degraded mode banner
            </button>
          </div>
          <p className="text-sm opacity-70">
            Offline banner тестируется через DevTools Network: выберите Offline.
            WebSocket banner появляется при разрыве соединения.
          </p>
        </div>
      </div>

      <div className="card bg-base-100 border border-base-300">
        <div className="card-body">
          <h2 className="card-title text-lg">Form Example</h2>
          <NotificationDemoForm />
        </div>
      </div>
    </div>
  );
};
