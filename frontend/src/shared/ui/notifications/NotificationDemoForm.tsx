import { FormEvent, useState } from "react";
import { notificationMessages, useNotifications } from "@/shared/model";

type DemoPayload = {
  title: string;
};

const postDemoData = async (payload: DemoPayload): Promise<void> => {
  const response = await fetch("/api/demo-endpoint", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    throw new Error(`Request failed with status ${response.status}`);
  }
};

export const NotificationDemoForm = () => {
  const { notify, notifySuccess, notifyError } = useNotifications();
  const [title, setTitle] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const submit = async () => {
    if (!title.trim()) {
      notify({
        type: "warning",
        title: "Проверьте форму",
        message: "Название не может быть пустым",
      });
      return;
    }

    setIsSubmitting(true);

    try {
      await postDemoData({ title });
      notifySuccess("Сохранено", "Данные успешно отправлены");
      setTitle("");
    } catch {
      notifyError("Не удалось сохранить", notificationMessages.errorRetry, {
        dedupeKey: "demo-save-error",
        retry: submit,
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void submit();
  };

  return (
    <form className="max-w-md space-y-3" onSubmit={handleSubmit}>
      <label className="form-control">
        <span className="label-text mb-1">Название</span>
        <input
          className="input input-bordered w-full"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          placeholder="Введите название"
          disabled={isSubmitting}
        />
      </label>

      <button className="btn btn-primary" type="submit" disabled={isSubmitting}>
        {isSubmitting ? "Сохраняем..." : "Сохранить"}
      </button>
    </form>
  );
};
