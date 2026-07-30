import { usersApi } from "@/shared/api";

// Транспорт живёт в операциях модуля users; здесь только имена, под которыми
// эти ручки зовёт виджет команд.
export const listTeamMembers = usersApi.listTeamMembers;
export const deleteTeamMember = usersApi.deleteTeamMember;
export const leaveTeam = usersApi.leaveTeam;
