import { useState } from "react";
import { STATUS_LABEL } from "../types";
import type { Task, UpdateTaskRequest } from "../types";
import { TaskForm } from "./TaskForm";

interface Props {
  task: Task;
  onUpdate: (id: string, body: UpdateTaskRequest) => Promise<unknown>;
  onDelete: (id: string) => void;
}

function formatDue(iso: string | null): string {
  if (!iso) return "No due date";
  return new Date(iso).toLocaleString();
}

export function TaskRow({ task, onUpdate, onDelete }: Props) {
  const [editing, setEditing] = useState(false);

  if (editing) {
    return (
      <li className="task-row editing">
        <TaskForm
          mode="edit"
          initial={task}
          onSubmit={async (body) => {
            await onUpdate(task.id, body);
            setEditing(false);
          }}
          onCancel={() => setEditing(false)}
        />
      </li>
    );
  }

  const overdue =
    task.dueDate && task.status !== "Done" && new Date(task.dueDate) < new Date();

  return (
    <li className={`task-row status-${task.status}`}>
      <div className="task-main">
        <div className="task-header">
          <span className={`status-pill status-${task.status}`}>
            {STATUS_LABEL[task.status]}
          </span>
          <h3 className="task-title">{task.title}</h3>
        </div>
        {task.description && <p className="task-desc">{task.description}</p>}
        <p className={`task-meta ${overdue ? "overdue" : ""}`}>
          {overdue && "Overdue · "}{formatDue(task.dueDate)}
        </p>
      </div>
      <div className="task-actions">
        <button onClick={() => setEditing(true)}>Edit</button>
        <button
          className="danger"
          onClick={() => {
            if (confirm(`Delete "${task.title}"?`)) onDelete(task.id);
          }}
        >
          Delete
        </button>
      </div>
    </li>
  );
}
