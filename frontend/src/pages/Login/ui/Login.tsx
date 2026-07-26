import { useState, useEffect } from "react";
import { useSearchParams } from "react-router-dom";
import { authType } from "@/shared/config";
import { hasOidcAutoAttempted, markOidcAutoAttempted } from "@/shared/lib/auth";
import { useLoginNext } from "../lib/useLoginNext";

type Props = {
  next: string;
};

const OidcLogin = ({ next }: Props) => {
  const [showButton, setShowButton] = useState(false);

  const callbackUrl = `/api/authorization/v1/external/token/callback?next=${encodeURIComponent(next)}`;

  useEffect(() => {
    if (hasOidcAutoAttempted()) {
      setShowButton(true);
      return;
    }
    markOidcAutoAttempted();
    window.location.href = callbackUrl;
  }, [callbackUrl]);

  if (!showButton) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="loading loading-spinner loading-lg"></div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-base-200">
      <div className="card bg-base-100 shadow-xl p-8 w-full max-w-md">
        <h1 className="text-2xl font-bold text-center mb-6">Вход в систему</h1>
        <button
          className="btn btn-primary w-full"
          onClick={() => {
            window.location.href = callbackUrl;
          }}
        >
          Войти через SSO
        </button>
      </div>
    </div>
  );
};

const FakeLogin = ({ next }: Props) => {
  const [externalId, setExternalId] = useState("default-1");
  const [name, setName] = useState("Дефолтов Дефолт");
  const [imageUrl, setImageUrl] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const params = new URLSearchParams({
      externalId,
      ...(name && { name }),
      ...(imageUrl && { imageUrl }),
      next,
    });
    window.location.href = `/api/authorization/v1/fake/login?${params}`;
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-base-200">
      <div className="card bg-base-100 shadow-xl p-8 w-full max-w-md">
        <h1 className="text-2xl font-bold text-center mb-6">
          Вход (Dev режим)
        </h1>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div className="form-control">
            <label className="label">
              <span className="label-text">External ID *</span>
            </label>
            <input
              type="text"
              placeholder="user-123"
              className="input input-bordered w-full"
              value={externalId}
              onChange={(e) => setExternalId(e.target.value)}
              required
            />
          </div>
          <div className="form-control">
            <label className="label">
              <span className="label-text">Имя</span>
            </label>
            <input
              type="text"
              placeholder="Иван Иванов"
              className="input input-bordered w-full"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>
          <div className="form-control">
            <label className="label">
              <span className="label-text">URL аватара</span>
            </label>
            <input
              type="url"
              placeholder="https://example.com/avatar.png"
              className="input input-bordered w-full"
              value={imageUrl}
              onChange={(e) => setImageUrl(e.target.value)}
            />
          </div>
          <button type="submit" className="btn btn-primary w-full mt-4">
            Войти
          </button>
        </form>
      </div>
    </div>
  );
};

export const Login = () => {
  const [searchParams] = useSearchParams();
  const next = useLoginNext(searchParams);

  if (authType === "oidc") {
    return <OidcLogin next={next} />;
  }

  return <FakeLogin next={next} />;
};
