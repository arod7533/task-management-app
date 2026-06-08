import { z } from "zod";

// Schemas are the source of truth for the frontend's contract with the API.
// All TS types are inferred from these, and every fetched payload is parsed
// through one of them so wire-format drift surfaces as a clear error at the
// boundary instead of an undefined-property crash three components deep.

export const TaskItemStatusSchema = z.enum(["Todo", "InProgress", "Done"]);
export type TaskItemStatus = z.infer<typeof TaskItemStatusSchema>;

export const STATUS_VALUES = TaskItemStatusSchema.options;

export const STATUS_LABEL: Record<TaskItemStatus, string> = {
  Todo: "To do",
  InProgress: "In progress",
  Done: "Done",
};

export const TaskSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  description: z.string().nullable(),
  dueDate: z.string().nullable(),
  status: TaskItemStatusSchema,
  createdAt: z.string(),
  updatedAt: z.string(),
  version: z.number().int(),
});
export type Task = z.infer<typeof TaskSchema>;

export const TaskListSchema = z.array(TaskSchema);

// Request bodies — also Zod-shaped so the client form can validate against
// the same rules the server enforces.
export const CreateTaskRequestSchema = z.object({
  title: z.string().min(1, "Title is required.").max(200, "Title must be 1-200 characters."),
  description: z.string().max(2000, "Description must be 2000 characters or fewer.").nullable(),
  dueDate: z.string().nullable(),
  status: TaskItemStatusSchema,
});
export type CreateTaskRequest = z.infer<typeof CreateTaskRequestSchema>;

export const UpdateTaskRequestSchema = CreateTaskRequestSchema.extend({
  version: z.number().int(),
});
export type UpdateTaskRequest = z.infer<typeof UpdateTaskRequestSchema>;

// ProblemDetails error shapes.
const BaseProblemSchema = z.object({
  status: z.number().optional(),
  title: z.string().optional(),
  detail: z.string().optional(),
  // Validation errors: { Title: ["Title is required."], ... }
  errors: z.record(z.string(), z.array(z.string())).optional(),
});

export const ConflictProblemSchema = BaseProblemSchema.extend({
  current: TaskSchema,
});

export type ApiError = {
  status: number;
  title: string;
  detail?: string;
  errors?: Record<string, string[]>;
  current?: Task;
};

// Auth contracts.
export const RegisterRequestSchema = z.object({
  email: z.string().email("Enter a valid email address."),
  password: z.string().min(8, "Password must be at least 8 characters."),
});
export type RegisterRequest = z.infer<typeof RegisterRequestSchema>;

export const LoginRequestSchema = z.object({
  email: z.string().email("Enter a valid email address."),
  password: z.string().min(1, "Password is required."),
});
export type LoginRequest = z.infer<typeof LoginRequestSchema>;

export const AuthResponseSchema = z.object({
  token: z.string(),
  expiresAt: z.string(),
  userId: z.string().uuid(),
  email: z.string(),
});
export type AuthResponse = z.infer<typeof AuthResponseSchema>;
