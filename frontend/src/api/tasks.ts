import { api } from "./client";
import {
  TaskListSchema,
  TaskSchema,
  type CreateTaskRequest,
  type Task,
  type TaskItemStatus,
  type UpdateTaskRequest,
} from "../schemas";

export function listTasks(status?: TaskItemStatus): Promise<Task[]> {
  const qs = status ? `?status=${status}` : "";
  return api(`/api/tasks${qs}`, {}, TaskListSchema);
}

export function createTask(body: CreateTaskRequest): Promise<Task> {
  return api(
    "/api/tasks",
    { method: "POST", body: JSON.stringify(body) },
    TaskSchema
  );
}

export function updateTask(id: string, body: UpdateTaskRequest): Promise<Task> {
  return api(
    `/api/tasks/${id}`,
    { method: "PUT", body: JSON.stringify(body) },
    TaskSchema
  );
}

export function deleteTask(id: string): Promise<void> {
  return api<void>(`/api/tasks/${id}`, { method: "DELETE" });
}
