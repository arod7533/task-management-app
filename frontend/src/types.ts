export type TaskItemStatus = "Todo" | "InProgress" | "Done";

export const STATUS_VALUES: TaskItemStatus[] = ["Todo", "InProgress", "Done"];

export const STATUS_LABEL: Record<TaskItemStatus, string> = {
  Todo: "To do",
  InProgress: "In progress",
  Done: "Done",
};

export interface Task {
  id: string;
  title: string;
  description: string | null;
  dueDate: string | null;
  status: TaskItemStatus;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export interface CreateTaskRequest {
  title: string;
  description: string | null;
  dueDate: string | null;
  status: TaskItemStatus;
}

export interface UpdateTaskRequest extends CreateTaskRequest {
  version: number;
}

export interface ApiError {
  status: number;
  title: string;
  detail?: string;
  // Validation errors: { Title: ["Title is required."], ... }
  errors?: Record<string, string[]>;
  // 409 conflict body includes the server's current state.
  current?: Task;
}
