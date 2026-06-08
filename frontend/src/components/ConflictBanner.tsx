import { STATUS_LABEL } from "../schemas";
import type { ConflictInfo } from "../hooks/useTasks";

interface Props {
  conflict: ConflictInfo;
  onReload: () => void;
  onOverwrite: () => void;
}

export function ConflictBanner({ conflict, onReload, onOverwrite }: Props) {
  const { attempted, current } = conflict;
  return (
    <div className="conflict-banner" role="alert">
      <h3>This task was changed in another session</h3>
      <p>Choose how to resolve:</p>
      <div className="conflict-cols">
        <div>
          <h4>Current on server (v{current.version})</h4>
          <p><strong>{current.title}</strong></p>
          <p className="muted">{STATUS_LABEL[current.status]}</p>
          {current.description && <p>{current.description}</p>}
        </div>
        <div>
          <h4>Your pending edit</h4>
          <p><strong>{attempted.title}</strong></p>
          <p className="muted">{STATUS_LABEL[attempted.status]}</p>
          {attempted.description && <p>{attempted.description}</p>}
        </div>
      </div>
      <div className="conflict-actions">
        <button onClick={onReload}>Discard mine, keep server's</button>
        <button className="danger" onClick={onOverwrite}>Overwrite with mine</button>
      </div>
    </div>
  );
}
