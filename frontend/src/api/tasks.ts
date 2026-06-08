import { api } from "./client";
import type {
  CreateTaskRequest,
  Task,
  TaskItemStatus,
  UpdateTaskRequest,
} from "../types";

export function listTasks(status?: TaskItemStatus): Promise<Task[]> {
  const qs = status ? `?status=${status}` : "";
  return api<Task[]>(`/api/tasks${qs}`);
}

export function createTask(body: CreateTaskRequest): Promise<Task> {
  return api<Task>("/api/tasks", {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function updateTask(id: string, body: UpdateTaskRequest): Promise<Task> {
  return api<Task>(`/api/tasks/${id}`, {
    method: "PUT",
    body: JSON.stringify(body),
  });
}

export function deleteTask(id: string): Promise<void> {
  return api<void>(`/api/tasks/${id}`, { method: "DELETE" });
}
