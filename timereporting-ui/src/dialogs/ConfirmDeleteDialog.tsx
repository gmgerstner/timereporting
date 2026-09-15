import { Button, Modal } from 'react-bootstrap';
import type { TimeEntry } from '../models';

interface ConfirmDeleteDialogProps {
  entry: TimeEntry;
  onClose: () => void;
  onConfirm: () => void;
}

export function ConfirmDeleteDialog({ entry, onClose, onConfirm }: ConfirmDeleteDialogProps) {
  return (
    <Modal show onHide={onClose} centered>
      <Modal.Header closeButton>
        <Modal.Title>Delete Time Entry</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        Delete Time Entry for: <strong>{entry.title}</strong>?
      </Modal.Body>
      <Modal.Footer>
        <Button variant="outline-dark" onClick={onConfirm}>
          Yes
        </Button>
        <Button variant="outline-dark" onClick={onClose}>
          No
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
