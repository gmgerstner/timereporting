import { Dropdown, DropdownButton, Form, InputGroup } from 'react-bootstrap';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faSearch } from '@fortawesome/free-solid-svg-icons';

interface TitleInputProps {
  value: string;
  onChange: (value: string) => void;
  commonTitles: string[];
  recentTitles: string[];
  ariaLabel?: string;
}

/** Free-text task title with a dropdown of common and recent titles. */
export function TitleInput({
  value,
  onChange,
  commonTitles,
  recentTitles,
  ariaLabel = 'Task title',
}: TitleInputProps) {
  return (
    <InputGroup className="mb-3">
      <DropdownButton
        variant="outline-secondary"
        title={<FontAwesomeIcon icon={faSearch} />}
        id={`${ariaLabel.replace(/\s+/g, '-').toLowerCase()}-picker`}
      >
        {commonTitles.map((title) => (
          <Dropdown.Item key={`common-${title}`} onClick={() => onChange(title)}>
            {title}
          </Dropdown.Item>
        ))}
        {commonTitles.length > 0 && recentTitles.length > 0 && <Dropdown.Divider />}
        {recentTitles.map((title) => (
          <Dropdown.Item key={`recent-${title}`} onClick={() => onChange(title)}>
            {title}
          </Dropdown.Item>
        ))}
      </DropdownButton>
      <Form.Control
        type="text"
        aria-label={ariaLabel}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </InputGroup>
  );
}
