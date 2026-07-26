import { authorizationApi, authorizationPath } from "@/shared/api";

export async function logout(): Promise<void> {
  await authorizationApi.post(authorizationPath("/logout"));
}
