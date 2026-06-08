import { useState } from "react";
import "./App.css";
import { ConflictBanner } from "./components/ConflictBanner";
import { TaskForm } from "./components/TaskForm";
import { TaskRow } from "./components/TaskRow";
import { useTasks } from "./hooks/useTasks";
import { STATUS_LABEL, STATUS_VALUES } from "./schemas";
import type { TaskItemStatus } from "./schemas";

type Filter = TaskItemStatus | "All";

function App() {
  const [filter, setFilter] = useState<Filter>("All");
  const {
    tasks,
    loading,
    error,
    conflict,
    create,
    update,
    remove,
    resolveConflictReload,
    resolveConflictOverwrite,
    dismissError,
  } = useTasks(filter);

  return (
    <div className="app">
      <header>
        <h1>Tasks</h1>
      </header>

      {error && (
        <div className="error-banner" role="alert">
          <span>{error}</span>
          <button onClick={dismissError}>Dismiss</button>
        </div>
      )}

      {conflict && (
        <ConflictBanner
          conflict={conflict}
          onReload={resolveConflictReload}
          onOverwrite={resolveConflictOverwrite}
        />
      )}

      <section className="create-section">
        <h2>Add a task</h2>
        <TaskForm mode="create" onSubmit={create} />
      </section>

      <section className="list-section">
        <div className="list-header">
          <h2>Your tasks</h2>
          <div className="filter">
            <label htmlFor="filter">Filter</label>
            <select
              id="filter"
              value={filter}
              onChange={(e) => setFilter(e.target.value as Filter)}
            >
              <option value="All">All</option>
              {STATUS_VALUES.map((s) => (
                <option key={s} value={s}>{STATUS_LABEL[s]}</option>
              ))}
            </select>
          </div>
        </div>

        {loading ? (
          <p className="muted">Loading…</p>
        ) : tasks.length === 0 ? (
          <p className="muted empty">
            {filter === "All"
              ? "No tasks yet. Add one above to get started."
              : `No tasks with status "${STATUS_LABEL[filter as TaskItemStatus]}".`}
          </p>
        ) : (
          <ul className="task-list">
            {tasks.map((t) => (
              <TaskRow key={t.id} task={t} onUpdate={update} onDelete={remove} />
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

export default App;
