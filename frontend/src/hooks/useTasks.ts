import { useCallback, useEffect, useState } from "react";
import { ApiException } from "../api/client";
import * as api from "../api/tasks";
import type {
  CreateTaskRequest,
  Task,
  TaskItemStatus,
  UpdateTaskRequest,
} from "../types";

export type ConflictInfo = {
  attempted: UpdateTaskRequest;
  current: Task;
  taskId: string;
};

export function useTasks(filter: TaskItemStatus | "All") {
  const [tasks, setTasks] = useState<Task[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState<ConflictInfo | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await api.listTasks(filter === "All" ? undefined : filter);
      setTasks(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load tasks.");
    } finally {
      setLoading(false);
    }
  }, [filter]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const create = useCallback(async (body: CreateTaskRequest) => {
    const created = await api.createTask(body);
    setTasks((prev) => [created, ...prev]);
    return created;
  }, []);

  const update = useCallback(
    async (id: string, body: UpdateTaskRequest) => {
      try {
        const updated = await api.updateTask(id, body);
        setTasks((prev) => prev.map((t) => (t.id === id ? updated : t)));
        return updated;
      } catch (e) {
        if (e instanceof ApiException && e.error.status === 409 && e.error.current) {
          setConflict({ attempted: body, current: e.error.current, taskId: id });
        }
        throw e;
      }
    },
    []
  );

  const remove = useCallback(async (id: string) => {
    // Optimistic: drop locally first, restore on failure so the list never
    // appears to lie about what's persisted.
    const prev = tasks;
    setTasks((current) => current.filter((t) => t.id !== id));
    try {
      await api.deleteTask(id);
    } catch (e) {
      setTasks(prev);
      setError(e instanceof Error ? e.message : "Failed to delete task.");
    }
  }, [tasks]);

  const resolveConflictReload = useCallback(() => {
    if (!conflict) return;
    setTasks((prev) => prev.map((t) => (t.id === conflict.taskId ? conflict.current : t)));
    setConflict(null);
  }, [conflict]);

  const resolveConflictOverwrite = useCallback(async () => {
    if (!conflict) return;
    const retried: UpdateTaskRequest = {
      ...conflict.attempted,
      version: conflict.current.version,
    };
    try {
      const updated = await api.updateTask(conflict.taskId, retried);
      setTasks((prev) => prev.map((t) => (t.id === conflict.taskId ? updated : t)));
      setConflict(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Overwrite failed.");
    }
  }, [conflict]);

  return {
    tasks,
    loading,
    error,
    conflict,
    refresh,
    create,
    update,
    remove,
    resolveConflictReload,
    resolveConflictOverwrite,
    dismissError: () => setError(null),
  };
}
