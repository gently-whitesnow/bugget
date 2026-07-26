import { useState, useRef, useEffect } from "react";
import { ReportLinkDto } from "@/entities/report";
import { extractDomainName } from "@/shared/lib";
import { linkMaxLength, linkNameMaxLength } from "@/shared/config";

type Props = {
  initialValues?: ReportLinkDto;
  onSave: (dto: ReportLinkDto) => void;
  onCancel: () => void;
};

const LinkForm = ({ initialValues, onSave, onCancel }: Props) => {
  const [name, setName] = useState(initialValues?.name ?? "");
  const [link, setLink] = useState(initialValues?.link ?? "");
  const [isNameManuallyEdited, setIsNameManuallyEdited] = useState(
    !!initialValues?.name
  );
  const formRef = useRef<HTMLFormElement>(null);
  const linkInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    linkInputRef.current?.focus();
  }, []);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (formRef.current && !formRef.current.contains(event.target as Node)) {
        onCancel();
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [onCancel]);

  const handleLinkChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newLink = e.target.value;
    setLink(newLink);

    // Автоматически заполняем название из домена, если оно не было изменено вручную
    if (!isNameManuallyEdited && newLink.trim()) {
      const domainName = extractDomainName(newLink);
      if (domainName) {
        setName(domainName);
      }
    }
  };

  const handleNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setName(e.target.value);
    setIsNameManuallyEdited(true);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !link.trim()) return;
    onSave({ name: name.trim(), link: link.trim() });
  };

  const isValid = name.trim() && link.trim();

  return (
    <form
      ref={formRef}
      onSubmit={handleSubmit}
      onKeyDown={(e) => e.key === "Escape" && onCancel()}
      className="absolute left-0 top-0 z-40 flex w-[min(100vw-2rem,20rem)] flex-col gap-2 rounded-lg border border-base-300 bg-base-100 p-3 shadow-lg"
    >
      <input
        ref={linkInputRef}
        type="url"
        placeholder="https://..."
        value={link}
        onChange={handleLinkChange}
        className="input input-sm input-bordered w-full"
        maxLength={linkMaxLength}
      />
      <input
        type="text"
        placeholder="Название ссылки"
        value={name}
        onChange={handleNameChange}
        className="input input-sm input-bordered w-full"
        maxLength={linkNameMaxLength}
      />
      <div className="flex gap-2 justify-end">
        <button
          type="button"
          className="btn btn-sm btn-ghost"
          onClick={onCancel}
        >
          Отмена
        </button>
        <button
          type="submit"
          className="btn btn-sm btn-primary"
          disabled={!isValid}
        >
          Сохранить
        </button>
      </div>
    </form>
  );
};

export default LinkForm;
