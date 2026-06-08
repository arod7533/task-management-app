import { useState, type FormEvent } from "react";
import { ApiException } from "../api/client";
import {
  CreateTaskRequestSchema,
  STATUS_LABEL,
  STATUS_VALUES,
} from "../schemas";
import type { Task, TaskItemStatus, UpdateTaskRequest } from "../schemas";

type Props =
  | {
      mode: "create";
      onSubmit: (body: {
        title: string;
        description: string | null;
        dueDate: string | null;
        status: TaskItemStatus;
      }) => Promise<unknown>;
      onCancel?: () => void;
    }
  | {
      mode: "edit";
      initial: Task;
      onSubmit: (body: UpdateTaskRequest) => Promise<unknown>;
      onCancel: () => void;
    };

// HTML datetime-local needs "YYYY-MM-DDTHH:mm" in *local* time. Convert from
// the API's ISO/UTC string by going through Date (which respects the runtime
// timezone) and trimming.
function isoToLocalInput(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

// User entered a local wall-clock time. Interpret it in their timezone and
// send as ISO/UTC so the server stores an unambiguous instant.
function localInputToIso(local: string): string | null {
  if (!local) return null;
  return new Date(local).toISOString();
}

export function TaskForm(props: Props) {
  const initial = props.mode === "edit" ? props.initial : null;

  const [title, setTitle] = useState(initial?.title ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [dueDate, setDueDate] = useState(isoToLocalInput(initial?.dueDate ?? null));
  const [status, setStatus] = useState<TaskItemStatus>(initial?.status ?? "Todo");
  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [formError, setFormError] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setFormError(null);

    // Validate through the same Zod schema the API client uses. The server's
    // [Required]/[StringLength] attributes are mirrored in the schema, so a
    // failure here would have been a 400 from the API anyway — we just catch
    // it one round-trip earlier.
    const parsed = CreateTaskRequestSchema.safeParse({
      title: title.trim(),
      description: description.trim() ? description.trim() : null,
      dueDate: localInputToIso(dueDate),
      status,
    });

    if (!parsed.success) {
      // Server's ASP.NET ProblemDetails uses PascalCase keys ("Title");
      // mirror that here so the rendering code is the same in both paths.
      const errs: Record<string, string[]> = {};
      for (const issue of parsed.error.issues) {
        const key = issue.path[0];
        if (typeof key !== "string") continue;
        const pascal = key.charAt(0).toUpperCase() + key.slice(1);
        (errs[pascal] ??= []).push(issue.message);
      }
      setFieldErrors(errs);
      return;
    }

    const body = parsed.data;

    setSubmitting(true);
    try {
      if (props.mode === "create") {
        await props.onSubmit(body);
        setTitle("");
        setDescription("");
        setDueDate("");
        setStatus("Todo");
      } else {
        await props.onSubmit({ ...body, version: props.initial.version });
      }
    } catch (e) {
      if (e instanceof ApiException) {
        if (e.error.errors) setFieldErrors(e.error.errors);
        else setFormError(e.error.detail ?? e.error.title);
      } else if (e instanceof Error) {
        setFormError(e.message);
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="task-form" onSubmit={handleSubmit} noValidate>
      <div className="field">
        <label htmlFor="title">Title</label>
        <input
          id="title"
          type="text"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          maxLength={200}
          aria-invalid={!!fieldErrors.Title}
        />
        {fieldErrors.Title?.map((m) => (
          <span key={m} className="field-error">{m}</span>
        ))}
      </div>

      <div className="field">
        <label htmlFor="description">Description</label>
        <textarea
          id="description"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          maxLength={2000}
          rows={3}
        />
        {fieldErrors.Description?.map((m) => (
          <span key={m} className="field-error">{m}</span>
        ))}
      </div>

      <div className="field-row">
        <div className="field">
          <label htmlFor="dueDate">Due</label>
          <input
            id="dueDate"
            type="datetime-local"
            value={dueDate}
            onChange={(e) => setDueDate(e.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="status">Status</label>
          <select
            id="status"
            value={status}
            onChange={(e) => setStatus(e.target.value as TaskItemStatus)}
          >
            {STATUS_VALUES.map((s) => (
              <option key={s} value={s}>{STATUS_LABEL[s]}</option>
            ))}
          </select>
        </div>
      </div>

      {formError && <div className="form-error">{formError}</div>}

      <div className="form-actions">
        <button type="submit" disabled={submitting}>
          {submitting ? "Saving…" : props.mode === "create" ? "Add task" : "Save changes"}
        </button>
        {props.onCancel && (
          <button type="button" onClick={props.onCancel} disabled={submitting}>
            Cancel
          </button>
        )}
      </div>
    </form>
  );
}
