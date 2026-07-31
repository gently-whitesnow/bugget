import { authorizationApi } from "@/shared/api";

/**
 * Выход из системы. Транспорт живёт в операциях модуля
 * (`shared/api/authorization`) — здесь только имя, под которым его зовёт модель.
 */
export async function logout(): Promise<void> {
  await authorizationApi.logout();
}
